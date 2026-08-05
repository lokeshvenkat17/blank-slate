using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using BlankSlate.ViewModels;
using BlankSlate.Views;
using Xunit;

namespace BlankSlate.Tests;

/// <summary>
/// Renders the real window to a PNG so the UI can be inspected rather than assumed.
/// Output goes to the path in BLANKSLATE_SHOT_DIR (or the system temp dir).
/// </summary>
public class ScreenshotTests
{
    private static string OutputDir =>
        Environment.GetEnvironmentVariable("BLANKSLATE_SHOT_DIR") ?? Path.GetTempPath();

    private static void Capture(Window window, string name)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Directory.CreateDirectory(OutputDir);
        var path = Path.Combine(OutputDir, name + ".png");
        frame!.Save(path);
        Assert.True(new FileInfo(path).Length > 0, "captured frame was empty");
    }

    [AvaloniaFact]
    public void Capture_EmptyUntitledDocument()
    {
        var window = new MainWindow { Width = 1000, Height = 650 };
        window.DataContext = new MainViewModel(null);
        window.Show();
        Capture(window, "01-untitled");
    }

    [AvaloniaFact]
    public void Capture_WithTypedText()
    {
        var window = new MainWindow { Width = 1000, Height = 650 };
        var vm = new MainViewModel(null);
        window.DataContext = vm;
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        vm.SelectedDocument!.Document.Text =
            "The quick brown fox\njumps over the lazy dog\n\nTyping should be visible here.";
        Capture(window, "02-with-text");
    }

    [AvaloniaFact]
    public void Capture_CSharpFileHighlighted()
    {
        var window = new MainWindow { Width = 1000, Height = 650 };
        var vm = new MainViewModel(null);
        window.DataContext = vm;
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var doc = vm.SelectedDocument!;
        doc.LanguageId = "csharp";
        doc.Document.Text = """
            using System;

            namespace Demo;

            public class Greeter
            {
                // Says hello
                public void Hello(string name)
                {
                    Console.WriteLine($"Hello, {name}!");
                }
            }
            """;
        Capture(window, "03-csharp");
    }
}
