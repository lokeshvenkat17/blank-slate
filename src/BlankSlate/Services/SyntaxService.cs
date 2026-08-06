using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TextMateSharp.Grammars;
using TextMateSharp.Themes;

namespace BlankSlate.Services;

/// <summary>
/// Shared TextMate grammar registry: language list, extension detection,
/// and light/dark theme loading for every editor instance.
///
/// Three grammar sources are merged, in order:
///   1. TextMateSharp.Grammars (64 languages shipped with the package)
///   2. Grammars/ next to the executable — extra languages authored for BlankSlate
///   3. ~/Library/Application Support/BlankSlate/grammars — the user's own grammars,
///      which is BlankSlate's equivalent of Notepad++'s User Defined Languages
/// Both extra folders use the VS Code extension layout: a package.json declaring
/// contributes.languages / contributes.grammars, plus the grammar files it points at.
/// </summary>
public static class SyntaxService
{
    /// <summary>Single registry shared by all editors (grammar parsing is expensive).</summary>
    public static RegistryOptions Registry { get; }

    /// <summary>Grammar folders that failed to load, as (folder, error) — surfaced in the UI.</summary>
    public static IReadOnlyList<(string Folder, string Error)> LoadErrors => _loadErrors;

    private static readonly List<(string, string)> _loadErrors = [];

    /// <summary>Folder the user can drop extra grammars into.</summary>
    public static string UserGrammarsDir => Path.Combine(PersistenceService.AppDataDir, "grammars");

    /// <summary>Grammars shipped inside the app bundle.</summary>
    public static string BundledGrammarsDir => Path.Combine(AppContext.BaseDirectory, "Grammars");

    static SyntaxService()
    {
        Registry = new RegistryOptions(ThemeName.LightPlus);
        LoadExtraGrammars(BundledGrammarsDir);
        LoadExtraGrammars(UserGrammarsDir);
        Languages = Registry.GetAvailableLanguages()
            .OrderBy(GetDisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Registers every grammar pack inside <paramref name="dir"/>. LoadFromLocalDir scans
    /// sub-folders for VS Code extension packages, so <paramref name="dir"/> is the parent
    /// and each pack lives in its own sub-folder with a package.json.
    /// A malformed or missing folder is recorded and skipped — bad grammars must never
    /// stop the editor from starting.
    /// </summary>
    private static void LoadExtraGrammars(string dir)
    {
        try
        {
            if (!Directory.Exists(dir))
                return;
            Registry.LoadFromLocalDir(dir, true);
        }
        catch (Exception ex)
        {
            _loadErrors.Add((dir, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    /// <summary>All available languages sorted by display name.</summary>
    public static IReadOnlyList<Language> Languages { get; }

    public static string GetDisplayName(Language language)
        => language.Aliases is { Count: > 0 } aliases ? aliases[0] : language.Id;

    /// <summary>Language id for a file path, or null for plain text.</summary>
    public static string? DetectLanguageId(string filePath)
    {
        // Whole-name matches win over the extension, so CMakeLists.txt is CMake, not text.
        var fileName = Path.GetFileName(filePath);
        var byName = Languages.FirstOrDefault(l =>
            l.Extensions?.Any(e => e.Equals(fileName, StringComparison.OrdinalIgnoreCase)) == true);
        if (byName is not null)
            return byName.Id;

        var extension = Path.GetExtension(filePath);
        return string.IsNullOrEmpty(extension) ? null : Registry.GetLanguageByExtension(extension)?.Id;
    }

    public static string? GetScope(string? languageId)
        => languageId is null ? null : Registry.GetScopeByLanguageId(languageId);

    public static string? GetDisplayNameById(string? languageId)
    {
        if (languageId is null)
            return null;
        var language = Languages.FirstOrDefault(l => l.Id == languageId);
        return language is null ? languageId : GetDisplayName(language);
    }

    public static IRawTheme LoadTheme(bool dark)
        => Registry.LoadTheme(dark ? ThemeName.DarkPlus : ThemeName.LightPlus);
}
