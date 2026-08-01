using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace BlankSlate.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        if (version is not null)
            VersionText.Text = $"Version {version.ToString(3)}";
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close();
}
