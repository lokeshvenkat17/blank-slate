using System.IO;
using System.Text;
using System.Threading.Tasks;
using AvaloniaEdit.Document;
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

    /// <summary>Encoding used when the file was read; reused on save. UTF-8 (no BOM) for new files, like Notepad++.</summary>
    public Encoding Encoding { get; set; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public string EncodingName => Encoding is UTF8Encoding u
        ? (u.GetPreamble().Length > 0 ? "UTF-8-BOM" : "UTF-8")
        : Encoding.WebName.ToUpperInvariant();

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
        // detectEncodingFromByteOrderMarks handles UTF-8/16/32 BOMs; richer
        // heuristic detection (ANSI code pages etc.) arrives in Phase 2.
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync();
        doc._suppressDirty = true;
        doc.Document.Text = text;
        doc._suppressDirty = false;
        doc.Document.UndoStack.ClearAll();
        doc.Encoding = reader.CurrentEncoding;
        doc.FilePath = path;
        doc.Title = Path.GetFileName(path);
        doc.IsDirty = false;
        return doc;
    }

    /// <summary>Saves to <see cref="FilePath"/> (caller ensures it is set).</summary>
    public async Task SaveAsync()
    {
        await File.WriteAllTextAsync(FilePath!, Document.Text, Encoding);
        Title = Path.GetFileName(FilePath!);
        IsDirty = false;
    }
}
