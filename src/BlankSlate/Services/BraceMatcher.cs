namespace BlankSlate.Services;

/// <summary>
/// Bracket matching for Search &gt; Go to Matching Brace and Select All In-between.
/// Pure text logic so it can be tested without an editor.
/// </summary>
public static class BraceMatcher
{
    private const string Openers = "([{";
    private const string Closers = ")]}";

    /// <summary>
    /// Finds the brace paired with the one at or just before <paramref name="offset"/>.
    /// Returns (bracePosition, matchPosition), or null when the caret isn't on a brace
    /// or the brace is unbalanced.
    /// </summary>
    public static (int Brace, int Match)? FindMatch(string text, int offset)
    {
        if (text.Length == 0)
            return null;

        // Prefer the character at the caret, then the one just before it (Notepad++ behaviour).
        foreach (var pos in new[] { offset, offset - 1 })
        {
            if (pos < 0 || pos >= text.Length)
                continue;
            var c = text[pos];
            var openIndex = Openers.IndexOf(c);
            if (openIndex >= 0)
            {
                var match = ScanForward(text, pos, c, Closers[openIndex]);
                if (match is { } m)
                    return (pos, m);
                return null;
            }
            var closeIndex = Closers.IndexOf(c);
            if (closeIndex >= 0)
            {
                var match = ScanBackward(text, pos, Openers[closeIndex], c);
                if (match is { } m)
                    return (pos, m);
                return null;
            }
        }
        return null;
    }

    /// <summary>Range strictly between a matched pair, for "Select All In-between".</summary>
    public static (int Start, int Length)? FindInnerRange(string text, int offset)
    {
        if (FindMatch(text, offset) is not { } pair)
            return null;
        var (a, b) = pair.Brace < pair.Match ? (pair.Brace, pair.Match) : (pair.Match, pair.Brace);
        var start = a + 1;
        var length = b - start;
        return length < 0 ? null : (start, length);
    }

    private static int? ScanForward(string text, int start, char open, char close)
    {
        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == open) depth++;
            else if (text[i] == close)
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }
        return null;
    }

    private static int? ScanBackward(string text, int start, char open, char close)
    {
        var depth = 0;
        for (var i = start; i >= 0; i--)
        {
            if (text[i] == close) depth++;
            else if (text[i] == open)
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }
        return null;
    }
}
