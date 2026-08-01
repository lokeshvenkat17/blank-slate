using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using BlankSlate.Models;
using BlankSlate.ViewModels;

namespace BlankSlate.Views;

public partial class MainWindow : Window
{
    /// <summary>Non-text keys worth recording in a macro (typed characters arrive as TextInput).</summary>
    private static readonly Key[] RecordableKeys =
    [
        Key.Back, Key.Delete, Key.Enter, Key.Return, Key.Tab, Key.Escape,
        Key.Up, Key.Down, Key.Left, Key.Right, Key.Home, Key.End, Key.PageUp, Key.PageDown,
    ];

    private bool _closeConfirmed;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(KeyDownEvent, OnRecordKeyDown, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnRecordTextInput, RoutingStrategies.Tunnel);
        DataContextChanged += (_, _) =>
        {
            BuildLanguageMenu();
            BuildRecentFilesMenu();
            BuildMacroMenu();
            if (ViewModel is not null)
            {
                ViewModel.RecentFiles.CollectionChanged += (_, _) => BuildRecentFilesMenu();
                ViewModel.SavedMacros.CollectionChanged += (_, _) => BuildMacroMenu();
            }
        };
    }

    // ---- Macro recording (window-level tunnel so we see editor input first) ----

    private static bool IsFromEditor(RoutedEventArgs e)
        => e.Source is Avalonia.Visual v && v.FindAncestorOfType<AvaloniaEdit.Editing.TextArea>(includeSelf: true) is not null;

    private void OnRecordKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { IsRecordingMacro: true } vm || !IsFromEditor(e))
            return;
        var hasCommandModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control)
            || e.KeyModifiers.HasFlag(KeyModifiers.Meta)
            || e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        // Don't record the macro-control shortcuts themselves.
        if (hasCommandModifier && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key is Key.R or Key.P)
            return;
        if (hasCommandModifier || RecordableKeys.Contains(e.Key))
            vm.RecordMacroStep(new MacroKeyStep(e.Key, e.KeyModifiers));
    }

    private void OnRecordTextInput(object? sender, TextInputEventArgs e)
    {
        if (ViewModel is { IsRecordingMacro: true } vm && IsFromEditor(e) && !string.IsNullOrEmpty(e.Text))
            vm.RecordMacroStep(new MacroTextStep(e.Text));
    }

    private void BuildMacroMenu()
    {
        if (ViewModel is null)
            return;
        // Remove previously added saved-macro items (everything after the trailing separator).
        var separatorIndex = MacroMenu.Items.IndexOf(MacroMenuSeparator);
        for (var i = MacroMenu.Items.Count - 1; i > separatorIndex; i--)
            MacroMenu.Items.RemoveAt(i);
        foreach (var macro in ViewModel.SavedMacros)
        {
            MacroMenu.Items.Add(new MenuItem
            {
                Header = macro.Name,
                Command = ViewModel.PlaySavedMacroCommand,
                CommandParameter = macro,
            });
        }
    }

    private void OnAboutClick(object? sender, RoutedEventArgs e)
        => (Avalonia.Application.Current as App)?.ShowAboutWindow();

    /// <summary>Double-click a tab header to rename the document (file on disk, or tab title when untitled).</summary>
    private async void OnTabHeaderDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel is null || (sender as Control)?.DataContext is not DocumentViewModel doc)
            return;
        e.Handled = true;
        ViewModel.SelectedDocument = doc;
        await ViewModel.RenameCommand.ExecuteAsync(null);
    }

    private void OnFunctionDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel is not null && FunctionListBox.SelectedItem is Services.FunctionEntry entry)
            ViewModel.GoToFunctionCommand.Execute(entry);
    }

    private void BuildRecentFilesMenu()
    {
        if (ViewModel is null)
            return;
        RecentFilesMenu.Items.Clear();
        foreach (var path in ViewModel.RecentFiles)
        {
            RecentFilesMenu.Items.Add(new MenuItem
            {
                Header = path,
                Command = ViewModel.OpenRecentFileCommand,
                CommandParameter = path,
            });
        }
        if (ViewModel.RecentFiles.Count > 0)
            RecentFilesMenu.Items.Add(new Separator());
        RecentFilesMenu.Items.Add(new MenuItem
        {
            Header = "Empty Recent Files List",
            Command = ViewModel.ClearRecentFilesCommand,
            IsEnabled = ViewModel.RecentFiles.Count > 0,
        });
    }

    /// <summary>Builds Language menu grouped by first letter (Notepad++ style): Normal Text, then A > Asciidoc…, B > Batch…</summary>
    private void BuildLanguageMenu()
    {
        if (ViewModel is null)
            return;
        LanguageMenu.Items.Clear();

        var plainText = new MenuItem { Header = "Normal Text" };
        plainText.Command = ViewModel.SetLanguageCommand;
        plainText.CommandParameter = null;
        LanguageMenu.Items.Add(plainText);
        LanguageMenu.Items.Add(new Separator());

        var byLetter = Services.SyntaxService.Languages
            .GroupBy(l => char.ToUpperInvariant(Services.SyntaxService.GetDisplayName(l)[0]))
            .OrderBy(g => g.Key);
        foreach (var group in byLetter)
        {
            var groupItem = new MenuItem { Header = group.Key.ToString() };
            foreach (var language in group)
            {
                groupItem.Items.Add(new MenuItem
                {
                    Header = Services.SyntaxService.GetDisplayName(language),
                    Command = ViewModel.SetLanguageCommand,
                    CommandParameter = language.Id,
                });
            }
            LanguageMenu.Items.Add(groupItem);
        }
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

        // Session-snapshot mode (default): no prompts — dirty buffers are
        // snapshotted and silently restored next launch, like Notepad++.
        if (ViewModel.SessionSnapshotEnabled)
        {
            ViewModel.SaveSession();
            return;
        }

        // Cancel, run the async unsaved-changes prompts, then re-close for real.
        e.Cancel = true;
        if (await ViewModel.TryCloseAllAsync())
        {
            _closeConfirmed = true;
            Close();
        }
    }
}
