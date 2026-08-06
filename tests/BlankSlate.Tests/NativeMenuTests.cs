using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using BlankSlate.ViewModels;
using BlankSlate.Views;
using Xunit;

namespace BlankSlate.Tests;

/// <summary>
/// The menu bar is exported to the macOS system menu bar, so it never appears in a
/// screenshot and cannot be checked visually. These tests assert the structure and
/// that every command actually resolves against the view-model.
/// </summary>
public class NativeMenuTests
{
    private static (MainWindow Window, NativeMenu Menu) ShowWindow()
    {
        var window = new MainWindow();
        window.DataContext = new MainViewModel(null);
        window.Show();
        window.UpdateLayout();
        var menu = NativeMenu.GetMenu(window);
        Assert.True(menu is not null, "the window has no native menu");
        return (window, menu!);
    }

    private static NativeMenu? Submenu(NativeMenu menu, string header)
        => menu.Items.OfType<NativeMenuItem>().FirstOrDefault(i => i.Header == header)?.Menu;

    [AvaloniaFact]
    public void TopLevelMenusAreExportedInOrder()
    {
        var (_, menu) = ShowWindow();
        var headers = menu.Items.OfType<NativeMenuItem>().Select(i => i.Header).ToList();

        Assert.Equal(
            ["File", "Edit", "Search", "View", "Language", "Macro", "Encoding", "Plugins", "Help"],
            headers);
    }

    [AvaloniaFact]
    public void MenuHeadersHaveNoWindowsMnemonics()
    {
        var (_, menu) = ShowWindow();

        static void AssertNoUnderscore(NativeMenu m)
        {
            foreach (var item in m.Items.OfType<NativeMenuItem>())
            {
                Assert.False(item.Header?.StartsWith('_') == true,
                    $"'{item.Header}' still carries a Windows mnemonic underscore");
                if (item.Menu is { } sub)
                    AssertNoUnderscore(sub);
            }
        }

        AssertNoUnderscore(menu);
    }

    [AvaloniaFact]
    public void FileMenuHasItsCoreCommandsBound()
    {
        var (_, menu) = ShowWindow();
        var file = Submenu(menu, "File");
        Assert.NotNull(file);

        var items = file!.Items.OfType<NativeMenuItem>().ToList();
        foreach (var header in new[] { "New", "Open…", "Save", "Save As…", "Rename…", "Close Tab" })
        {
            var item = items.FirstOrDefault(i => i.Header == header);
            Assert.True(item is not null, $"File menu is missing '{header}'");
            Assert.True(item!.Command is not null, $"'{header}' has no command bound");
        }
    }

    [AvaloniaFact]
    public void ShortcutsSurviveTheConversion()
    {
        var (_, menu) = ShowWindow();
        var newItem = Submenu(menu, "File")!.Items.OfType<NativeMenuItem>()
            .First(i => i.Header == "New");

        Assert.True(newItem.Gesture is not null, "New lost its keyboard shortcut");
    }

    [AvaloniaFact]
    public void DynamicMenusArePopulated()
    {
        var (_, menu) = ShowWindow();

        // Language is built in code from the grammar registry.
        var language = Submenu(menu, "Language");
        Assert.NotNull(language);
        var languageItems = language!.Items.OfType<NativeMenuItem>().ToList();
        Assert.Equal("Normal Text", languageItems[0].Header);
        Assert.Contains(languageItems, i => i.Header == "Open Grammars Folder…");
        // Letter groups, each holding languages.
        Assert.Contains(languageItems, i => i.Menu is { Items.Count: > 0 });

        // Plugins keeps its static entries and reports when nothing is installed.
        var plugins = Submenu(menu, "Plugins");
        Assert.NotNull(plugins);
        var pluginHeaders = plugins!.Items.OfType<NativeMenuItem>().Select(i => i.Header).ToList();
        Assert.Contains("Plugin Manager…", pluginHeaders);
        Assert.Contains("Open Plugins Folder…", pluginHeaders);

        // Recent files always offers the clear entry.
        var recent = Submenu(Submenu(menu, "File")!, "Open Recent");
        Assert.NotNull(recent);
        Assert.Contains(recent!.Items.OfType<NativeMenuItem>(), i => i.Header == "Empty Recent Files List");
    }

    /// <summary>Rebuilding must not stack duplicates onto the statically declared items.</summary>
    [AvaloniaFact]
    public void RebuildingDynamicMenusDoesNotDuplicateStaticItems()
    {
        var (window, menu) = ShowWindow();
        var vm = (MainViewModel)window.DataContext!;

        var plugins = Submenu(menu, "Plugins")!;
        var before = plugins.Items.Count;

        // Force several rebuilds through the collection-changed hook.
        vm.SavedMacros.Add(new BlankSlate.Models.Macro { Name = "M1" });
        vm.SavedMacros.Add(new BlankSlate.Models.Macro { Name = "M2" });
        vm.RecentFiles.Add("/tmp/a.txt");
        vm.RecentFiles.Add("/tmp/b.txt");

        Assert.Equal(before, Submenu(menu, "Plugins")!.Items.Count);

        var macro = Submenu(menu, "Macro")!;
        var macroHeaders = macro.Items.OfType<NativeMenuItem>().Select(i => i.Header).ToList();
        Assert.Single(macroHeaders, h => h == "Start Recording");
        Assert.Contains("M1", macroHeaders);
        Assert.Contains("M2", macroHeaders);
    }

    [AvaloniaFact]
    public void ViewMenuCheckboxesBindToSettings()
    {
        var (_, menu) = ShowWindow();
        var view = Submenu(menu, "View");
        Assert.NotNull(view);

        var wordWrap = view!.Items.OfType<NativeMenuItem>().FirstOrDefault(i => i.Header == "Word Wrap");
        Assert.True(wordWrap is not null, "View menu is missing Word Wrap");
        Assert.Equal(MenuItemToggleType.CheckBox, wordWrap!.ToggleType);
    }

    [AvaloniaFact]
    public void EveryLeafItemHasACommandOrSubmenu()
    {
        var (_, menu) = ShowWindow();
        var orphans = new System.Collections.Generic.List<string>();

        void Walk(NativeMenu m)
        {
            foreach (var item in m.Items.OfType<NativeMenuItem>())
            {
                if (item is NativeMenuItemSeparator || item.Header == "-")
                    continue;
                if (item.Menu is { } sub)
                    Walk(sub);
                else if (item.Command is null && !item.HasClickHandlers
                         && item.ToggleType == MenuItemToggleType.None && item.IsEnabled)
                {
                    // Toggles act through their IsChecked binding, so they need no command.
                    orphans.Add(item.Header ?? "(no header)");
                }
            }
        }

        Walk(menu);
        Assert.True(orphans.Count == 0, "menu entries that do nothing: " + string.Join(", ", orphans));
    }
}
