using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using BlankSlate.Services;
using BlankSlate.ViewModels;
using BlankSlate.Views;

namespace BlankSlate;

public partial class App : Application
{
    private MainViewModel? _viewModel;

    public override void Initialize()
    {
        // Both must happen before the windowing platform builds the macOS application
        // menu — setting them in OnFrameworkInitializationCompleted is too late and
        // leaves the menu bar reading "Avalonia Application".
        Name = "BlankSlate";
        AvaloniaXamlLoader.Load(this);
        SetupMacAppMenu();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            _viewModel = new MainViewModel(new DialogService(mainWindow));
            // Settings (which plugins are disabled) and plugin loading must both happen
            // before the DataContext is set, so the Plugins menu builds fully populated.
            _viewModel.InitializePersistence();
            _viewModel.LoadPlugins();
            mainWindow.DataContext = _viewModel;
            desktop.MainWindow = mainWindow;
            _ = RunStartupAsync(_viewModel, desktop.Args);
        }

        // Finder "Open With" / dock drops arrive as file-activation events on macOS.
        if (TryGetFeature(typeof(IActivatableLifetime)) is IActivatableLifetime activatable)
            activatable.Activated += OnActivated;

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task RunStartupAsync(MainViewModel viewModel, string[]? args)
    {
        try
        {
            await viewModel.RestoreSessionAsync();
            if (args is { Length: > 0 })
            {
                foreach (var arg in args)
                {
                    if (File.Exists(arg))
                        await viewModel.OpenPathAsync(Path.GetFullPath(arg));
                }
            }
        }
        catch (System.Exception ex)
        {
            System.Console.Error.WriteLine($"Startup restore/open failed: {ex}");
        }
    }

    /// <summary>
    /// Native macOS app menu. Supplying our own About item replaces Avalonia's default
    /// "About Avalonia" entry; Services/Hide/Quit still come from the system menu.
    /// </summary>
    private void SetupMacAppMenu()
    {
        var about = new Avalonia.Controls.NativeMenuItem("About BlankSlate");
        about.Click += (_, _) => ShowAboutWindow();
        var menu = new Avalonia.Controls.NativeMenu();
        menu.Add(about);
        Avalonia.Controls.NativeMenu.SetMenu(this, menu);
    }

    public void ShowAboutWindow()
    {
        var about = new AboutWindow();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
            about.ShowDialog(owner);
        else
            about.Show();
    }

    private async void OnActivated(object? sender, ActivatedEventArgs e)
    {
        if (_viewModel is null || e is not FileActivatedEventArgs fileArgs)
            return;
        foreach (var item in fileArgs.Files)
        {
            if (item is IStorageFile file && file.TryGetLocalPath() is { } path)
                await _viewModel.OpenPathAsync(path);
        }
    }
}
