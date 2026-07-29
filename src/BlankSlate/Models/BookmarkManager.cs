using System;
using System.Collections.Generic;
using System.Linq;
using AvaloniaEdit.Document;

namespace BlankSlate.Models;

/// <summary>
/// Per-document bookmarks, stored as <see cref="TextAnchor"/>s so they follow
/// their lines through edits (an anchor on a deleted line disappears with it).
/// </summary>
public sealed class BookmarkManager(TextDocument document)
{
    private readonly List<TextAnchor> _anchors = [];

    public event EventHandler? Changed;

    /// <summary>Bookmarked line numbers, ascending, without duplicates.</summary>
    public IReadOnlyList<int> Lines
    {
        get
        {
            Prune();
            return _anchors.Select(a => a.Line).Distinct().OrderBy(l => l).ToList();
        }
    }

    public bool Contains(int line)
    {
        Prune();
        return _anchors.Any(a => a.Line == line);
    }

    public void Toggle(int line)
    {
        Prune();
        var existing = _anchors.Where(a => a.Line == line).ToList();
        if (existing.Count > 0)
        {
            foreach (var a in existing)
                _anchors.Remove(a);
        }
        else
        {
            var anchor = document.CreateAnchor(document.GetLineByNumber(line).Offset);
            anchor.SurviveDeletion = false;
            _anchors.Add(anchor);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Next bookmarked line after <paramref name="fromLine"/>, wrapping to the first.</summary>
    public int? Next(int fromLine)
    {
        var lines = Lines;
        if (lines.Count == 0)
            return null;
        return lines.FirstOrDefault(l => l > fromLine, lines[0]);
    }

    /// <summary>Previous bookmarked line before <paramref name="fromLine"/>, wrapping to the last.</summary>
    public int? Previous(int fromLine)
    {
        var lines = Lines;
        if (lines.Count == 0)
            return null;
        return lines.LastOrDefault(l => l < fromLine, lines[^1]);
    }

    public void Clear()
    {
        _anchors.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Bookmarks every non-bookmarked line and vice versa (Notepad++ "Inverse Bookmarks").</summary>
    public void Inverse()
    {
        var bookmarked = Lines.ToHashSet();
        _anchors.Clear();
        for (var line = 1; line <= document.LineCount; line++)
        {
            if (bookmarked.Contains(line))
                continue;
            var anchor = document.CreateAnchor(document.GetLineByNumber(line).Offset);
            anchor.SurviveDeletion = false;
            _anchors.Add(anchor);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Drops anchors whose lines were deleted from under them.</summary>
    private void Prune() => _anchors.RemoveAll(a => a.IsDeleted);

    public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
