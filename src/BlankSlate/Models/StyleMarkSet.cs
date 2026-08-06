using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AvaloniaEdit.Document;

namespace BlankSlate.Models;

/// <summary>
/// Notepad++'s "Style All Occurrences of Token" marks: five independent highlight
/// styles per document. Ranges live in <see cref="TextSegmentCollection{T}"/>, which
/// rewrites offsets as the document changes, so marks stay on their text through edits.
/// </summary>
public sealed class StyleMarkSet
{
    public const int StyleCount = 5;

    private readonly TextDocument _document;
    private readonly TextSegmentCollection<TextSegment>[] _styles;

    public event EventHandler? Changed;

    public StyleMarkSet(TextDocument document)
    {
        _document = document;
        _styles = new TextSegmentCollection<TextSegment>[StyleCount];
        for (var i = 0; i < StyleCount; i++)
            _styles[i] = new TextSegmentCollection<TextSegment>(document);
    }

    public IEnumerable<TextSegment> GetSegments(int style)
        => IsValid(style) ? _styles[style] : [];

    public IEnumerable<TextSegment> GetOverlapping(int style, int offset, int length)
        => IsValid(style) ? _styles[style].FindOverlappingSegments(offset, length) : [];

    public bool HasAnyMarks => _styles.Any(s => s.Count > 0);

    /// <summary>Styles every occurrence of <paramref name="token"/> in the document.</summary>
    public int MarkAll(string token, int style, bool matchCase = true, bool wholeWord = true)
    {
        if (!IsValid(style) || string.IsNullOrEmpty(token))
            return 0;

        var pattern = Regex.Escape(token);
        if (wholeWord)
            pattern = $@"\b{pattern}\b";
        var options = matchCase ? RegexOptions.None : RegexOptions.IgnoreCase;

        var count = 0;
        foreach (Match match in Regex.Matches(_document.Text, pattern, options, TimeSpan.FromSeconds(5)))
        {
            AddSegment(style, match.Index, match.Length);
            count++;
        }
        if (count > 0)
            Changed?.Invoke(this, EventArgs.Empty);
        return count;
    }

    /// <summary>Styles a single range ("Style One Token").</summary>
    public void MarkOne(int offset, int length, int style)
    {
        if (!IsValid(style) || length <= 0)
            return;
        AddSegment(style, offset, length);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void AddSegment(int style, int offset, int length)
    {
        // Don't stack duplicates on the same range.
        if (_styles[style].FindOverlappingSegments(offset, length)
            .Any(s => s.StartOffset == offset && s.Length == length))
            return;
        _styles[style].Add(new TextSegment { StartOffset = offset, Length = length });
    }

    public void Clear(int style)
    {
        if (!IsValid(style))
            return;
        foreach (var segment in _styles[style].ToList())
            _styles[style].Remove(segment);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ClearAll()
    {
        for (var i = 0; i < StyleCount; i++)
        {
            foreach (var segment in _styles[i].ToList())
                _styles[i].Remove(segment);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Offset of the next mark after <paramref name="offset"/>, wrapping to the first.</summary>
    public int? NextMark(int offset, int style)
    {
        if (!IsValid(style))
            return null;
        var ordered = _styles[style].OrderBy(s => s.StartOffset).ToList();
        if (ordered.Count == 0)
            return null;
        return ordered.FirstOrDefault(s => s.StartOffset > offset)?.StartOffset ?? ordered[0].StartOffset;
    }

    /// <summary>Offset of the previous mark before <paramref name="offset"/>, wrapping to the last.</summary>
    public int? PreviousMark(int offset, int style)
    {
        if (!IsValid(style))
            return null;
        var ordered = _styles[style].OrderBy(s => s.StartOffset).ToList();
        if (ordered.Count == 0)
            return null;
        return ordered.LastOrDefault(s => s.StartOffset < offset)?.StartOffset ?? ordered[^1].StartOffset;
    }

    /// <summary>Concatenates the text of every mark in a style ("Copy Styled Text").</summary>
    public string GetStyledText(int style)
    {
        if (!IsValid(style))
            return "";
        var sb = new StringBuilder();
        foreach (var segment in _styles[style].OrderBy(s => s.StartOffset))
        {
            var end = Math.Min(segment.EndOffset, _document.TextLength);
            if (segment.StartOffset < end)
                sb.AppendLine(_document.GetText(segment.StartOffset, end - segment.StartOffset));
        }
        return sb.ToString();
    }

    public string GetAllStyledText()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < StyleCount; i++)
            sb.Append(GetStyledText(i));
        return sb.ToString();
    }

    /// <summary>The word under <paramref name="offset"/>, used when nothing is selected.</summary>
    public static (int Offset, int Length)? GetWordAt(TextDocument document, int offset)
    {
        if (document.TextLength == 0)
            return null;
        offset = Math.Clamp(offset, 0, document.TextLength - 1);
        var text = document.Text;
        if (!IsWordChar(text[offset]))
        {
            if (offset == 0 || !IsWordChar(text[offset - 1]))
                return null;
            offset--;
        }
        var start = offset;
        while (start > 0 && IsWordChar(text[start - 1]))
            start--;
        var end = offset;
        while (end + 1 < text.Length && IsWordChar(text[end + 1]))
            end++;
        return (start, end - start + 1);
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static bool IsValid(int style) => style is >= 0 and < StyleCount;
}
