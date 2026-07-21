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
}

/// <summary>Window-backed implementation using Avalonia's StorageProvider.</summary>
public sealed class DialogService(Window owner) : IDialogService
{
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
