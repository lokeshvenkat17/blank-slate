using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BlankSlate.Models;

namespace BlankSlate.Services;

/// <summary>Pure text transformations behind the Edit menu (case, sort, line and blank operations).</summary>
public static class TextOperations
{
    // ---- Case conversion ----

    public static string ConvertCase(string text, CaseKind kind) => kind switch
    {
        CaseKind.Upper => text.ToUpperInvariant(),
        CaseKind.Lower => text.ToLowerInvariant(),
        CaseKind.ProperForce => ProperCase(text, blend: false),
        CaseKind.ProperBlend => ProperCase(text, blend: true),
        CaseKind.SentenceForce => SentenceCase(text, blend: false),
        CaseKind.SentenceBlend => SentenceCase(text, blend: true),
        CaseKind.Invert => InvertCase(text),
        CaseKind.Random => RandomCase(text),
        _ => text,
    };

    private static string ProperCase(string text, bool blend)
    {
        var sb = new StringBuilder(text.Length);
        var atWordStart = true;
        foreach (var c in text)
        {
            if (char.IsLetter(c))
            {
                sb.Append(atWordStart ? char.ToUpperInvariant(c) : blend ? c : char.ToLowerInvariant(c));
                atWordStart = false;
            }
            else
            {
                sb.Append(c);
                atWordStart = true;
            }
        }
        return sb.ToString();
    }

    private static string SentenceCase(string text, bool blend)
    {
        var sb = new StringBuilder(text.Length);
        var atSentenceStart = true;
        foreach (var c in text)
        {
            if (char.IsLetter(c))
            {
                sb.Append(atSentenceStart ? char.ToUpperInvariant(c) : blend ? c : char.ToLowerInvariant(c));
                atSentenceStart = false;
            }
            else
            {
                sb.Append(c);
                if (c is '.' or '!' or '?' or '\n')
                    atSentenceStart = true;
            }
        }
        return sb.ToString();
    }

    private static string InvertCase(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
            sb.Append(char.IsUpper(c) ? char.ToLowerInvariant(c) : char.IsLower(c) ? char.ToUpperInvariant(c) : c);
        return sb.ToString();
    }

    private static string RandomCase(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
            sb.Append(Random.Shared.Next(2) == 0 ? char.ToLowerInvariant(c) : char.ToUpperInvariant(c));
        return sb.ToString();
    }

    // ---- Sorting ----

    public static List<string> SortLines(IReadOnlyList<string> lines, SortKind kind)
    {
        var descending = kind is SortKind.LexDesc or SortKind.LexCiDesc or SortKind.LocaleDesc
            or SortKind.IntDesc or SortKind.DecCommaDesc or SortKind.DecDotDesc or SortKind.LenDesc;

        IEnumerable<string> sorted = kind switch
        {
            SortKind.LexAsc or SortKind.LexDesc => lines.OrderBy(l => l, StringComparer.Ordinal),
            SortKind.LexCiAsc or SortKind.LexCiDesc => lines.OrderBy(l => l, StringComparer.OrdinalIgnoreCase),
            SortKind.LocaleAsc or SortKind.LocaleDesc => lines.OrderBy(l => l, StringComparer.CurrentCulture),
            SortKind.IntAsc or SortKind.IntDesc => lines
                .OrderBy(l => long.TryParse(l.Trim(), out _) ? 0 : 1)
                .ThenBy(l => long.TryParse(l.Trim(), out var v) ? v : 0),
            SortKind.DecCommaAsc or SortKind.DecCommaDesc => OrderByDecimal(lines, ','),
            SortKind.DecDotAsc or SortKind.DecDotDesc => OrderByDecimal(lines, '.'),
            SortKind.LenAsc or SortKind.LenDesc => lines.OrderBy(l => l.Length),
            _ => lines,
        };

        var result = sorted.ToList();
        if (descending)
            result.Reverse();
        return result;
    }

    private static IEnumerable<string> OrderByDecimal(IReadOnlyList<string> lines, char decimalSeparator)
    {
        var culture = CultureInfo.InvariantCulture;
        return lines
            .OrderBy(l => TryParseDecimal(l, decimalSeparator, culture, out _) ? 0 : 1)
            .ThenBy(l => TryParseDecimal(l, decimalSeparator, culture, out var v) ? v : 0m);
    }

    private static bool TryParseDecimal(string line, char decimalSeparator, CultureInfo culture, out decimal value)
    {
        var normalized = decimalSeparator == ',' ? line.Trim().Replace(',', '.') : line.Trim();
        return decimal.TryParse(normalized, NumberStyles.Number, culture, out value);
    }

