using System.IO;
using System.Threading.Tasks;
using AvaloniaEdit.Document;
using BlankSlate.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BlankSlate.ViewModels;

/// <summary>
/// Abstraction over the editor control so the view-model can drive
/// clipboard/undo operations without referencing the view directly.
/// </summary>
public interface IEditorHandle
{
    void Undo();
    void Redo();
    void Cut();
    void Copy();
    void Paste();
    void SelectAll();
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

    public DocumentViewModel()
    {
        Title = $"new {++_untitledCounter}";
        Document.TextChanged += OnTextChanged;
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
}
