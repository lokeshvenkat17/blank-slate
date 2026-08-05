using Avalonia.Controls;
using Avalonia.Interactivity;
using BlankSlate.Services;
using BlankSlate.ViewModels;

namespace BlankSlate.Views;

public partial class PluginManagerWindow : Window
{
    public PluginManagerWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnPluginToggled(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm
            && sender is CheckBox { DataContext: PluginEntry entry, IsChecked: { } isChecked })
        {
            vm.SetPluginEnabled(entry, isChecked);
        }
    }
}
