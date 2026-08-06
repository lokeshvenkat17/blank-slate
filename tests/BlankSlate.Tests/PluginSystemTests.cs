using System;
using System.IO;
using System.Linq;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using BlankSlate.Plugins;
using BlankSlate.Services;
using BlankSlate.ViewModels;
using Xunit;

namespace BlankSlate.Tests;

public class PluginSystemTests
{
    /// <summary>Locates the sample plugin's build output next to the test assembly's build tree.</summary>
    private static string SamplePluginDll
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "samples")))
                dir = dir.Parent;
            Assert.True(dir is not null, "could not locate repo root from " + AppContext.BaseDirectory);
            var dll = Directory
                .GetFiles(Path.Combine(dir!.FullName, "samples", "TextToolsPlugin"), "TextToolsPlugin.dll",
                    SearchOption.AllDirectories)
                .FirstOrDefault();
            Assert.True(dll is not null, "sample plugin was not built");
            return dll!;
        }
    }

    /// <summary>Stages the built sample plugin into a temp plugins folder in the expected layout.</summary>
    private static string StageSamplePlugin()
    {
        var root = Path.Combine(Path.GetTempPath(), "blankslate-plugins-" + Guid.NewGuid().ToString("N"));
        var pluginDir = Path.Combine(root, "TextToolsPlugin");
        Directory.CreateDirectory(pluginDir);
        File.Copy(SamplePluginDll, Path.Combine(pluginDir, "TextToolsPlugin.dll"));
        return root;
    }

    [Fact]
    public void Discover_FindsPluginFolders()
    {
        var root = StageSamplePlugin();
        try
        {
            var entries = PluginLoader.Discover(root);
            Assert.Single(entries);
            Assert.Equal("TextToolsPlugin", entries[0].Name);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Discover_OnMissingFolder_ReturnsEmpty()
        => Assert.Empty(PluginLoader.Discover("/nonexistent/plugins/path"));

    /// <summary>
    /// The real end-to-end check: a plugin compiled against the contract assembly loads in
    /// its own AssemblyLoadContext and its IPlugin still matches the host's IPlugin type.
    /// </summary>
    [Fact]
    public void Load_RealPluginAssembly_ProducesUsableInstance()
    {
        var root = StageSamplePlugin();
        try
        {
            var entry = PluginLoader.Discover(root).Single();
            PluginLoader.Load(entry);

            Assert.Null(entry.Error);
            Assert.NotNull(entry.Instance);
            Assert.Equal("Text Tools", entry.Instance!.Name);
            Assert.Contains("word count", entry.Description, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Load_FolderWithoutPluginType_RecordsError()
    {
        var root = Path.Combine(Path.GetTempPath(), "blankslate-badplugin-" + Guid.NewGuid().ToString("N"));
        var dir = Path.Combine(root, "NotAPlugin");
        Directory.CreateDirectory(dir);
        // A real managed assembly that contains no IPlugin implementation.
        File.Copy(typeof(PluginLoader).Assembly.Location, Path.Combine(dir, "NotAPlugin.dll"));
        try
        {
            var entry = PluginLoader.Discover(root).Single();
            PluginLoader.Load(entry);
            Assert.Null(entry.Instance);
            Assert.NotNull(entry.Error);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [AvaloniaFact]
    public void Host_ExposesDocumentsAndRegistersCommands()
    {
        var vm = new MainViewModel(null);
        var host = new PluginHost(vm) { CurrentPluginName = "Test Plugin" };

        Assert.NotNull(host.ActiveDocument);
        Assert.Single(host.Documents);

        var ran = false;
        host.RegisterCommand("Do Thing", () => ran = true);
        Assert.Single(host.Commands);
        Assert.Equal("Test Plugin", host.Commands[0].PluginName);

        host.Invoke(host.Commands[0]);
        Assert.True(ran);
    }

    [AvaloniaFact]
    public void Host_DocumentAdapter_ReadsAndWritesText()
    {
        var vm = new MainViewModel(null);
        var host = new PluginHost(vm);
        var doc = host.ActiveDocument!;

        doc.Text = "alpha\nbeta\ngamma";
        Assert.Equal("alpha\nbeta\ngamma", doc.Text);
        Assert.Equal(3, doc.LineCount);
        Assert.Equal("beta", doc.GetLineText(2));

        doc.Insert(0, ">> ");
        Assert.StartsWith(">> alpha", doc.Text);

        doc.Replace(0, 3, "");
        Assert.StartsWith("alpha", doc.Text);

        // Same view-model document must map to the same adapter instance.
        Assert.Same(doc, host.ActiveDocument);
    }

    [AvaloniaFact]
    public void Host_FailingCommand_IsCaughtAndLogged()
    {
        var vm = new MainViewModel(null);
        var host = new PluginHost(vm) { CurrentPluginName = "Bad Plugin" };
        host.RegisterCommand("Explode", () => throw new InvalidOperationException("boom"));

        // Must not throw — a broken plugin command cannot take down the editor.
        host.Invoke(host.Commands[0]);

        Assert.Contains(host.LogLines, l => l.Contains("Explode") && l.Contains("boom"));
    }

    [AvaloniaFact]
    public void Host_RaisesDocumentSavedToPlugins()
    {
        var vm = new MainViewModel(null);
        var host = new PluginHost(vm);
        IEditorDocument? seen = null;
        host.DocumentSaved += (_, e) => seen = e.Document;

        host.RaiseDocumentSaved(vm.SelectedDocument!);
        Assert.NotNull(seen);
        Assert.Equal(vm.SelectedDocument!.Title, seen!.Title);
    }

    /// <summary>
    /// End-to-end: a real plugin assembly loads, registers commands, and those commands
    /// appear as menu items under the Plugins menu in the actual window.
    /// </summary>
    [AvaloniaFact]
    public void PluginCommands_AppearInPluginsMenu()
    {
        var window = new BlankSlate.Views.MainWindow();
        var vm = new MainViewModel(null);
        vm.LoadPluginsFrom(PluginTestSupport.StageSamplePlugin());
        window.DataContext = vm;
        window.Show();
        window.UpdateLayout();

        Assert.Single(vm.Plugins);
        Assert.Null(vm.Plugins[0].Error);
        Assert.Equal(3, vm.PluginCommands.Count);
        Assert.Contains(vm.PluginCommands, c => c.Title == "Word Count");

        // The menu now lives in the macOS system menu bar, not the visual tree.
        var pluginsMenu = Avalonia.Controls.NativeMenu.GetMenu(window)!
            .Items.OfType<Avalonia.Controls.NativeMenuItem>()
            .FirstOrDefault(m => m.Header == "Plugins");
        Assert.True(pluginsMenu is not null, "Plugins menu was not found in the native menu");

        var groups = pluginsMenu!.Menu!.Items.OfType<Avalonia.Controls.NativeMenuItem>()
            .Select(m => m.Header).ToList();
        Assert.Contains("Text Tools", groups);
    }

    /// <summary>A plugin the user disabled must not be loaded or contribute commands.</summary>
    [AvaloniaFact]
    public void DisabledPlugin_IsNotLoaded()
    {
        var vm = new MainViewModel(null);
        var root = PluginTestSupport.StageSamplePlugin();
        vm.LoadPluginsFrom(root);
        Assert.NotNull(vm.Plugins[0].Instance);

        vm.SetPluginEnabled(vm.Plugins[0], false);
        vm.LoadPluginsFrom(root);

        Assert.False(vm.Plugins[0].IsEnabled);
        Assert.Null(vm.Plugins[0].Instance);
        Assert.Empty(vm.PluginCommands);
    }
}
