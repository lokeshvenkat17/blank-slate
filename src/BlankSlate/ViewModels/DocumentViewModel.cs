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
    void Delete();
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

    /// <summary>Re-dispatches recorded macro input onto the editor.</summary>
    void ReplayText(string text);
    void ReplayKey(Avalonia.Input.Key key, Avalonia.Input.KeyModifiers modifiers);
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

    /// <summary>TextMate language id (null = plain text). Auto-detected from the file extension.</summary>
    [ObservableProperty]
    public partial string? LanguageId { get; set; }

    public string LanguageName => Services.SyntaxService.GetDisplayNameById(LanguageId) ?? "Normal Text";

    partial void OnLanguageIdChanged(string? value) => OnPropertyChanged(nameof(LanguageName));

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

    /// <summary>Keeps "new N" numbering unique after restoring session tabs named by a previous run.</summary>
    public static void EnsureUntitledCounterAtLeast(int value)
    {
        if (_untitledCounter < value)
            _untitledCounter = value;
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
        doc.LanguageId = Services.SyntaxService.DetectLanguageId(path);
        doc.IsDirty = false;
        return doc;
    }

    /// <summary>Saves to <see cref="FilePath"/> (caller ensures it is set).</summary>
    public async Task SaveAsync()
    {
        await File.WriteAllTextAsync(FilePath!, Document.Text, TextEncodings.GetEncoding(EncodingKind));
        Title = Path.GetFileName(FilePath!);
        // Save As may have given the file a new extension; re-detect unless the user picked a language manually.
        LanguageId ??= Services.SyntaxService.DetectLanguageId(FilePath!);
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

    // ---- Edit-menu text operations (Phase 6a) ----

    private string EolTerminator => EolModes.GetTerminator(EolMode);

    /// <summary>1-based inclusive line range covered by the selection (or the caret line).</summary>
    private (int StartLine, int EndLine) GetTargetLineRange()
    {
        if (EditorHandle is not { } handle)
            return (CaretLine, CaretLine);
        if (handle.SelectionLength == 0)
            return (CaretLine, CaretLine);
        var start = Document.GetLineByOffset(handle.SelectionStart).LineNumber;
        var endOffset = handle.SelectionStart + handle.SelectionLength;
        var endLine = Document.GetLineByOffset(endOffset);
        // A selection ending exactly at a line start shouldn't drag that line in.
        if (endOffset == endLine.Offset && endLine.LineNumber > start)
            return (start, endLine.LineNumber - 1);
        return (start, endLine.LineNumber);
    }

    private List<string> GetLines(int startLine, int endLine)
    {
        var lines = new List<string>(endLine - startLine + 1);
        for (var i = startLine; i <= endLine; i++)
        {
            var line = Document.GetLineByNumber(i);
            lines.Add(Document.GetText(line.Offset, line.Length));
        }
        return lines;
    }

    private void ReplaceLineRange(int startLine, int endLine, IReadOnlyList<string> newLines)
    {
        var start = Document.GetLineByNumber(startLine).Offset;
        var end = Document.GetLineByNumber(endLine).EndOffset;
        Document.Replace(start, end - start, string.Join(EolTerminator, newLines));
    }

    /// <summary>Case conversion applies to the selection only (like Notepad++).</summary>
    public void ApplyCase(CaseKind kind)
    {
        if (EditorHandle is not { SelectionLength: > 0 } handle)
            return;
        var start = handle.SelectionStart;
        var length = handle.SelectionLength;
        var converted = Services.TextOperations.ConvertCase(Document.GetText(start, length), kind);
        Document.Replace(start, length, converted);
        handle.SelectAndReveal(start, converted.Length);
    }

    public void ApplyLineOp(LineOpKind kind)
    {
        var (startLine, endLine) = GetTargetLineRange();
        using (Document.RunUpdate())
        {
            switch (kind)
            {
                case LineOpKind.Duplicate:
                {
                    var block = GetLines(startLine, endLine);
                    var insertAt = Document.GetLineByNumber(endLine).EndOffset;
                    Document.Insert(insertAt, EolTerminator + string.Join(EolTerminator, block));
                    break;
                }
                case LineOpKind.JoinLines:
                {
                    if (endLine == startLine && endLine < Document.LineCount)
                        endLine++; // joining needs at least two lines
                    var joined = string.Join(" ", GetLines(startLine, endLine).Select(l => l.Trim()));
                    ReplaceLineRange(startLine, endLine, [joined]);
                    break;
                }
                case LineOpKind.MoveUp when startLine > 1:
                {
                    var block = GetLines(startLine, endLine);
                    var above = GetLines(startLine - 1, startLine - 1)[0];
                    ReplaceLineRange(startLine - 1, endLine, [.. block, above]);
                    EditorHandle?.GoToLine(startLine - 1);
                    break;
                }
                case LineOpKind.MoveDown when endLine < Document.LineCount:
                {
                    var block = GetLines(startLine, endLine);
                    var below = GetLines(endLine + 1, endLine + 1)[0];
                    ReplaceLineRange(startLine, endLine + 1, [below, .. block]);
                    EditorHandle?.GoToLine(startLine + 1);
                    break;
                }
                case LineOpKind.BlankAbove:
                    Document.Insert(Document.GetLineByNumber(startLine).Offset, EolTerminator);
                    break;
                case LineOpKind.BlankBelow:
                    Document.Insert(Document.GetLineByNumber(endLine).EndOffset, EolTerminator);
                    break;
                case LineOpKind.RemoveDuplicates or LineOpKind.RemoveConsecutiveDuplicates
                    or LineOpKind.RemoveEmpty or LineOpKind.RemoveEmptyWithBlank
                    or LineOpKind.Reverse or LineOpKind.Randomize:
                {
                    // Selection scope, or the whole document when nothing is selected (Notepad++ behavior).
                    var wholeDoc = EditorHandle is not { SelectionLength: > 0 };
                    var (s, e) = wholeDoc ? (1, Document.LineCount) : (startLine, endLine);
                    ReplaceLineRange(s, e, Services.TextOperations.ApplyLineOp(GetLines(s, e), kind));
                    break;
                }
            }
        }
    }

    public void ApplySort(SortKind kind)
    {
        var wholeDoc = EditorHandle is not { SelectionLength: > 0 };
        var (startLine, endLine) = wholeDoc ? (1, Document.LineCount) : GetTargetLineRange();
        if (endLine <= startLine)
            return;
        ReplaceLineRange(startLine, endLine, Services.TextOperations.SortLines(GetLines(startLine, endLine), kind));
    }

    public void ApplyBlankOp(BlankOpKind kind)
    {
        var newText = Services.TextOperations.ApplyBlankOp(Document.Text, kind);
        if (newText != Document.Text)
            Document.Text = newText;
    }

    public void ApplyIndent(bool increase)
    {
        var (startLine, endLine) = GetTargetLineRange();
        ReplaceLineRange(startLine, endLine, Services.TextOperations.Indent(GetLines(startLine, endLine), increase));
    }

    public void ApplyComment(CommentOpKind kind)
    {
        var tokens = CommentDefinitions.Get(LanguageId);
        using (Document.RunUpdate())
        {
            switch (kind)
            {
                case CommentOpKind.ToggleLine or CommentOpKind.SetLine or CommentOpKind.RemoveLine:
                {
                    if (tokens?.Line is not { } token)
                        return;
                    var (startLine, endLine) = GetTargetLineRange();
                    var lines = GetLines(startLine, endLine);
                    var uncomment = kind == CommentOpKind.RemoveLine
                        || (kind == CommentOpKind.ToggleLine && Services.TextOperations.AllLinesCommented(lines, token));
                    var newLines = uncomment
                        ? Services.TextOperations.UncommentLines(lines, token)
                        : Services.TextOperations.CommentLines(lines, token);
                    ReplaceLineRange(startLine, endLine, newLines);
                    break;
                }
                case CommentOpKind.BlockSet:
                {
                    if (tokens is not { BlockStart: { } bs, BlockEnd: { } be } || EditorHandle is not { SelectionLength: > 0 } handle)
                        return;
                    var start = handle.SelectionStart;
                    var length = handle.SelectionLength;
                    Document.Insert(start + length, be);
                    Document.Insert(start, bs);
                    handle.SelectAndReveal(start, length + bs.Length + be.Length);
                    break;
                }
                case CommentOpKind.BlockRemove:
                {
                    if (tokens is not { BlockStart: { } bs, BlockEnd: { } be } || EditorHandle is not { SelectionLength: > 0 } handle)
                        return;
                    var text = Document.GetText(handle.SelectionStart, handle.SelectionLength).Trim();
                    if (!text.StartsWith(bs, System.StringComparison.Ordinal) || !text.EndsWith(be, System.StringComparison.Ordinal))
                        return;
                    var inner = text[bs.Length..^be.Length];
                    Document.Replace(handle.SelectionStart, handle.SelectionLength, inner);
                    break;
                }
            }
        }
    }

    public void InsertDateTime(bool longFormat)
    {
        if (EditorHandle is not { } handle)
            return;
        var now = System.DateTime.Now;
        var text = longFormat
            ? $"{now.ToShortTimeString()} {now.ToLongDateString()}"
            : $"{now.ToShortTimeString()} {now.ToShortDateString()}";
        Document.Insert(handle.CaretOffset, text);
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
