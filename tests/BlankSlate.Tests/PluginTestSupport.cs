using System;
using System.IO;
using System.Linq;
using Xunit;

namespace BlankSlate.Tests;

/// <summary>Stages the built sample plugin into a temp folder in the layout the loader expects.</summary>
public static class PluginTestSupport
{
    public static string StageSamplePlugin()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "samples")))
            dir = dir.Parent;
        Assert.True(dir is not null, "could not locate repo root");

        var dll = Directory
            .GetFiles(Path.Combine(dir!.FullName, "samples", "TextToolsPlugin"), "TextToolsPlugin.dll",
                SearchOption.AllDirectories)
            .First();

        var root = Path.Combine(Path.GetTempPath(), "blankslate-shot-plugins-" + Guid.NewGuid().ToString("N"));
        var pluginDir = Path.Combine(root, "TextToolsPlugin");
        Directory.CreateDirectory(pluginDir);
        File.Copy(dll, Path.Combine(pluginDir, "TextToolsPlugin.dll"));
        return root;
    }
}
