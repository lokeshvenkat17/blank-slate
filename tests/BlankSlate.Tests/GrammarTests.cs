using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlankSlate.Services;
using TextMateSharp.Registry;
using Xunit;

namespace BlankSlate.Tests;

/// <summary>
/// Verifies the bundled grammars are actually registered and produce real tokens.
/// Loading a grammar file is not enough — a grammar with a broken regex loads fine
/// and then colours nothing, so every language is tokenized against a sample.
/// </summary>
public class GrammarTests
{
    /// <summary>language id, sample source, and a scope fragment that must appear.</summary>
    public static TheoryData<string, string, string> Samples => new()
    {
        { "toml", "[package]\nname = \"blankslate\"  # comment\nversion = 2", "entity.name.tag" },
        { "cmake", "cmake_minimum_required(VERSION 3.20)\n# comment\nset(FOO ON)", "keyword.control" },
        { "haskell", "-- comment\nmodule Main where\nmain :: IO ()\nmain = putStrLn \"hi\"", "keyword.control" },
        { "erlang", "%% comment\n-module(demo).\nadd(A, B) -> A + B.", "keyword.other.directive" },
        { "tcl", "# comment\nproc greet {name} {\n  puts \"hello $name\"\n}", "keyword.control" },
        { "d", "// comment\nimport std.stdio;\nvoid main() { writeln(\"hi\"); }", "keyword.control" },
        { "nim", "# comment\nproc greet(name: string) =\n  echo \"hi\"", "keyword.control" },
        { "scheme", "; comment\n(define (square x) (* x x))", "keyword.control" },
        { "fortran", "! comment\nprogram demo\n  integer :: i = 1\nend program", "keyword.control" },
        { "verilog", "// comment\nmodule top(input clk);\n  reg [7:0] data;\nendmodule", "keyword.control" },
        { "vhdl", "-- comment\nentity counter is\nend entity;", "keyword.control" },
        { "ada", "-- comment\nprocedure Hello is\nbegin\n  null;\nend Hello;", "keyword.control" },
        { "cobol", "      *> comment\n       IDENTIFICATION DIVISION.\n       PROGRAM-ID. DEMO.", "keyword.control" },
        { "smalltalk", "\"comment\"\nObject subclass: #Point\n  x := 3.", "constant.other.symbol" },
        { "matlab", "% comment\nfunction y = square(x)\n  y = x^2;\nend", "keyword.control" },
        { "gdscript", "# comment\nextends Node\nfunc _ready():\n  print(\"hi\")", "keyword.control" },
        { "registry", "; comment\nWindows Registry Editor Version 5.00\n\n[HKEY_CURRENT_USER\\Software\\Demo]\n\"Name\"=\"value\"", "entity.name.tag" },
        { "nsis", "; comment\nName \"Demo\"\nSection \"Main\"\nSectionEnd", "keyword.control" },
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public void BundledGrammar_TokenizesSample(string languageId, string sample, string expectedScopeFragment)
    {
        var scope = SyntaxService.GetScope(languageId);
        Assert.True(scope is not null, $"'{languageId}' is not registered as a language");

        var registry = new Registry(SyntaxService.Registry);
        var grammar = registry.LoadGrammar(scope!);
        Assert.True(grammar is not null, $"grammar '{scope}' failed to load");

        var scopes = new List<string>();
        foreach (var line in sample.Split('\n'))
        {
            foreach (var token in grammar!.TokenizeLine(line).Tokens)
                scopes.AddRange(token.Scopes);
        }

        Assert.True(scopes.Any(s => s.Contains(expectedScopeFragment, StringComparison.Ordinal)),
            $"'{languageId}' produced no '{expectedScopeFragment}' scope. Got: " +
            string.Join(", ", scopes.Distinct().Take(15)));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void BundledGrammar_ProducesComments(string languageId, string sample, string _)
    {
        // Every sample starts with a comment line, so comment scoping must work.
        var registry = new Registry(SyntaxService.Registry);
        var grammar = registry.LoadGrammar(SyntaxService.GetScope(languageId)!);

        var scopes = sample.Split('\n')
            .SelectMany(line => grammar!.TokenizeLine(line).Tokens)
            .SelectMany(t => t.Scopes)
            .ToList();

        Assert.True(scopes.Any(s => s.Contains("comment", StringComparison.Ordinal)),
            $"'{languageId}' never produced a comment scope");
    }

    [Theory]
    [InlineData("Cargo.toml", "toml")]
    [InlineData("main.hs", "haskell")]
    [InlineData("server.erl", "erlang")]
    [InlineData("build.tcl", "tcl")]
    [InlineData("app.d", "d")]
    [InlineData("main.nim", "nim")]
    [InlineData("core.scm", "scheme")]
    [InlineData("solver.f90", "fortran")]
    [InlineData("cpu.v", "verilog")]
    [InlineData("counter.vhd", "vhdl")]
    [InlineData("hello.adb", "ada")]
    [InlineData("payroll.cbl", "cobol")]
    [InlineData("plot.mlx", "matlab")]
    [InlineData("player.gd", "gdscript")]
    [InlineData("tweaks.reg", "registry")]
    [InlineData("installer.nsi", "nsis")]
    public void Extension_MapsToBundledLanguage(string fileName, string expectedLanguageId)
        => Assert.Equal(expectedLanguageId, SyntaxService.DetectLanguageId("/tmp/" + fileName));

    /// <summary>.m is Objective-C on macOS; MATLAB must not hijack it.</summary>
    [Fact]
    public void DotM_StaysObjectiveC()
        => Assert.Equal("objective-c", SyntaxService.DetectLanguageId("/tmp/plot.m"));

    [Fact]
    public void CMakeLists_IsDetectedByFileName()
        => Assert.Equal("cmake", SyntaxService.DetectLanguageId("/proj/CMakeLists.txt"));

    [Fact]
    public void LanguageCount_CoversBundledGrammars()
    {
        // 64 from TextMateSharp.Grammars + 18 authored for BlankSlate.
        Assert.True(SyntaxService.Languages.Count >= 82,
            $"expected at least 82 languages, found {SyntaxService.Languages.Count}");
    }

    [Fact]
    public void NoGrammarFolderFailedToLoad()
        => Assert.True(SyntaxService.LoadErrors.Count == 0,
            "grammar folders failed: " + string.Join("; ", SyntaxService.LoadErrors.Select(e => $"{e.Folder}: {e.Error}")));

    [Fact]
    public void BundledGrammars_AreValidJsonWithMatchingScopes()
    {
        var root = SyntaxService.BundledGrammarsDir;
        Assert.True(Directory.Exists(root), "bundled grammars folder was not copied to the output");

        var packs = Directory.GetDirectories(root)
            .Where(d => File.Exists(Path.Combine(d, "package.json"))).ToList();
        Assert.True(packs.Count > 0, "no grammar pack with a package.json was found");

        var dir = packs[0];
        var package = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "package.json")));
        var declared = package.RootElement.GetProperty("contributes").GetProperty("grammars");

        foreach (var entry in declared.EnumerateArray())
        {
            var scopeName = entry.GetProperty("scopeName").GetString()!;
            var relative = entry.GetProperty("path").GetString()!.TrimStart('.', '/');
            var path = Path.Combine(dir, relative);
            Assert.True(File.Exists(path), $"grammar file missing: {relative}");

            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(scopeName, doc.RootElement.GetProperty("scopeName").GetString());
        }
    }
}
