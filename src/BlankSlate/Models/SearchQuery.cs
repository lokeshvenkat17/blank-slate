namespace BlankSlate.Models;

/// <summary>Search modes matching Notepad++'s Find dialog: Normal, Extended (\n, \t, \x..), Regular expression.</summary>
public enum SearchMode { Normal, Extended, Regex }

public sealed record SearchQuery
{
    public required string Pattern { get; init; }
    public SearchMode Mode { get; init; } = SearchMode.Normal;
    public bool MatchCase { get; init; }
    public bool WholeWord { get; init; }
    public bool WrapAround { get; init; } = true;
}
