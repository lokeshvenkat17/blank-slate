using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
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
        WireNativeMenuClicks();
        if (Services.LayoutDiagnostics.IsEnabled)
        {
            // Dump after layout has settled so bounds are meaningful.
            Opened += (_, _) => Avalonia.Threading.DispatcherTimer.RunOnce(
                () => Services.LayoutDiagnostics.DumpVisualTree(this, "MainWindow after show"),
                TimeSpan.FromSeconds(2));
        }

        DataContextChanged += (_, _) =>
        {
            BuildLanguageMenu();
            BuildRecentFilesMenu();
            BuildMacroMenu();
            BuildPluginsMenu();
            if (ViewModel is not null)
            {
                ViewModel.RecentFiles.CollectionChanged += (_, _) => BuildRecentFilesMenu();
                ViewModel.SavedMacros.CollectionChanged += (_, _) => BuildMacroMenu();
                if (ViewModel.PluginHost is { } host)
                    host.CommandsChanged += (_, _) => BuildPluginsMenu();
                ViewModel.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(MainViewModel.IsSecondaryViewVisible))
                        UpdateViewColumns();
                };
                UpdateViewColumns();
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


    /// <summary>Enter / Shift+Enter step through matches; Esc closes the bar.</summary>
    private void OnIncrementalSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is null)
            return;
        switch (e.Key)
        {
            case Key.Enter or Key.Return:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    ViewModel.IncrementalSearchPreviousCommand.Execute(null);
                else
                    ViewModel.IncrementalSearchNextCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                ViewModel.HideIncrementalSearchCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    /// <summary>Gives the second tab group half the width only while it holds documents.</summary>
    private void UpdateViewColumns()
    {
        if (ViewModel is null)
            return;
        ViewsGrid.ColumnDefinitions[2].Width = ViewModel.IsSecondaryViewVisible
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
    }

    // ---- Native menu plumbing ----
    //
    // NativeMenuItem is not a Control, so x:Name generates no field. The dynamic
    // menus are located by their header instead.

    private NativeMenu? FindMenu(params string[] headerPath)
    {
        var menu = NativeMenu.GetMenu(this);
        foreach (var header in headerPath)
        {
            var item = menu?.Items.OfType<NativeMenuItem>()
                .FirstOrDefault(i => i.Header == header);
            menu = item?.Menu;
        }
        return menu;
    }

    /// <summary>Item counts of the statically declared part of each dynamic menu.</summary>
    private readonly Dictionary<string, int> _staticMenuCounts = [];

    /// <summary>Removes previously appended dynamic entries, keeping the XAML-declared ones.</summary>
    private NativeMenu? TrimToStaticItems(string key, params string[] headerPath)
    {
        var menu = FindMenu(headerPath);
        if (menu is null)
            return null;
        if (!_staticMenuCounts.TryGetValue(key, out var keep))
            _staticMenuCounts[key] = keep = menu.Items.Count;
        while (menu.Items.Count > keep)
            menu.Items.RemoveAt(menu.Items.Count - 1);
        return menu;
    }

    private void BuildMacroMenu()
    {
        if (ViewModel is null || TrimToStaticItems("macro", "Macro") is not { } menu)
            return;
        foreach (var macro in ViewModel.SavedMacros)
        {
            menu.Items.Add(new NativeMenuItem(macro.Name)
            {
                Command = ViewModel.PlaySavedMacroCommand,
                CommandParameter = macro,
            });
        }
    }

    /// <summary>Groups plugin-contributed commands into one submenu per plugin.</summary>
    private void BuildPluginsMenu()
    {
        if (ViewModel is null || TrimToStaticItems("plugins", "Plugins") is not { } menu)
            return;

        foreach (var group in ViewModel.PluginCommands.GroupBy(c => c.PluginName))
        {
            var groupItem = new NativeMenuItem(group.Key) { Menu = new NativeMenu() };
            foreach (var command in group)
            {
                groupItem.Menu.Items.Add(new NativeMenuItem(command.Title)
                {
                    Command = ViewModel.RunPluginCommandCommand,
                    CommandParameter = command,
                });
            }
            menu.Items.Add(groupItem);
        }

        if (ViewModel.PluginCommands.Count == 0)
            menu.Items.Add(new NativeMenuItem("(no plugins installed)") { IsEnabled = false });
    }

    private void BuildRecentFilesMenu()
    {
        if (ViewModel is null || FindMenu("File", "Open Recent") is not { } menu)
            return;
        menu.Items.Clear();
        foreach (var path in ViewModel.RecentFiles)
        {
            menu.Items.Add(new NativeMenuItem(path)
            {
                Command = ViewModel.OpenRecentFileCommand,
                CommandParameter = path,
            });
        }
        if (ViewModel.RecentFiles.Count > 0)
            menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(new NativeMenuItem("Empty Recent Files List")
        {
            Command = ViewModel.ClearRecentFilesCommand,
            IsEnabled = ViewModel.RecentFiles.Count > 0,
        });
    }

    /// <summary>Language menu grouped by first letter, Notepad++ style.</summary>
    private void BuildLanguageMenu()
    {
        if (ViewModel is null || FindMenu("Language") is not { } menu)
            return;
        menu.Items.Clear();

        menu.Items.Add(new NativeMenuItem("Normal Text")
        {
            Command = ViewModel.SetLanguageCommand,
            CommandParameter = null,
        });
        menu.Items.Add(new NativeMenuItemSeparator());

        var byLetter = Services.SyntaxService.Languages
            .GroupBy(l => char.ToUpperInvariant(Services.SyntaxService.GetDisplayName(l)[0]))
            .OrderBy(g => g.Key);
        foreach (var group in byLetter)
        {
            var groupItem = new NativeMenuItem(group.Key.ToString()) { Menu = new NativeMenu() };
            foreach (var language in group)
            {
                groupItem.Menu.Items.Add(new NativeMenuItem(Services.SyntaxService.GetDisplayName(language))
                {
                    Command = ViewModel.SetLanguageCommand,
                    CommandParameter = language.Id,
                });
            }
            menu.Items.Add(groupItem);
        }

        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(new NativeMenuItem("Open Grammars Folder…")
        {
            Command = ViewModel.OpenGrammarsFolderCommand,
        });
    }

    private void OnAboutClick(object? sender, EventArgs e)
        => (Avalonia.Application.Current as App)?.ShowAboutWindow();

    private void OnPluginManagerClick(object? sender, EventArgs e)
    {
        if (ViewModel is null)
            return;
        new PluginManagerWindow { DataContext = ViewModel }.ShowDialog(this);
    }

    /// <summary>NativeMenuItem.Click is a plain EventHandler, so XAML cannot bind it.</summary>
    private void WireNativeMenuClicks()
    {
        NativeMenuItem? Find(string parent, string header)
            => FindMenu(parent)?.Items.OfType<NativeMenuItem>().FirstOrDefault(i => i.Header == header);

        if (Find("Plugins", "Plugin Manager…") is { } pluginManager)
            pluginManager.Click += OnPluginManagerClick;
        if (Find("Help", "About BlankSlate") is { } about)
            about.Click += OnAboutClick;
    }


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
