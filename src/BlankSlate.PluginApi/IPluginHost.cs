namespace BlankSlate.Plugins;

/// <summary>The editor services available to a plugin.</summary>
public interface IPluginHost
{
    /// <summary>The document in the active tab, or null when none is focused.</summary>
    IEditorDocument? ActiveDocument { get; }

    /// <summary>Every open document, in tab order.</summary>
    IReadOnlyList<IEditorDocument> Documents { get; }

    /// <summary>
    /// Adds an entry under the Plugins menu, grouped in a submenu named after the plugin.
    /// Call during <see cref="IPlugin.Initialize"/>.
    /// </summary>
    void RegisterCommand(string title, Action action);

    /// <summary>Shows a simple informational dialog.</summary>
    void ShowMessage(string title, string message);

    /// <summary>Writes a line to the plugin log surfaced in the Plugin Manager.</summary>
    void Log(string message);

    /// <summary>Raised after a document is opened from disk or created.</summary>
    event EventHandler<DocumentEventArgs>? DocumentOpened;

    /// <summary>Raised after a document is written to disk.</summary>
    event EventHandler<DocumentEventArgs>? DocumentSaved;

    /// <summary>Raised when the active tab changes.</summary>
    event EventHandler<DocumentEventArgs>? ActiveDocumentChanged;
}

public sealed class DocumentEventArgs(IEditorDocument document) : EventArgs
{
    public IEditorDocument Document { get; } = document;
}
