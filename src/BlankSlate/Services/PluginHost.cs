using System;
using System.Collections.Generic;
using System.Linq;
using BlankSlate.Plugins;
using BlankSlate.ViewModels;

namespace BlankSlate.Services;

/// <summary>A command a plugin contributed to the Plugins menu.</summary>
public sealed record PluginCommand(string PluginName, string Title, Action Action);

/// <summary>
/// Adapts <see cref="DocumentViewModel"/> to the public <see cref="IEditorDocument"/> contract.
/// Selection and caret operations need an attached editor; they degrade gracefully when the
/// tab has never been shown.
/// </summary>
internal sealed class EditorDocumentAdapter(DocumentViewModel doc) : IEditorDocument
{
    internal DocumentViewModel Inner => doc;

    public string? FilePath => doc.FilePath;
    public string Title => doc.Title;
    public string? LanguageId => doc.LanguageId;
    public bool IsDirty => doc.IsDirty;

    public string Text
    {
        get => doc.Document.Text;
        set => doc.Document.Text = value;
    }

    public int TextLength => doc.Document.TextLength;
    public int LineCount => doc.Document.LineCount;
    public int CaretLine => doc.CaretLine;

    public int CaretOffset
    {
        get => doc.EditorHandle?.CaretOffset ?? 0;
        set { if (doc.EditorHandle is { } h) h.CaretOffset = value; }
    }

    public string SelectedText => doc.EditorHandle?.SelectedText ?? "";
    public int SelectionStart => doc.EditorHandle?.SelectionStart ?? 0;
    public int SelectionLength => doc.EditorHandle?.SelectionLength ?? 0;

    public string GetLineText(int lineNumber)
    {
        if (lineNumber < 1 || lineNumber > doc.Document.LineCount)
            throw new ArgumentOutOfRangeException(nameof(lineNumber));
        var line = doc.Document.GetLineByNumber(lineNumber);
        return doc.Document.GetText(line.Offset, line.Length);
    }

    public string GetText(int offset, int length) => doc.Document.GetText(offset, length);
    public void Insert(int offset, string text) => doc.Document.Insert(offset, text);
    public void Replace(int offset, int length, string text) => doc.Document.Replace(offset, length, text);
    public void Remove(int offset, int length) => doc.Document.Remove(offset, length);

    public void Select(int start, int length) => doc.EditorHandle?.SelectAndReveal(start, length);
    public void GoToLine(int lineNumber) => doc.RequestGoToLine(lineNumber);
}

/// <summary>
/// The <see cref="IPluginHost"/> implementation handed to every plugin. Plugin callbacks
/// are invoked from the UI thread, so plugins can touch documents directly.
/// </summary>
public sealed class PluginHost(MainViewModel main) : IPluginHost
{
    private readonly Dictionary<DocumentViewModel, EditorDocumentAdapter> _adapters = [];
    private readonly List<string> _log = [];

    /// <summary>Set by the loader so RegisterCommand can attribute commands to the right plugin.</summary>
    internal string CurrentPluginName { get; set; } = "Plugin";

    public List<PluginCommand> Commands { get; } = [];

    public IReadOnlyList<string> LogLines => _log;

    /// <summary>Raised when a plugin adds a command, so the Plugins menu can rebuild.</summary>
    public event EventHandler? CommandsChanged;

    public event EventHandler<DocumentEventArgs>? DocumentOpened;
    public event EventHandler<DocumentEventArgs>? DocumentSaved;
    public event EventHandler<DocumentEventArgs>? ActiveDocumentChanged;

    internal IEditorDocument Wrap(DocumentViewModel doc)
    {
        if (!_adapters.TryGetValue(doc, out var adapter))
            _adapters[doc] = adapter = new EditorDocumentAdapter(doc);
        return adapter;
    }

    public IEditorDocument? ActiveDocument
        => main.SelectedDocument is { } doc ? Wrap(doc) : null;

    public IReadOnlyList<IEditorDocument> Documents
        => main.Documents.Select(Wrap).ToList();

    public void RegisterCommand(string title, Action action)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Command title is required.", nameof(title));
        Commands.Add(new PluginCommand(CurrentPluginName, title, action));
        CommandsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ShowMessage(string title, string message) => main.ShowPluginMessage(title, message);

    public void Log(string message)
    {
        _log.Add($"{DateTime.Now:HH:mm:ss}  [{CurrentPluginName}] {message}");
        if (_log.Count > 500)
            _log.RemoveAt(0);
    }

    // ---- Event pumps, called by MainViewModel ----

    internal void RaiseDocumentOpened(DocumentViewModel doc)
        => DocumentOpened?.Invoke(this, new DocumentEventArgs(Wrap(doc)));

    internal void RaiseDocumentSaved(DocumentViewModel doc)
        => DocumentSaved?.Invoke(this, new DocumentEventArgs(Wrap(doc)));

    internal void RaiseActiveDocumentChanged(DocumentViewModel doc)
        => ActiveDocumentChanged?.Invoke(this, new DocumentEventArgs(Wrap(doc)));

    /// <summary>Runs a plugin command, surfacing failures in the log instead of crashing.</summary>
    public void Invoke(PluginCommand command)
    {
        try
        {
            command.Action();
        }
        catch (Exception ex)
        {
            _log.Add($"{DateTime.Now:HH:mm:ss}  [{command.PluginName}] '{command.Title}' failed — {ex.GetType().Name}: {ex.Message}");
            main.ShowPluginMessage("Plugin error",
                $"{command.PluginName}: '{command.Title}' failed.\n\n{ex.Message}");
        }
    }
}