    // ---- Line operations on a list of lines ----

    public static List<string> ApplyLineOp(IReadOnlyList<string> lines, LineOpKind kind) => kind switch
    {
        LineOpKind.RemoveDuplicates => RemoveDuplicates(lines, consecutiveOnly: false),
        LineOpKind.RemoveConsecutiveDuplicates => RemoveDuplicates(lines, consecutiveOnly: true),
        LineOpKind.RemoveEmpty => lines.Where(l => l.Length > 0).ToList(),
        LineOpKind.RemoveEmptyWithBlank => lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList(),
        LineOpKind.Reverse => lines.Reverse().ToList(),
        LineOpKind.Randomize => Shuffle(lines),
        _ => lines.ToList(),
    };

    private static List<string> RemoveDuplicates(IReadOnlyList<string> lines, bool consecutiveOnly)
    {
        var result = new List<string>();
        var seen = new HashSet<string>();
        string? previous = null;
        foreach (var line in lines)
        {
            if (consecutiveOnly ? line == previous : !seen.Add(line))
            {
                previous = line;
                continue;
            }
            previous = line;
            result.Add(line);
        }
        return result;
    }

    private static List<string> Shuffle(IReadOnlyList<string> lines)
    {
        var result = lines.ToList();
        for (var i = result.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }
        return result;
    }

    // ---- Blank operations on whole text ----

    public static string ApplyBlankOp(string text, BlankOpKind kind, int tabWidth = 4) => kind switch
    {
        BlankOpKind.TrimTrailing => Regex.Replace(text, @"[ \t]+(?=\r?\n|$)", ""),
        BlankOpKind.TrimLeading => Regex.Replace(text, @"(?m)^[ \t]+", ""),
        BlankOpKind.TrimBoth => ApplyBlankOp(ApplyBlankOp(text, BlankOpKind.TrimTrailing), BlankOpKind.TrimLeading),
        BlankOpKind.EolToSpace => Regex.Replace(text, @"\r\n|\r|\n", " "),
        BlankOpKind.TrimAll => ApplyBlankOp(ApplyBlankOp(text, BlankOpKind.TrimBoth), BlankOpKind.EolToSpace),
        BlankOpKind.TabToSpace => text.Replace("\t", new string(' ', tabWidth)),
        BlankOpKind.SpaceToTabAll => Regex.Replace(text, $"[ ]{{{tabWidth}}}", "\t"),
        BlankOpKind.SpaceToTabLeading => Regex.Replace(text, @"(?m)^[ ]+",
            m => new string('\t', m.Length / tabWidth) + new string(' ', m.Length % tabWidth)),
        _ => text,
    };

    // ---- Line comments ----

    /// <summary>True when every non-blank line starts (after indentation) with <paramref name="token"/>.</summary>
    public static bool AllLinesCommented(IReadOnlyList<string> lines, string token)
        => lines.Where(l => !string.IsNullOrWhiteSpace(l))
            .All(l => l.TrimStart().StartsWith(token, StringComparison.Ordinal));

    public static List<string> CommentLines(IReadOnlyList<string> lines, string token)
        => lines.Select(l => string.IsNullOrWhiteSpace(l) ? l : InsertAfterIndent(l, token + " ")).ToList();

    public static List<string> UncommentLines(IReadOnlyList<string> lines, string token)
        => lines.Select(l =>
        {
            var trimmed = l.TrimStart();
            if (!trimmed.StartsWith(token, StringComparison.Ordinal))
                return l;
            var indent = l[..(l.Length - trimmed.Length)];
            var rest = trimmed[token.Length..];
            if (rest.StartsWith(' '))
                rest = rest[1..];
            return indent + rest;
        }).ToList();

    private static string InsertAfterIndent(string line, string insert)
    {
        var trimmed = line.TrimStart();
        var indent = line[..(line.Length - trimmed.Length)];
        return indent + insert + trimmed;
    }

    // ---- Indent ----

    public static List<string> Indent(IReadOnlyList<string> lines, bool increase, int tabWidth = 4)
        => increase
            ? lines.Select(l => l.Length == 0 ? l : "\t" + l).ToList()
            : lines.Select(l =>
            {
                if (l.StartsWith('\t'))
                    return l[1..];
                var spaces = 0;
                while (spaces < l.Length && spaces < tabWidth && l[spaces] == ' ')
                    spaces++;
                return l[spaces..];
            }).ToList();
}
