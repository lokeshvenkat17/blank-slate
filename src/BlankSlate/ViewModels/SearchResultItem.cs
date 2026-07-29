namespace BlankSlate.ViewModels;

/// <summary>One row in the search-results panel (Find All / Find in Files).</summary>
public sealed record SearchResultItem(
    string? FilePath,      // null = unsaved document (navigate by DisplayName match)
    string DisplayName,    // file name or tab title
    int LineNumber,
    string LineText)
{
    public string Location => $"{DisplayName} (line {LineNumber})";
}
