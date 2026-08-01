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
/// </summary>
public static class SyntaxService
{
    /// <summary>Single registry shared by all editors (grammar parsing is expensive).</summary>
    public static RegistryOptions Registry { get; } = new(ThemeName.LightPlus);

    /// <summary>All available languages sorted by display name.</summary>
    public static IReadOnlyList<Language> Languages { get; } =
        Registry.GetAvailableLanguages()
            .OrderBy(GetDisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string GetDisplayName(Language language)
        => language.Aliases is { Count: > 0 } aliases ? aliases[0] : language.Id;

    /// <summary>Language id for a file path based on its extension, or null for plain text.</summary>
    public static string? DetectLanguageId(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(extension))
            return null;
        return Registry.GetLanguageByExtension(extension)?.Id;
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
