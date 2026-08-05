namespace BlankSlate.Plugins;

/// <summary>A document open in a BlankSlate tab.</summary>
public interface IEditorDocument
{
    /// <summary>Full path on disk, or null for an unsaved document.</summary>
    string? FilePath { get; }

    /// <summary>Tab title (file name, or "new N" when unsaved).</summary>
    string Title { get; }

    /// <summary>TextMate language id (e.g. "csharp"), or null for plain text.</summary>
    string? LanguageId { get; }

    /// <summary>True when there are unsaved changes.</summary>
    bool IsDirty { get; }

    /// <summary>The whole buffer. Assigning replaces all text in one undo step.</summary>
    string Text { get; set; }

    int TextLength { get; }
    int LineCount { get; }

    /// <summary>1-based caret line.</summary>
    int CaretLine { get; }

    /// <summary>Caret position as a character offset from the start of the buffer.</summary>
    int CaretOffset { get; set; }

    /// <summary>Currently selected text ("" when the selection is empty).</summary>
    string SelectedText { get; }

    int SelectionStart { get; }
    int SelectionLength { get; }

    /// <summary>Text of a 1-based line, without its line terminator.</summary>
    string GetLineText(int lineNumber);

    string GetText(int offset, int length);
    void Insert(int offset, string text);
    void Replace(int offset, int length, string text);
    void Remove(int offset, int length);

    /// <summary>Selects a range and scrolls it into view.</summary>
    void Select(int start, int length);

    /// <summary>Moves the caret to a 1-based line and scrolls to it.</summary>
    void GoToLine(int lineNumber);
}
