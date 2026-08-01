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
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            _viewModel = new MainViewModel(new DialogService(mainWindow));
            mainWindow.DataContext = _viewModel;
            desktop.MainWindow = mainWindow;
            _viewModel.InitializePersistence();
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
