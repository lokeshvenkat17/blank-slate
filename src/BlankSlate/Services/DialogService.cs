using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace BlankSlate.Services;

public enum SaveConfirmation { Save, DontSave, Cancel }

public interface IDialogService
{
    Task<IReadOnlyList<string>> ShowOpenFileDialogAsync();
    Task<string?> ShowSaveFileDialogAsync(string suggestedName);
    Task<SaveConfirmation> ShowConfirmSaveAsync(string documentName);

    /// <summary>Shows (or fronts) the non-modal Find/Replace window on the given tab: 0=Find 1=Replace 2=Mark 3=Find in Files.</summary>
    void ShowFindReplace(ViewModels.FindReplaceViewModel viewModel, int tabIndex);

    Task<int?> ShowGoToLineAsync(int currentLine, int maxLine);

    /// <summary>Generic single-field text prompt. Returns null on cancel.</summary>
    Task<string?> ShowTextInputAsync(string title, string label, string initial = "");
}

/// <summary>Window-backed implementation using Avalonia's StorageProvider.</summary>
public sealed class DialogService(Window owner) : IDialogService
{
    private Views.FindReplaceWindow? _findReplaceWindow;

    public void ShowFindReplace(ViewModels.FindReplaceViewModel viewModel, int tabIndex)
    {
        if (_findReplaceWindow is null)
        {
            _findReplaceWindow = new Views.FindReplaceWindow { DataContext = viewModel };
            _findReplaceWindow.Closed += (_, _) => _findReplaceWindow = null;
            _findReplaceWindow.Show(owner);
        }
        else
        {
            _findReplaceWindow.Activate();
        }
        _findReplaceWindow.SelectTab(tabIndex);
    }

    public async Task<string?> ShowTextInputAsync(string title, string label, string initial = "")
    {
        string? result = null;
        var dialog = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
        };

        var input = new TextBox { Text = initial, MinWidth = 260 };
        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80 };
        ok.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(input.Text))
            {
                result = input.Text.Trim();
                dialog.Close();
            }
        };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80 };
        cancel.Click += (_, _) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(24),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = label },
                input,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { cancel, ok },
                },
            },
        };

        input.AttachedToVisualTree += (_, _) => { input.Focus(); input.SelectAll(); };
        await dialog.ShowDialog(owner);
        return result;
    }

    public async Task<int?> ShowGoToLineAsync(int currentLine, int maxLine)
    {
        int? result = null;
        var dialog = new Window
        {
            Title = "Go to…",
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
        };

        var input = new TextBox
        {
            Text = currentLine.ToString(),
            PlaceholderText = $"1 – {maxLine}",
            MinWidth = 200,
        };

        var ok = new Button { Content = "Go", IsDefault = true, MinWidth = 80 };
        ok.Click += (_, _) =>
        {
            if (int.TryParse(input.Text, out var line) && line >= 1 && line <= maxLine)
            {
                result = line;
                dialog.Close();
            }
        };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80 };
        cancel.Click += (_, _) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(24),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = $"Line number (1 – {maxLine}):" },
                input,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { cancel, ok },
                },
            },
        };

        input.AttachedToVisualTree += (_, _) => { input.Focus(); input.SelectAll(); };
        await dialog.ShowDialog(owner);
        return result;
    }
    public async Task<IReadOnlyList<string>> ShowOpenFileDialogAsync()
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open",
            AllowMultiple = true,
        });
        return files
            .Select(f => f.TryGetLocalPath())
            .Where(p => p is not null)
            .Cast<string>()
            .ToList();
    }

    public async Task<string?> ShowSaveFileDialogAsync(string suggestedName)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save As",
            SuggestedFileName = suggestedName,
            ShowOverwritePrompt = true,
        });
        return file?.TryGetLocalPath();
    }

    public async Task<SaveConfirmation> ShowConfirmSaveAsync(string documentName)
    {
        var result = SaveConfirmation.Cancel;

        var dialog = new Window
        {
            Title = "BlankSlate",
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
        };

        Button MakeButton(string text, SaveConfirmation value, bool isDefault = false)
        {
            var button = new Button
            {
                Content = text,
                MinWidth = 90,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                IsDefault = isDefault,
                IsCancel = value == SaveConfirmation.Cancel,
            };
            button.Click += (_, _) => { result = value; dialog.Close(); };
            return button;
        }

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(24),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = $"Save changes to “{documentName}” before closing?",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    MaxWidth = 360,
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children =
                    {
                        MakeButton("Don't Save", SaveConfirmation.DontSave),
                        MakeButton("Cancel", SaveConfirmation.Cancel),
                        MakeButton("Save", SaveConfirmation.Save, isDefault: true),
                    },
                },
            },
        };

        await dialog.ShowDialog(owner);
        return result;
    }
}
