using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using BlankSlate.Plugins;

namespace BlankSlate.Services;

/// <summary>A plugin that was discovered on disk, whether or not it loaded successfully.</summary>
public sealed class PluginEntry
{
    public required string Name { get; init; }
    public required string AssemblyPath { get; init; }
    public string Description { get; set; } = "";
    public IPlugin? Instance { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? Error { get; set; }

    public string Status => Error is not null ? "Failed" : IsEnabled ? "Loaded" : "Disabled";
}

/// <summary>
/// Discovers and loads plugin assemblies. Each plugin gets its own
/// <see cref="AssemblyLoadContext"/> so plugins can ship their own dependency versions,
/// while the contract assembly is deliberately shared with the host so
/// <see cref="IPlugin"/> means the same type on both sides.
/// </summary>
public static class PluginLoader
{
    private static readonly string ContractAssemblyName =
        typeof(IPlugin).Assembly.GetName().Name!;

    /// <summary>Layout: &lt;pluginsDir&gt;/&lt;PluginName&gt;/&lt;anything&gt;.dll</summary>
    public static List<PluginEntry> Discover(string pluginsDir)
    {
        var entries = new List<PluginEntry>();
        if (!Directory.Exists(pluginsDir))
            return entries;

        foreach (var dir in Directory.EnumerateDirectories(pluginsDir).OrderBy(d => d))
        {
            var folderName = Path.GetFileName(dir);
            // Prefer a DLL matching the folder name; otherwise take the only candidate.
            var candidates = Directory.GetFiles(dir, "*.dll")
                .Where(f => !Path.GetFileName(f).Equals(ContractAssemblyName + ".dll", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var assemblyPath = candidates.FirstOrDefault(f =>
                                   Path.GetFileNameWithoutExtension(f)
                                       .Equals(folderName, StringComparison.OrdinalIgnoreCase))
                               ?? candidates.FirstOrDefault();
            if (assemblyPath is null)
                continue;

            entries.Add(new PluginEntry { Name = folderName, AssemblyPath = assemblyPath });
        }
        return entries;
    }

    /// <summary>
    /// Loads the plugin type from <paramref name="entry"/>. Any failure is recorded on
    /// <see cref="PluginEntry.Error"/> instead of propagating — a bad plugin must never
    /// prevent the editor from starting.
    /// </summary>
    public static void Load(PluginEntry entry)
    {
        try
        {
            var context = new PluginLoadContext(entry.AssemblyPath);
            var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(entry.AssemblyPath));

            var pluginType = assembly.GetTypes().FirstOrDefault(t =>
                typeof(IPlugin).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });
            if (pluginType is null)
            {
                entry.Error = $"No public type implementing {nameof(IPlugin)} was found.";
                return;
            }

            if (Activator.CreateInstance(pluginType) is not IPlugin plugin)
            {
                entry.Error = $"{pluginType.FullName} could not be constructed.";
                return;
            }

            entry.Instance = plugin;
            entry.Description = SafeGet(() => plugin.Description) ?? "";
        }
        catch (ReflectionTypeLoadException ex)
        {
            entry.Error = "Type load failed: " +
                string.Join("; ", ex.LoaderExceptions.Where(e => e is not null).Select(e => e!.Message).Distinct().Take(3));
        }
        catch (Exception ex)
        {
            entry.Error = $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    /// <summary>Runs <see cref="IPlugin.Initialize"/>, recording failures on the entry.</summary>
    public static void Initialize(PluginEntry entry, IPluginHost host)
    {
        if (entry.Instance is null)
            return;
        try
        {
            entry.Instance.Initialize(host);
        }
        catch (Exception ex)
        {
            entry.Error = $"Initialize failed — {ex.GetType().Name}: {ex.Message}";
            entry.Instance = null;
        }
    }

    private static string? SafeGet(Func<string> getter)
    {
        try { return getter(); }
        catch (Exception) { return null; }
    }

    private sealed class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public PluginLoadContext(string pluginPath)
            : base(name: Path.GetFileNameWithoutExtension(pluginPath), isCollectible: false)
            => _resolver = new AssemblyDependencyResolver(pluginPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Returning null defers to the default context. The contract assembly must
            // resolve there, or the plugin's IPlugin would be a different type than ours.
            if (assemblyName.Name == ContractAssemblyName)
                return null;

            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }
    }
}
