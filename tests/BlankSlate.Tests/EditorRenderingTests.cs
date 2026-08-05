using System.Linq;
using Avalonia;
using Avalonia.Threading;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using BlankSlate.Services;
using BlankSlate.ViewModels;
using BlankSlate.Views;
using Xunit;

namespace BlankSlate.Tests;

/// <summary>
/// Guards the editor surface actually materializes. A previous regression left the
/// TextEditor rendering nothing at all (AvaloniaEdit control theme was never included),
/// which "the process is still running" checks did not catch.
/// </summary>
public class EditorRenderingTests
{
    private static (Window Window, MainViewModel Vm) ShowMainWindow()
    {
        var window = new MainWindow();
        var vm = new MainViewModel(null);
        window.DataContext = vm;
        window.Show();
        // Two layout passes: one to build the tab content, one to size it.
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return (window, vm);
    }

    private static TextEditor? FindEditor(Window window)
        => window.GetVisualDescendants().OfType<TextEditor>().FirstOrDefault();

    [AvaloniaFact]
    public void UntitledDocument_ShowsEditorSurface()
    {
        var (window, vm) = ShowMainWindow();

        Assert.Single(vm.Documents);
        Assert.Null(vm.SelectedDocument!.LanguageId); // plain text

        var editor = FindEditor(window);
        Assert.NotNull(editor);
        Assert.True(editor!.Bounds.Width > 0, $"editor width was {editor.Bounds.Width}");
        Assert.True(editor.Bounds.Height > 0, $"editor height was {editor.Bounds.Height}");
    }

    /// <summary>
    /// Regression guard: AvaloniaEdit's control theme must be included in App.axaml.
    /// Without it the TextEditor gets no template, so it has no visual children and
    /// renders nothing — no text and no line numbers — even though its bounds, caret
    /// position, and document binding all look correct.
    /// </summary>
    [AvaloniaFact]
    public void Editor_HasAppliedTemplate_WithVisibleTextView()
    {
        var (window, _) = ShowMainWindow();
        var editor = FindEditor(window);
        Assert.NotNull(editor);

        var textView = editor!.GetVisualDescendants().OfType<TextView>().FirstOrDefault();
        Assert.True(textView is not null,
            "TextEditor has no TextView in its visual tree — the AvaloniaEdit control theme is missing.");
        Assert.True(textView!.Bounds.Height > 0, "TextView rendered with zero height");

        // Line numbers are part of the templated surface too.
        var margins = editor.GetVisualDescendants().OfType<LineNumberMargin>().ToList();
        Assert.True(margins.Count > 0, "line number margin was never realized");
    }

    [AvaloniaFact]
    public void EditorIsWiredToSelectedDocument_AndAcceptsText()
    {
        var (window, vm) = ShowMainWindow();
        var doc = vm.SelectedDocument!;

        // The editor must be bound to the document, and the view-model must have
        // received the editor handle that every Edit/Search command depends on.
        var editor = FindEditor(window);
        Assert.NotNull(editor);
        Assert.Same(doc.Document, editor!.Document);
        Assert.NotNull(doc.EditorHandle);

        editor.Document.Text = "hello world";
        Assert.Equal("hello world", doc.Document.Text);
        Assert.True(doc.IsDirty);
    }

    [AvaloniaFact]
    public void SwitchingLanguage_KeepsEditorAlive()
    {
        var (window, vm) = ShowMainWindow();
        var doc = vm.SelectedDocument!;

        // plain text -> C# -> back to plain text exercises both SetGrammar and the
        // reinstall path that clears highlighting.
        doc.LanguageId = "csharp";
        Dispatcher.UIThread.RunJobs();
        doc.LanguageId = null;
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var editor = FindEditor(window);
        Assert.NotNull(editor);
        Assert.True(editor!.Bounds.Height > 0);
    }

    [AvaloniaFact]
    public void OpeningFileWithoutKnownLanguage_StillShowsEditor()
    {
        // .txt has no TextMate grammar — the exact case that broke.
        Assert.Null(SyntaxService.DetectLanguageId("/tmp/notes.txt"));

        var (window, vm) = ShowMainWindow();
        vm.SelectedDocument!.LanguageId = SyntaxService.DetectLanguageId("/tmp/notes.txt");
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var editor = FindEditor(window);
        Assert.NotNull(editor);
        Assert.True(editor!.Bounds.Height > 0);
    }
}
