using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using BlankSlate.ViewModels;

namespace BlankSlate.Views;

public partial class MainWindow : Window
{
    private bool _closeConfirmed;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (ViewModel is null || e.DataTransfer.TryGetFiles() is not { } items)
            return;
        foreach (var item in items)
        {
            if (item is IStorageFile file && file.TryGetLocalPath() is { } path)
                await ViewModel.OpenPathAsync(path);
        }
    }

    private async void OnResultDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (ViewModel is not null && ResultsList.SelectedItem is ViewModels.SearchResultItem item)
            await ViewModel.GoToSearchResultCommand.ExecuteAsync(item);
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_closeConfirmed || ViewModel is null)
            return;

        // Cancel, run the async unsaved-changes prompts, then re-close for real.
        e.Cancel = true;
        if (await ViewModel.TryCloseAllAsync())
        {
            _closeConfirmed = true;
            Close();
        }
    }
}
