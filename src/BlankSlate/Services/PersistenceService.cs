using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BlankSlate.Models;

namespace BlankSlate.Services;

/// <summary>
/// Reads/writes settings.json, session.json, and dirty-buffer backups under
/// the per-user app-data directory. All methods are best-effort: persistence
/// failures must never crash the editor.
/// </summary>
public static class PersistenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string AppDataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BlankSlate");

    public static string BackupDir { get; } = Path.Combine(AppDataDir, "backup");

    /// <summary>Where third-party plugins live: &lt;PluginsDir&gt;/&lt;PluginName&gt;/&lt;PluginName&gt;.dll</summary>
    public static string PluginsDir { get; } = Path.Combine(AppDataDir, "plugins");

    private static string SettingsPath => Path.Combine(AppDataDir, "settings.json");
    private static string SessionPath => Path.Combine(AppDataDir, "session.json");

    public static AppSettingsData? LoadSettings() => LoadJson<AppSettingsData>(SettingsPath);

    public static void SaveSettings(AppSettingsData settings) => SaveJson(SettingsPath, settings);

    public static SessionData? LoadSession() => LoadJson<SessionData>(SessionPath);

    public static void SaveSession(SessionData session) => SaveJson(SessionPath, session);

    private static string MacrosPath => Path.Combine(AppDataDir, "macros.json");

    public static List<MacroData> LoadMacros() => LoadJson<List<MacroData>>(MacrosPath) ?? [];

    public static void SaveMacros(List<MacroData> macros) => SaveJson(MacrosPath, macros);

    public static void WriteBackup(string fileName, string content)
    {
        try
        {
            Directory.CreateDirectory(BackupDir);
            File.WriteAllText(Path.Combine(BackupDir, fileName), content);
        }
        catch (Exception) { /* best effort */ }
    }

    public static string? ReadBackup(string fileName)
    {
        try
        {
            var path = Path.Combine(BackupDir, fileName);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Deletes backup files not in <paramref name="keep"/> (stale buffers from previous sessions).</summary>
    public static void CleanBackups(HashSet<string> keep)
    {
        try
        {
            if (!Directory.Exists(BackupDir))
                return;
            foreach (var file in Directory.EnumerateFiles(BackupDir))
            {
                if (!keep.Contains(Path.GetFileName(file)))
                    File.Delete(file);
            }
        }
        catch (Exception) { /* best effort */ }
    }

    private static T? LoadJson<T>(string path) where T : class
    {
        try
        {
            if (!File.Exists(path))
                return null;
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void SaveJson<T>(string path, T value)
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
        }
        catch (Exception) { /* best effort */ }
    }
}
