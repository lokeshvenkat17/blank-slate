using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AvaloniaEdit.Document;
using BlankSlate.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BlankSlate.ViewModels;

/// <summary>
/// Abstraction over the editor control so the view-model can drive
/// clipboard/undo/selection operations without referencing the view directly.
/// </summary>
public interface IEditorHandle
{
    void Undo();
    void Redo();
    void Cut();
    void Copy();
    void Paste();
    void SelectAll();

    string? SelectedText { get; }
    int CaretOffset { get; set; }
    int SelectionStart { get; }
    int SelectionLength { get; }

    /// <summary>Selects the given range and scrolls it into view.</summary>
    void SelectAndReveal(int start, int length);

    /// <summary>Moves the caret to the start of <paramref name="line"/> and scrolls it into view.</summary>
    void GoToLine(int line);

    Task SetClipboardTextAsync(string text);
    Task<string?> GetClipboardTextAsync();
}

/// <summary>One open document (one tab), mirroring a Notepad++ buffer.</summary>
public partial class DocumentViewModel : ViewModelBase
{
    private static int _untitledCounter;
    private bool _suppressDirty;

    public TextDocument Document { get; } = new();

    /// <summary>Set by EditorView when the tab's editor is attached.</summary>
    public IEditorHandle? EditorHandle { get; set; }

    /// <summary>Global view preferences (word wrap, zoom, etc.), injected by MainViewModel.</summary>
    public EditorSettings? Settings { get; set; }

    [ObservableProperty]
    public partial string? FilePath { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial bool IsDirty { get; set; }

    [ObservableProperty]
    public partial int CaretLine { get; set; } = 1;

    [ObservableProperty]
    public partial int CaretColumn { get; set; } = 1;

    /// <summary>Encoding the file will be saved with. UTF-8 (no BOM) for new files, like Notepad++.</summary>
    [ObservableProperty]
    public partial TextEncodingKind EncodingKind { get; set; } = TextEncodingKind.Utf8;

    /// <summary>Line-ending convention. LF for new files (this app targets macOS/Linux first).</summary>
    [ObservableProperty]
    public partial EolMode EolMode { get; set; } = EolMode.Lf;

    public string EncodingLabel => TextEncodings.GetLabel(EncodingKind);
    public string EolLabel => EolModes.GetLabel(EolMode);

    partial void OnEncodingKindChanged(TextEncodingKind value) => OnPropertyChanged(nameof(EncodingLabel));
    partial void OnEolModeChanged(EolMode value) => OnPropertyChanged(nameof(EolLabel));

    public BookmarkManager Bookmarks { get; }

    /// <summary>Active "Mark All" highlight pattern; EditorView re-applies it when the tab is shown.</summary>
    [ObservableProperty]
    public partial Regex? MarkPattern { get; set; }

    /// <summary>Line to jump to once an editor attaches (used when opening a search result in a not-yet-materialized tab).</summary>
    public int? PendingCaretLine { get; set; }

    public DocumentViewModel()
    {
        Title = $"new {++_untitledCounter}";
        Bookmarks = new BookmarkManager(Document);
        Document.TextChanged += OnTextChanged;
    }

    /// <summary>Jumps to a line now if an editor is attached, or defers until one is.</summary>
    public void RequestGoToLine(int line)
    {
        if (EditorHandle is not null)
            EditorHandle.GoToLine(line);
        else
            PendingCaretLine = line;
    }

    private void OnTextChanged(object? sender, System.EventArgs e)
    {
        if (!_suppressDirty)
            IsDirty = true;
    }

    public static async Task<DocumentViewModel> LoadFromFileAsync(string path)
    {
        var doc = new DocumentViewModel();
        var bytes = await File.ReadAllBytesAsync(path);
        var (kind, _, text) = TextEncodings.DetectAndDecode(bytes);

        doc._suppressDirty = true;
        doc.Document.Text = text;
        doc._suppressDirty = false;
        doc.Document.UndoStack.ClearAll();

        doc.EncodingKind = kind;
        doc.EolMode = EolModes.Detect(text);
        doc.FilePath = path;
        doc.Title = Path.GetFileName(path);
        doc.IsDirty = false;
        return doc;
    }

    /// <summary>Saves to <see cref="FilePath"/> (caller ensures it is set).</summary>
    public async Task SaveAsync()
    {
        await File.WriteAllTextAsync(FilePath!, Document.Text, TextEncodings.GetEncoding(EncodingKind));
        Title = Path.GetFileName(FilePath!);
        IsDirty = false;
    }

    /// <summary>"Encode in X": reinterprets how the in-memory text will be written on next save, like Notepad++'s Encoding menu.</summary>
    public void SetEncoding(TextEncodingKind kind)
    {
        if (EncodingKind == kind)
            return;
        EncodingKind = kind;
        IsDirty = true;
    }

    /// <summary>Rewrites every line ending in the buffer to <paramref name="mode"/>.</summary>
    public void ConvertEol(EolMode mode)
    {
        if (EolMode == mode)
            return;
        Document.Text = EolModes.Normalize(Document.Text, mode);
        EolMode = mode;
    }

    // ---- Bookmarked-line operations (Notepad++ Search > Bookmark submenu) ----

    /// <summary>Concatenated text of all bookmarked lines (with trailing newline each), or null when none.</summary>
    public string? GetBookmarkedLinesText()
    {
        var lines = Bookmarks.Lines;
        if (lines.Count == 0)
            return null;
        var sb = new System.Text.StringBuilder();
        foreach (var lineNumber in lines)
        {
            var line = Document.GetLineByNumber(lineNumber);
            sb.Append(Document.GetText(line.Offset, line.Length)).Append('\n');
        }
        return sb.ToString();
    }

    public void RemoveBookmarkedLines() => RemoveLines(Bookmarks.Lines.ToHashSet(), keepBookmarked: false);

    public void RemoveNonBookmarkedLines() => RemoveLines(Bookmarks.Lines.ToHashSet(), keepBookmarked: true);

    private void RemoveLines(HashSet<int> bookmarked, bool keepBookmarked)
    {
        if (bookmarked.Count == 0 && !keepBookmarked)
            return;
        using (Document.RunUpdate())
        {
            for (var lineNumber = Document.LineCount; lineNumber >= 1; lineNumber--)
            {
                var isBookmarked = bookmarked.Contains(lineNumber);
                if (isBookmarked == keepBookmarked)
                    continue;
                var line = Document.GetLineByNumber(lineNumber);
                Document.Remove(line.Offset, line.TotalLength);
            }
        }
        Bookmarks.NotifyChanged();
    }

    /// <summary>Replaces each bookmarked line's text with the corresponding line of <paramref name="clipboardText"/> (last line reused when clipboard is shorter).</summary>
    public void PasteToBookmarkedLines(string clipboardText)
    {
        var bookmarkedLines = Bookmarks.Lines;
        if (bookmarkedLines.Count == 0)
            return;
        var replacementLines = clipboardText.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        using (Document.RunUpdate())
        {
            // Descending so earlier offsets stay valid while replacing.
            for (var i = bookmarkedLines.Count - 1; i >= 0; i--)
            {
                var line = Document.GetLineByNumber(bookmarkedLines[i]);
                var replacement = replacementLines[System.Math.Min(i, replacementLines.Length - 1)];
                Document.Replace(line.Offset, line.Length, replacement);
            }
        }
    }
}
