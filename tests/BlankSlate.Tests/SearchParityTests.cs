using System.Linq;
using AvaloniaEdit.Document;
using BlankSlate.Models;
using BlankSlate.Services;
using Xunit;

namespace BlankSlate.Tests;

public class BraceMatcherTests
{
    [Theory]
    [InlineData("(abc)", 0, 4)]      // caret on the opener
    [InlineData("(abc)", 5, 0)]      // caret just after the closer
    [InlineData("[1, [2], 3]", 0, 10)]
    [InlineData("{ a { b } c }", 0, 12)]
    [InlineData("f(g(x))", 2, 6)]
    public void FindMatch_PairsBrackets(string text, int caret, int expectedMatch)
    {
        var result = BraceMatcher.FindMatch(text, caret);
        Assert.NotNull(result);
        Assert.Equal(expectedMatch, result!.Value.Match);
    }

    [Theory]
    [InlineData("(abc", 0)]          // unbalanced
    [InlineData("abc", 1)]           // not on a brace
    [InlineData("", 0)]
    public void FindMatch_ReturnsNullWhenNoPair(string text, int caret)
        => Assert.Null(BraceMatcher.FindMatch(text, caret));

    [Fact]
    public void FindInnerRange_ExcludesTheBraces()
    {
        var range = BraceMatcher.FindInnerRange("f(arg)", 1);
        Assert.NotNull(range);
        Assert.Equal(2, range!.Value.Start);
        Assert.Equal(3, range.Value.Length);
    }

    [Fact]
    public void FindInnerRange_HandlesEmptyPair()
    {
        var range = BraceMatcher.FindInnerRange("()", 0);
        Assert.NotNull(range);
        Assert.Equal(0, range!.Value.Length);
    }
}

public class StyleMarkSetTests
{
    private static (TextDocument, StyleMarkSet) Setup(string text)
    {
        var doc = new TextDocument(text);
        return (doc, new StyleMarkSet(doc));
    }

    [Fact]
    public void MarkAll_StylesEveryWholeWordOccurrence()
    {
        var (_, marks) = Setup("foo bar foo foobar foo");
        var count = marks.MarkAll("foo", 0);
        Assert.Equal(3, count); // "foobar" must not match as a whole word
        Assert.Equal(3, marks.GetSegments(0).Count());
    }

    [Fact]
    public void MarkAll_KeepsStylesIndependent()
    {
        var (_, marks) = Setup("alpha beta alpha");
        marks.MarkAll("alpha", 0);
        marks.MarkAll("beta", 1);

        Assert.Equal(2, marks.GetSegments(0).Count());
        Assert.Single(marks.GetSegments(1));
        Assert.Empty(marks.GetSegments(2));
    }

    /// <summary>The reason marks use anchored segments: they must follow their text.</summary>
    [Fact]
    public void Marks_FollowTextThroughEdits()
    {
        var (doc, marks) = Setup("alpha beta");
        marks.MarkAll("beta", 0);
        var before = marks.GetSegments(0).Single().StartOffset;
        Assert.Equal(6, before);

        doc.Insert(0, ">>> ");   // shift everything right

        var after = marks.GetSegments(0).Single();
        Assert.Equal(before + 4, after.StartOffset);
        Assert.Equal("beta", doc.GetText(after.StartOffset, after.Length));
    }

    [Fact]
    public void Clear_RemovesOnlyThatStyle()
    {
        var (_, marks) = Setup("a b");
        marks.MarkAll("a", 0);
        marks.MarkAll("b", 1);

        marks.Clear(0);
        Assert.Empty(marks.GetSegments(0));
        Assert.Single(marks.GetSegments(1));

        marks.ClearAll();
        Assert.False(marks.HasAnyMarks);
    }

