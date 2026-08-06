using System.Linq;
using Avalonia.Headless.XUnit;
using BlankSlate.Services;
using BlankSlate.ViewModels;
using Xunit;

namespace BlankSlate.Tests;

public class TwoViewTests
{
    [AvaloniaFact]
    public void NewDocuments_StartInThePrimaryViewOnly()
    {
        var vm = new MainViewModel(null);
        Assert.Single(vm.PrimaryDocuments);
        Assert.Empty(vm.SecondaryDocuments);
        Assert.False(vm.IsSecondaryViewVisible);
    }

    [AvaloniaFact]
    public void MoveToOtherView_MovesTheTabAndShowsTheSecondGroup()
    {
        var vm = new MainViewModel(null);
        var doc = vm.SelectedDocument!;

        vm.MoveToOtherViewCommand.Execute(null);

        Assert.Empty(vm.PrimaryDocuments);
        Assert.Single(vm.SecondaryDocuments);
        Assert.True(vm.IsSecondaryViewVisible);
        Assert.Equal(1, doc.ViewIndex);
        Assert.Same(doc, vm.SelectedDocument);
        Assert.Equal(1, vm.ActiveViewIndex);
    }

    [AvaloniaFact]
    public void MoveToOtherView_IsReversible()
    {
        var vm = new MainViewModel(null);
        vm.MoveToOtherViewCommand.Execute(null);
        vm.MoveToOtherViewCommand.Execute(null);

        Assert.Single(vm.PrimaryDocuments);
        Assert.Empty(vm.SecondaryDocuments);
        Assert.False(vm.IsSecondaryViewVisible);
    }

    [AvaloniaFact]
    public void CloneToOtherView_SharesTheSameBuffer()
    {
        var vm = new MainViewModel(null);
        var original = vm.SelectedDocument!;
        original.Document.Text = "shared content";

        vm.CloneToOtherViewCommand.Execute(null);

        Assert.Single(vm.PrimaryDocuments);
        Assert.Single(vm.SecondaryDocuments);
        var clone = vm.SecondaryDocuments[0];
        Assert.True(clone.IsClone);

        // The point of a clone: one buffer, two tabs.
        Assert.Same(original.Document, clone.Document);
        original.Document.Insert(0, ">> ");
        Assert.Equal(">> shared content", clone.Document.Text);
    }

    [AvaloniaFact]
    public void CloneToOtherView_DoesNotCloneAClone()
    {
        var vm = new MainViewModel(null);
        vm.CloneToOtherViewCommand.Execute(null);
        var before = vm.Documents.Count;

        vm.SelectedDocument = vm.SecondaryDocuments[0];
        vm.CloneToOtherViewCommand.Execute(null);

        Assert.Equal(before, vm.Documents.Count);
    }

    [AvaloniaFact]
    public void SelectingInASecondViewTab_MakesItTheActiveDocument()
    {
        var vm = new MainViewModel(null);
        var first = vm.SelectedDocument!;
        vm.MoveToOtherViewCommand.Execute(null);   // first -> view 1
        vm.NewFileCommand.Execute(null);
        vm.MoveToOtherViewCommand.Execute(null);   // a second tab in view 1
        vm.NewFileCommand.Execute(null);           // two tabs back in view 0
        var primaryDoc = vm.SelectedDocument!;
        vm.NewFileCommand.Execute(null);

        Assert.Equal(0, vm.ActiveViewIndex);
        Assert.Equal(2, vm.SecondaryDocuments.Count);
        Assert.Equal(2, vm.PrimaryDocuments.Count);

        // Switching tabs inside the second group activates that group.
        vm.SecondarySelectedDocument = first;
        Assert.Equal(1, vm.ActiveViewIndex);
        Assert.Same(first, vm.SelectedDocument);

        // And picking a tab back in the first group returns focus there.
        vm.PrimarySelectedDocument = primaryDoc;
        Assert.Equal(0, vm.ActiveViewIndex);
        Assert.Same(primaryDoc, vm.SelectedDocument);
    }

    [AvaloniaFact]
    public void ClosingTheLastSecondViewTab_HidesTheGroup()
    {
        var vm = new MainViewModel(null);
        vm.NewFileCommand.Execute(null);
        vm.MoveToOtherViewCommand.Execute(null);
        Assert.True(vm.IsSecondaryViewVisible);

        var moved = vm.SecondaryDocuments[0];
        vm.Documents.Remove(moved);

        Assert.False(vm.IsSecondaryViewVisible);
        Assert.Empty(vm.SecondaryDocuments);
    }
}

public class WordCompletionTests
{
    [Theory]
    [InlineData("hello wor", 9, "wor")]
    [InlineData("hello", 5, "hello")]
    [InlineData("a.b", 3, "b")]
    [InlineData("text ", 5, "")]
    public void GetPrefix_ReadsThePartialWordBeforeTheCaret(string text, int offset, string expected)
        => Assert.Equal(expected, WordCompletion.GetPrefix(text, offset));

    [Fact]
    public void GetSuggestions_ReturnsMatchingWordsFromTheDocument()
    {
        const string text = "renderWidget renderAll renderWidget banana";
        var suggestions = WordCompletion.GetSuggestions(text, "render");

        Assert.Contains("renderWidget", suggestions);
        Assert.Contains("renderAll", suggestions);
        Assert.DoesNotContain("banana", suggestions);
        // renderWidget appears twice, so it ranks first.
        Assert.Equal("renderWidget", suggestions[0]);
    }

    [Fact]
    public void GetSuggestions_ExcludesThePrefixItself()
        => Assert.DoesNotContain("render", WordCompletion.GetSuggestions("render rendering", "render"));

    [Fact]
    public void GetSuggestions_IsEmptyForNoPrefix()
        => Assert.Empty(WordCompletion.GetSuggestions("some text here", ""));

    [Fact]
    public void GetSuggestions_SkipsVeryShortWords()
        => Assert.DoesNotContain("ab", WordCompletion.GetSuggestions("ab abc abcd", "ab"));
}

public class BeginEndSelectTests
{
    [AvaloniaFact]
    public void BeginEndSelect_AnchorsThenExtendsTheSelection()
    {
        var window = new BlankSlate.Views.MainWindow();
        var vm = new MainViewModel(null);
        window.DataContext = vm;
        window.Show();
        window.UpdateLayout();

        var doc = vm.SelectedDocument!;
        doc.Document.Text = "0123456789";
        var handle = doc.EditorHandle!;

        handle.CaretOffset = 2;
        vm.BeginEndSelectCommand.Execute(null);   // anchor at 2
        Assert.Equal(0, handle.SelectionLength);

        handle.CaretOffset = 7;
        vm.BeginEndSelectCommand.Execute(null);   // extend to 7

        Assert.Equal(2, handle.SelectionStart);
        Assert.Equal(5, handle.SelectionLength);
    }
}
