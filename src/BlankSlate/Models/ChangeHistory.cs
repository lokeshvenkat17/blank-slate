using System;
using System.Collections.Generic;
using System.Linq;
using AvaloniaEdit.Document;

namespace BlankSlate.Models;

public enum ChangeState { None, Modified, Saved }

/// <summary>
/// Notepad++'s change-history gutter: tracks which lines were edited since the file was
/// opened (orange) and which of those have since been saved (green). Ranges are held in
/// <see cref="TextSegmentCollection{T}"/> so they follow the text through later edits.
/// </summary>
public sealed class ChangeHistory
{
    private readonly TextDocument _document;
    private readonly TextSegmentCollection<TextSegment> _modified;
    private readonly TextSegmentCollection<TextSegment> _saved;
    private bool _suppress;

    public event EventHandler? Changed;

    public ChangeHistory(TextDocument document)
    {
        _document = document;
        _modified = new TextSegmentCollection<TextSegment>(document);
        _saved = new TextSegmentCollection<TextSegment>(document);
        document.Changed += OnDocumentChanged;
    }

    /// <summary>Suppresses tracking while the document is being loaded or replaced wholesale.</summary>
    public IDisposable SuppressTracking()
    {
        _suppress = true;
        return new Resumer(this);
    }

    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e)
    {
        if (_suppress)
            return;
        // Record the inserted range; deletions mark the line where text was removed.
        var length = Math.Max(e.InsertionLength, 1);
        var offset = Math.Min(e.Offset, Math.Max(0, _document.TextLength - 1));
        if (offset + length > _document.TextLength)
            length = Math.Max(0, _document.TextLength - offset);
        if (length <= 0)
            return;

        _modified.Add(new TextSegment { StartOffset = offset, Length = length });
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Called after a successful save: modified lines become "saved" markers.</summary>
    public void MarkSaved()
    {
        foreach (var segment in _modified.ToList())
        {
            _saved.Add(new TextSegment { StartOffset = segment.StartOffset, Length = segment.Length });
            _modified.Remove(segment);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        foreach (var s in _modified.ToList()) _modified.Remove(s);
        foreach (var s in _saved.ToList()) _saved.Remove(s);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Change state of a 1-based line.</summary>
    public ChangeState GetLineState(int lineNumber)
    {
        if (lineNumber < 1 || lineNumber > _document.LineCount)
            return ChangeState.None;
        var line = _document.GetLineByNumber(lineNumber);
        var length = Math.Max(line.Length, 1);
        if (_modified.FindOverlappingSegments(line.Offset, length).Count > 0)
            return ChangeState.Modified;
        if (_saved.FindOverlappingSegments(line.Offset, length).Count > 0)
            return ChangeState.Saved;
        return ChangeState.None;
    }

    /// <summary>All 1-based line numbers with any change marker, ascending.</summary>
    public IReadOnlyList<int> ChangedLines()
    {
        var lines = new SortedSet<int>();
        foreach (var segment in _modified.Concat(_saved))
        {
            if (segment.StartOffset > _document.TextLength)
                continue;
            var first = _document.GetLineByOffset(Math.Min(segment.StartOffset, _document.TextLength)).LineNumber;
            var last = _document.GetLineByOffset(Math.Min(segment.EndOffset, _document.TextLength)).LineNumber;
            for (var l = first; l <= last; l++)
                lines.Add(l);
        }
        return lines.ToList();
    }

    public int? NextChange(int fromLine)
    {
        var lines = ChangedLines();
        if (lines.Count == 0)
            return null;
        return lines.FirstOrDefault(l => l > fromLine, lines[0]);
    }

    public int? PreviousChange(int fromLine)
    {
        var lines = ChangedLines();
        if (lines.Count == 0)
            return null;
        return lines.LastOrDefault(l => l < fromLine, lines[^1]);
    }

    private sealed class Resumer(ChangeHistory owner) : IDisposable
    {
        public void Dispose() => owner._suppress = false;
    }
}
