using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using BlankSlate.Services;
using BlankSlate.ViewModels;
using BlankSlate.Views;

namespace BlankSlate;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            var viewModel = new MainViewModel(new DialogService(mainWindow));
            mainWindow.DataContext = viewModel;
            desktop.MainWindow = mainWindow;
            viewModel.InitializePersistence();
            _ = viewModel.RestoreSessionAsync().ContinueWith(
                t => System.Console.Error.WriteLine($"Session restore failed: {t.Exception}"),
                System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
        }

        base.OnFrameworkInitializationCompleted();
    }
}