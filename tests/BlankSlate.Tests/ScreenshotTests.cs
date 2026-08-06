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
        // TextMate tokenizes asynchronously; pump repeatedly so colorization lands
        // before the frame is captured.
        for (var i = 0; i < 40; i++)
        {
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            System.Threading.Thread.Sleep(25);
            Dispatcher.UIThread.RunJobs();
        }

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

    [AvaloniaFact]
    public void Capture_AboutDialog()
    {
        var about = new AboutWindow();
        about.Show();
        Capture(about, "04-about");
    }

    [AvaloniaFact]
    public void Capture_PluginManager_WithLoadedPlugin()
    {
        var vm = new MainViewModel(null);
        // Point the loader at the sample plugin staged in a temp folder.
        vm.LoadPluginsFrom(PluginTestSupport.StageSamplePlugin());

        var window = new PluginManagerWindow { Width = 640, Height = 440, DataContext = vm };
        window.Show();
        Capture(window, "05-plugin-manager");
    }

    [AvaloniaFact]
    public void Capture_BundledGrammar_Toml()
    {
        var window = new MainWindow { Width = 1000, Height = 650 };
        var vm = new MainViewModel(null);
        window.DataContext = vm;
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var doc = vm.SelectedDocument!;
        doc.LanguageId = "toml";
        doc.Document.Text = """
            # BlankSlate bundled grammar demo
            [package]
            name = "blankslate"
            version = "0.1.0"
            edition = 2024

            [dependencies]
            avalonia = { version = "12.1.0", features = ["desktop"] }
            enabled = true
            """;
        Capture(window, "06-toml-grammar");
    }

    [AvaloniaFact]
    public void Capture_BundledGrammar_Haskell()
    {
        var window = new MainWindow { Width = 1000, Height = 650 };
        var vm = new MainViewModel(null);
        window.DataContext = vm;
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var doc = vm.SelectedDocument!;
        doc.LanguageId = "haskell";
        doc.Document.Text = """
            -- Bundled Haskell grammar
            module Main where

            import Data.List (sort)

            factorial :: Integer -> Integer
            factorial 0 = 1
            factorial n = n * factorial (n - 1)

            main :: IO ()
            main = do
              let xs = sort [3, 1, 2]
              putStrLn "sorted"
            """;
        Capture(window, "07-haskell-grammar");
    }
}
