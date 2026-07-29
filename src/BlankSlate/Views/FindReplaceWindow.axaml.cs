using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace BlankSlate.Views;

public partial class FindReplaceWindow : Window
{
    public FindReplaceWindow()
    {
        InitializeComponent();
    }

    public void SelectTab(int index)
    {
        Tabs.SelectedIndex = index;
        FindBox1.Focus();
        FindBox1.SelectAll();
    }

    private async void OnBrowseDirectory(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose search directory",
            AllowMultiple = false,
        });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path
            && DataContext is ViewModels.FindReplaceViewModel vm)
        {
            vm.Directory = path;
        }
    }
}