    [Fact]
    public void NextAndPreviousMark_WrapAround()
    {
        var (_, marks) = Setup("x .. x .. x");
        marks.MarkAll("x", 0);

        Assert.Equal(5, marks.NextMark(0, 0));
        Assert.Equal(0, marks.NextMark(10, 0));    // wraps to first
        Assert.Equal(5, marks.PreviousMark(10, 0));
        Assert.Equal(10, marks.PreviousMark(0, 0)); // wraps to last
    }

    [Fact]
    public void GetStyledText_ReturnsMarkedTextInOrder()
    {
        var (_, marks) = Setup("one two one");
        marks.MarkAll("one", 0);
        var lines = marks.GetStyledText(0).Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(["one", "one"], lines.Select(l => l.Trim()).ToArray());
    }

    [Fact]
    public void MarkOne_DoesNotDuplicateTheSameRange()
    {
        var (_, marks) = Setup("hello");
        marks.MarkOne(0, 5, 0);
        marks.MarkOne(0, 5, 0);
        Assert.Single(marks.GetSegments(0));
    }

    [Theory]
    [InlineData("hello world", 0, "hello")]
    [InlineData("hello world", 7, "world")]
    [InlineData("foo_bar1 x", 2, "foo_bar1")]
    public void GetWordAt_FindsTheTokenUnderTheCaret(string text, int offset, string expected)
    {
        var doc = new TextDocument(text);
        var word = StyleMarkSet.GetWordAt(doc, offset);
        Assert.NotNull(word);
        Assert.Equal(expected, doc.GetText(word!.Value.Offset, word.Value.Length));
    }

    [Fact]
    public void GetWordAt_ReturnsNullBetweenWords()
    {
        var doc = new TextDocument("a   b");
        Assert.Null(StyleMarkSet.GetWordAt(doc, 2));
    }
}

public class ChangeHistoryTests
{
    [Fact]
    public void EditedLine_IsMarkedModified()
    {
        var doc = new TextDocument("one\ntwo\nthree");
        var history = new ChangeHistory(doc);

        Assert.Equal(ChangeState.None, history.GetLineState(2));

        var line2 = doc.GetLineByNumber(2);
        doc.Replace(line2.Offset, line2.Length, "TWO");

        Assert.Equal(ChangeState.Modified, history.GetLineState(2));
        Assert.Equal(ChangeState.None, history.GetLineState(1));
    }

    [Fact]
    public void SavingConvertsModifiedToSaved()
    {
        var doc = new TextDocument("one\ntwo");
        var history = new ChangeHistory(doc);
        doc.Insert(0, "X");

        Assert.Equal(ChangeState.Modified, history.GetLineState(1));
        history.MarkSaved();
        Assert.Equal(ChangeState.Saved, history.GetLineState(1));
    }

    [Fact]
    public void SuppressTracking_IgnoresLoadingTheFile()
    {
        var doc = new TextDocument();
        var history = new ChangeHistory(doc);

        using (history.SuppressTracking())
            doc.Text = "loaded from disk\nsecond line";

        Assert.Empty(history.ChangedLines());
    }

    [Fact]
    public void NextAndPreviousChange_WrapAround()
    {
        var doc = new TextDocument("a\nb\nc\nd");
        var history = new ChangeHistory(doc);

        var line3 = doc.GetLineByNumber(3);
        doc.Replace(line3.Offset, line3.Length, "C");
        var line1 = doc.GetLineByNumber(1);
        doc.Replace(line1.Offset, line1.Length, "A");

        Assert.Equal([1, 3], history.ChangedLines());
        Assert.Equal(3, history.NextChange(1));
        Assert.Equal(1, history.NextChange(3));   // wraps
        Assert.Equal(1, history.PreviousChange(3));
        Assert.Equal(3, history.PreviousChange(1)); // wraps
    }

    [Fact]
    public void Clear_RemovesAllMarkers()
    {
        var doc = new TextDocument("x");
        var history = new ChangeHistory(doc);
        doc.Insert(0, "y");
        Assert.NotEmpty(history.ChangedLines());

        history.Clear();
        Assert.Empty(history.ChangedLines());
    }
}
