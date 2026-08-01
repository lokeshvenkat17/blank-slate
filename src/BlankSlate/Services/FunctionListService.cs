using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace BlankSlate.Services;

public sealed record FunctionEntry(string Name, int Line);

/// <summary>
/// Regex-based function/class discovery for the Function List panel,
/// analogous to Notepad++'s functionList parsers.
/// </summary>
public static class FunctionListService
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    // One pattern per language family; group "name" captures the symbol.
    private static readonly Dictionary<string, Regex> Patterns = new()
    {
        ["python"] = Make(@"^[ \t]*(?:async[ \t]+)?(?:def|class)[ \t]+(?<name>\w+)"),
        ["ruby"] = Make(@"^[ \t]*(?:def[ \t]+(?<name>[\w.?!]+)|class[ \t]+(?<name>\w+)|module[ \t]+(?<name>\w+))"),
        ["go"] = Make(@"^func[ \t]+(?:\([^)]*\)[ \t]*)?(?<name>\w+)"),
        ["rust"] = Make(@"^[ \t]*(?:pub[ \t(][^)]*\)?[ \t]*)?fn[ \t]+(?<name>\w+)"),
        ["javascript"] = Make(@"^[ \t]*(?:export[ \t]+)?(?:default[ \t]+)?(?:async[ \t]+)?function[ \t*]*(?<name>\w+)|^[ \t]*(?:export[ \t]+)?class[ \t]+(?<name>\w+)|^[ \t]*(?:const|let|var)[ \t]+(?<name>\w+)[ \t]*=[ \t]*(?:async[ \t]*)?(?:\([^)]*\)|\w+)[ \t]*=>"),
        ["php"] = Make(@"^[ \t]*(?:(?:public|private|protected|static|abstract|final)[ \t]+)*function[ \t]+(?<name>\w+)|^[ \t]*class[ \t]+(?<name>\w+)"),
        ["shellscript"] = Make(@"^[ \t]*(?:function[ \t]+(?<name>\w+)|(?<name>[\w-]+)[ \t]*\(\)[ \t]*\{?)"),
        ["lua"] = Make(@"^[ \t]*(?:local[ \t]+)?function[ \t]+(?<name>[\w.:]+)"),
        ["perl"] = Make(@"^[ \t]*sub[ \t]+(?<name>\w+)"),
        ["r"] = Make(@"^[ \t]*(?<name>[\w.]+)[ \t]*(?:<-|=)[ \t]*function"),
        ["swift"] = Make(@"^[ \t]*(?:(?:public|private|internal|open|static|override|final)[ \t]+)*(?:func[ \t]+(?<name>\w+)|class[ \t]+(?<name>\w+)|struct[ \t]+(?<name>\w+)|enum[ \t]+(?<name>\w+))"),
    };

    // C-family heuristic: a word followed by (args) then { on the same or next line, not a control keyword.
    private static readonly Regex CFamily = Make(
        @"^[ \t]*(?:[\w:<>\[\],~&*\t ]+?[ \t&*])??(?<name>[\w~]+)[ \t]*\([^;)]*\)[^;{]*\{");

    private static readonly HashSet<string> CFamilyLanguages =
        ["c", "cpp", "csharp", "java", "kotlin", "scala", "dart", "objective-c", "groovy", "typescript", "cuda-cpp"];

    private static readonly HashSet<string> ControlKeywords =
        ["if", "else", "for", "foreach", "while", "switch", "catch", "using", "lock", "return", "new", "do", "try", "get", "set"];

    private static Regex Make(string pattern) => new(pattern, RegexOptions.Multiline, Timeout);

    public static List<FunctionEntry> GetFunctions(string? languageId, string text)
    {
        var results = new List<FunctionEntry>();
        if (languageId is null || text.Length == 0)
            return results;

        Regex? regex = null;
        if (Patterns.TryGetValue(languageId, out var specific))
            regex = specific;
        else if (CFamilyLanguages.Contains(languageId))
            regex = CFamily;
        if (regex is null)
            return results;

        try
        {
            foreach (Match match in regex.Matches(text))
            {
                var name = match.Groups["name"].Value;
                if (name.Length == 0 || ControlKeywords.Contains(name))
                    continue;
                var line = CountLines(text, match.Groups["name"].Index);
                results.Add(new FunctionEntry(name, line));
                if (results.Count >= 2000)
                    break;
            }
        }
        catch (RegexMatchTimeoutException) { /* huge file — return what we have */ }
        return results;
    }

    private static int CountLines(string text, int offset)
    {
        var line = 1;
        for (var i = 0; i < offset; i++)
        {
            if (text[i] == '\n')
                line++;
        }
        return line;
    }
}
