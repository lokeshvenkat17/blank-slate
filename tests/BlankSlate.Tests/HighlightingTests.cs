using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using BlankSlate.ViewModels;
using BlankSlate.Views;
using Xunit;

namespace BlankSlate.Tests;

/// <summary>Verifies TextMate syntax highlighting is actually wired to the editor.</summary>
public class HighlightingTests
{
    private static (Window Window, MainViewModel Vm, TextEditor Editor) Show()
    {
        var window = new MainWindow { Width = 900, Height = 500 };
        var vm = new MainViewModel(null);
        window.DataContext = vm;
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        var editor = window.GetVisualDescendants().OfType<TextEditor>().First();
        return (window, vm, editor);
    }

    [AvaloniaFact]
    public void SettingLanguage_AddsTextMateLineTransformer_WithoutErrors()
    {
        var originalError = Console.Error;
        var captured = new StringWriter();
        Console.SetError(captured);
        try
        {
            var (window, vm, editor) = Show();
            var doc = vm.SelectedDocument!;
            doc.Document.Text = "using System;\npublic class Foo { }";
            doc.LanguageId = "csharp";
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            Assert.False(captured.ToString().Contains("failed to load"),
                $"grammar load reported an error: {captured}");

            // TextMate colorizes through a line transformer on the TextView.
            var transformers = editor.TextArea.TextView.LineTransformers;
            var names = string.Join(", ", transformers.Select(t => t.GetType().FullName));
            Assert.True(
                transformers.Any(t => t.GetType().FullName?.Contains("TextMate", StringComparison.OrdinalIgnoreCase) == true),
                $"no TextMate line transformer installed; found: {names}");
        }
        finally
        {
            Console.SetError(originalError);
        }
    }
}
