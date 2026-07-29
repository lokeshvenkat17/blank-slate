using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BlankSlate.Models;

namespace BlankSlate.Services;

/// <summary>Pure search/replace logic shared by the Find dialog, Find in Files, and Mark All.</summary>
public static class SearchService
{
    /// <summary>Compiles a <see cref="SearchQuery"/> into a regex. Throws <see cref="ArgumentException"/> on invalid regex patterns.</summary>
    public static Regex BuildRegex(SearchQuery query)
    {
        var pattern = query.Mode switch
        {
            SearchMode.Regex => query.Pattern,
            SearchMode.Extended => Regex.Escape(UnescapeExtended(query.Pattern)),
            _ => Regex.Escape(query.Pattern),
        };
        if (query.WholeWord)
            pattern = $@"\b(?:{pattern})\b";
        var options = RegexOptions.Multiline;
        if (!query.MatchCase)
            options |= RegexOptions.IgnoreCase;
        return new Regex(pattern, options, TimeSpan.FromSeconds(5));
    }

    /// <summary>Finds the next match from <paramref name="startOffset"/>, honoring direction and wrap-around. Null when no match.</summary>
    public static Match? FindNext(string text, SearchQuery query, int startOffset, bool backward)
    {
        if (query.Pattern.Length == 0)
            return null;
        var regex = BuildRegex(query);

        if (!backward)
        {
            var m = regex.Match(text, Math.Min(startOffset, text.Length));
            if (!m.Success && query.WrapAround)
                m = regex.Match(text);
            return m.Success ? m : null;
        }

        Match? lastBefore = null, lastOverall = null;
        for (var m = regex.Match(text); m.Success; m = m.NextMatch())
        {
            if (m.Index + m.Length <= startOffset)
                lastBefore = m;
            lastOverall = m;
            if (m.Index >= startOffset && !query.WrapAround)
                break;
        }
        return lastBefore ?? (query.WrapAround ? lastOverall : null);
    }

    /// <summary>Computes the replacement text for one match (regex mode supports $1 group substitutions).</summary>
    public static string GetReplacement(Match match, string replaceWith, SearchMode mode) => mode switch
    {
        SearchMode.Regex => match.Result(UnescapeExtended(replaceWith)),
        SearchMode.Extended => UnescapeExtended(replaceWith),
        _ => replaceWith,
    };

    /// <summary>Replaces every match in <paramref name="text"/>; returns the new text and replacement count.</summary>
    public static (string Text, int Count) ReplaceAll(string text, SearchQuery query, string replaceWith)
    {
        if (query.Pattern.Length == 0)
            return (text, 0);
        var regex = BuildRegex(query);
        var count = 0;
        var result = regex.Replace(text, m =>
        {
            count++;
            return GetReplacement(m, replaceWith, query.Mode);
        });
        return (result, count);
    }

    public static int Count(string text, SearchQuery query)
        => query.Pattern.Length == 0 ? 0 : BuildRegex(query).Matches(text).Count;

    /// <summary>Interprets Extended-mode escapes: \n \r \t \0 \\ \xHH \uHHHH.</summary>
    public static string UnescapeExtended(string input)
    {
        var sb = new StringBuilder(input.Length);
        for (var i = 0; i < input.Length; i++)
        {
            if (input[i] != '\\' || i + 1 >= input.Length)
            {
                sb.Append(input[i]);
                continue;
            }
            i++;
            switch (input[i])
            {
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case '0': sb.Append('\0'); break;
                case '\\': sb.Append('\\'); break;
                case 'x' when i + 2 < input.Length
                    && int.TryParse(input.AsSpan(i + 1, 2), NumberStyles.HexNumber, null, out var hx):
                    sb.Append((char)hx);
                    i += 2;
                    break;
                case 'u' when i + 4 < input.Length
                    && int.TryParse(input.AsSpan(i + 1, 4), NumberStyles.HexNumber, null, out var hu):
                    sb.Append((char)hu);
                    i += 4;
                    break;
                default:
                    sb.Append('\\').Append(input[i]);
                    break;
            }
        }
        return sb.ToString();
    }
}
