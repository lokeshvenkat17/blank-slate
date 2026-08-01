using System.Collections.Generic;

namespace BlankSlate.Models;

public sealed record CommentTokens(string? Line, string? BlockStart, string? BlockEnd);

/// <summary>Comment tokens per TextMate language id, used by Edit &gt; Comment/Uncomment.</summary>
public static class CommentDefinitions
{
    private static readonly CommentTokens CStyle = new("//", "/*", "*/");
    private static readonly CommentTokens Hash = new("#", null, null);

    private static readonly Dictionary<string, CommentTokens> ByLanguage = new()
    {
        ["c"] = CStyle, ["cpp"] = CStyle, ["csharp"] = CStyle, ["java"] = CStyle,
        ["javascript"] = CStyle, ["typescript"] = CStyle, ["go"] = CStyle, ["rust"] = CStyle,
        ["swift"] = CStyle, ["kotlin"] = CStyle, ["scala"] = CStyle, ["dart"] = CStyle,
        ["objective-c"] = CStyle, ["cuda-cpp"] = CStyle, ["groovy"] = CStyle, ["php"] = CStyle,
        ["fsharp"] = new CommentTokens("//", "(*", "*)"),
        ["python"] = Hash, ["ruby"] = new CommentTokens("#", "=begin", "=end"),
        ["shellscript"] = Hash, ["perl"] = Hash, ["r"] = Hash, ["yaml"] = Hash,
        ["dockerfile"] = Hash, ["makefile"] = Hash, ["coffeescript"] = Hash,
        ["powershell"] = new CommentTokens("#", "<#", "#>"),
        ["elixir"] = Hash, ["julia"] = new CommentTokens("#", "#=", "=#"),
        ["sql"] = new CommentTokens("--", "/*", "*/"),
        ["lua"] = new CommentTokens("--", "--[[", "]]"),
        ["haskell"] = new CommentTokens("--", "{-", "-}"),
        ["html"] = new CommentTokens(null, "<!--", "-->"),
        ["xml"] = new CommentTokens(null, "<!--", "-->"),
        ["markdown"] = new CommentTokens(null, "<!--", "-->"),
        ["css"] = new CommentTokens(null, "/*", "*/"),
        ["scss"] = CStyle, ["less"] = CStyle,
        ["jsonc"] = CStyle,
        ["bat"] = new CommentTokens("REM", null, null),
        ["ini"] = new CommentTokens(";", null, null),
        ["clojure"] = new CommentTokens(";;", null, null),
        ["lisp"] = new CommentTokens(";", null, null),
        ["vb"] = new CommentTokens("'", null, null),
        ["latex"] = new CommentTokens("%", null, null),
        ["erlang"] = new CommentTokens("%", null, null),
        ["asciidoc"] = new CommentTokens("//", "////", "////"),
    };

    public static CommentTokens? Get(string? languageId)
        => languageId is not null && ByLanguage.TryGetValue(languageId, out var tokens) ? tokens : null;
}
