using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BlankSlate.Services;

/// <summary>
/// Notepad++'s word completion: suggests words already present in the document.
/// Pure logic so it can be tested without an editor.
/// </summary>
public static class WordCompletion
{
    private const int MinWordLength = 3;
    private const int MaxSuggestions = 50;

    private static readonly Regex WordPattern = new(@"[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled);

    /// <summary>The partial word immediately before <paramref name="offset"/>.</summary>
    public static string GetPrefix(string text, int offset)
    {
        offset = Math.Clamp(offset, 0, text.Length);
        var start = offset;
        while (start > 0 && IsWordChar(text[start - 1]))
            start--;
        return text[start..offset];
    }

    /// <summary>
    /// Words in the document that start with <paramref name="prefix"/>, excluding the
    /// prefix itself, ordered by frequency then alphabetically.
    /// </summary>
    public static IReadOnlyList<string> GetSuggestions(string text, string prefix)
    {
        if (prefix.Length == 0)
            return [];

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Match match in WordPattern.Matches(text))
        {
            var word = match.Value;
            if (word.Length < MinWordLength
                || word.Equals(prefix, StringComparison.Ordinal)
                || !word.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            counts[word] = counts.GetValueOrDefault(word) + 1;
        }

        return counts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Take(MaxSuggestions)
            .Select(kv => kv.Key)
            .ToList();
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
