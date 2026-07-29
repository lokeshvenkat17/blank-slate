using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlankSlate.Models;
using BlankSlate.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlankSlate.ViewModels;

/// <summary>State and commands behind the Find/Replace/Find-in-Files dialog.</summary>
public partial class FindReplaceViewModel(MainViewModel main) : ViewModelBase
{
    private const int MaxFileSizeBytes = 20 * 1024 * 1024;
    private const int MaxResults = 10_000;

    private CancellationTokenSource? _filesSearchCts;

    [ObservableProperty] public partial string FindWhat { get; set; } = "";
    [ObservableProperty] public partial string ReplaceWith { get; set; } = "";
    [ObservableProperty] public partial bool MatchCase { get; set; }
    [ObservableProperty] public partial bool WholeWord { get; set; }
    [ObservableProperty] public partial bool WrapAround { get; set; } = true;
    [ObservableProperty] public partial SearchMode Mode { get; set; } = SearchMode.Normal;

    // Radio-button adapters for the dialog.
    public bool IsNormalMode
    {
        get => Mode == SearchMode.Normal;
        set { if (value) Mode = SearchMode.Normal; OnPropertyChanged(); }
    }
    public bool IsExtendedMode
    {
        get => Mode == SearchMode.Extended;
        set { if (value) Mode = SearchMode.Extended; OnPropertyChanged(); }
    }
    public bool IsRegexMode
    {
        get => Mode == SearchMode.Regex;
        set { if (value) Mode = SearchMode.Regex; OnPropertyChanged(); }
    }

    // Find in Files fields.
    [ObservableProperty] public partial string Directory { get; set; } = "";
    [ObservableProperty] public partial string Filters { get; set; } = "*.*";
    [ObservableProperty] public partial bool InSubfolders { get; set; } = true;
    [ObservableProperty] public partial bool IsSearching { get; set; }

    [ObservableProperty] public partial string StatusText { get; set; } = "";

    private SearchQuery BuildQuery() => new()
    {
        Pattern = FindWhat,
        Mode = Mode,
        MatchCase = MatchCase,
        WholeWord = WholeWord,
        WrapAround = WrapAround,
    };

    // ---- Find tab ----

    [RelayCommand]
    public void FindNext() => Find(backward: false);

    [RelayCommand]
    public void FindPrevious() => Find(backward: true);

    private void Find(bool backward)
    {
        var doc = main.SelectedDocument;
        if (doc?.EditorHandle is not { } handle || FindWhat.Length == 0)
            return;
        try
        {
            var text = doc.Document.Text;
            var start = backward
                ? Math.Min(handle.SelectionStart, handle.CaretOffset)
                : Math.Max(handle.SelectionStart + handle.SelectionLength, handle.CaretOffset);
            var match = SearchService.FindNext(text, BuildQuery(), start, backward);
            if (match is null)
            {
                StatusText = $"Can't find the text \"{FindWhat}\"";
                return;
            }
            handle.SelectAndReveal(match.Index, match.Length);
            StatusText = "";
        }
        catch (ArgumentException ex)
        {
            StatusText = $"Invalid regular expression: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CountOccurrences()
    {
        var doc = main.SelectedDocument;
        if (doc is null || FindWhat.Length == 0)
            return;
        try
        {
            StatusText = $"Count: {SearchService.Count(doc.Document.Text, BuildQuery())} matches";
        }
        catch (ArgumentException ex)
        {
            StatusText = $"Invalid regular expression: {ex.Message}";
        }
    }

    [RelayCommand]
    private void FindAllInCurrentDocument()
    {
        var doc = main.SelectedDocument;
        if (doc is null || FindWhat.Length == 0)
            return;
        try
        {
            var regex = SearchService.BuildRegex(BuildQuery());
            var results = new List<SearchResultItem>();
            var text = doc.Document.Text;
            foreach (var match in regex.Matches(text).Cast<System.Text.RegularExpressions.Match>())
            {
                var line = doc.Document.GetLineByOffset(match.Index);
                results.Add(new SearchResultItem(
                    doc.FilePath, doc.Title, line.LineNumber,
                    doc.Document.GetText(line.Offset, line.Length).Trim()));
                if (results.Count >= MaxResults)
                    break;
            }
            main.ShowSearchResults(results, $"Find All \"{FindWhat}\": {results.Count} hits in {doc.Title}");
            StatusText = $"{results.Count} matches";
        }
        catch (ArgumentException ex)
        {
            StatusText = $"Invalid regular expression: {ex.Message}";
        }
    }

    // ---- Replace tab ----

    [RelayCommand]
    private void ReplaceNext()
    {
        var doc = main.SelectedDocument;
        if (doc?.EditorHandle is not { } handle || FindWhat.Length == 0)
            return;
        try
        {
            // Notepad++ semantics: if the selection already matches, replace it, then find the next.
            var text = doc.Document.Text;
            var regex = SearchService.BuildRegex(BuildQuery());
            if (handle.SelectionLength > 0)
            {
                var m = regex.Match(text, handle.SelectionStart);
                if (m.Success && m.Index == handle.SelectionStart && m.Length == handle.SelectionLength)
                {
                    var replacement = SearchService.GetReplacement(m, ReplaceWith, Mode);
                    doc.Document.Replace(m.Index, m.Length, replacement);
                    handle.CaretOffset = m.Index + replacement.Length;
                }
            }
            Find(backward: false);
        }
        catch (ArgumentException ex)
        {
            StatusText = $"Invalid regular expression: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ReplaceAll()
    {
        var doc = main.SelectedDocument;
        if (doc is null || FindWhat.Length == 0)
            return;
        try
        {
            var (newText, count) = SearchService.ReplaceAll(doc.Document.Text, BuildQuery(), ReplaceWith);
            if (count > 0)
                doc.Document.Text = newText;
            StatusText = $"Replace All: {count} occurrences replaced";
        }
        catch (ArgumentException ex)
        {
            StatusText = $"Invalid regular expression: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ReplaceAllInAllOpenDocuments()
    {
        if (FindWhat.Length == 0)
            return;
        try
        {
            var total = 0;
            foreach (var doc in main.Documents)
            {
                var (newText, count) = SearchService.ReplaceAll(doc.Document.Text, BuildQuery(), ReplaceWith);
                if (count > 0)
                {
                    doc.Document.Text = newText;
                    total += count;
                }
            }
            StatusText = $"Replace All in Open Documents: {total} occurrences replaced";
        }
        catch (ArgumentException ex)
        {
            StatusText = $"Invalid regular expression: {ex.Message}";
        }
    }

    // ---- Mark tab ----

    [RelayCommand]
    private void MarkAll()
    {
        var doc = main.SelectedDocument;
        if (doc is null || FindWhat.Length == 0)
            return;
        try
        {
            doc.MarkPattern = SearchService.BuildRegex(BuildQuery());
            StatusText = $"Marked {SearchService.Count(doc.Document.Text, BuildQuery())} matches";
        }
        catch (ArgumentException ex)
        {
            StatusText = $"Invalid regular expression: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearMarks()
    {
        if (main.SelectedDocument is { } doc)
            doc.MarkPattern = null;
        StatusText = "";
    }

    // ---- Find in Files tab ----

    [RelayCommand]
    private async Task FindAllInFilesAsync()
    {
        if (FindWhat.Length == 0)
            return;
        if (!System.IO.Directory.Exists(Directory))
        {
            StatusText = "Directory doesn't exist";
            return;
        }

        System.Text.RegularExpressions.Regex regex;
        try
        {
            regex = SearchService.BuildRegex(BuildQuery());
        }
        catch (ArgumentException ex)
        {
            StatusText = $"Invalid regular expression: {ex.Message}";
            return;
        }

        _filesSearchCts?.Cancel();
        var cts = _filesSearchCts = new CancellationTokenSource();
        IsSearching = true;
        StatusText = "Searching…";
        try
        {
            var (results, fileCount) = await Task.Run(() => SearchFiles(regex, cts.Token), cts.Token);
            main.ShowSearchResults(results,
                $"Find in Files \"{FindWhat}\": {results.Count} hits in {fileCount} files");
            StatusText = $"{results.Count} hits in {fileCount} files";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Search cancelled";
        }
        finally
        {
            if (_filesSearchCts == cts)
            {
                IsSearching = false;
                _filesSearchCts = null;
            }
        }
    }

    [RelayCommand]
    private void CancelFilesSearch() => _filesSearchCts?.Cancel();

    private (List<SearchResultItem> Results, int FileCount) SearchFiles(
        System.Text.RegularExpressions.Regex regex, CancellationToken ct)
    {
        var results = new List<SearchResultItem>();
        var filesWithHits = 0;
        var patterns = Filters.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (patterns.Length == 0)
            patterns = ["*.*"];

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = InSubfolders,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        var seen = new HashSet<string>();
        foreach (var pattern in patterns)
        {
            foreach (var file in System.IO.Directory.EnumerateFiles(Directory, pattern, options))
            {
                ct.ThrowIfCancellationRequested();
                if (!seen.Add(file) || results.Count >= MaxResults)
                    continue;
                try
                {
                    var info = new FileInfo(file);
                    if (info.Length > MaxFileSizeBytes)
                        continue;
                    var bytes = File.ReadAllBytes(file);
                    if (LooksBinary(bytes))
                        continue;
                    var (_, _, text) = TextEncodings.DetectAndDecode(bytes);
                    var hadHit = false;
                    var lineNumber = 0;
                    using var reader = new StringReader(text);
                    while (reader.ReadLine() is { } lineText)
                    {
                        lineNumber++;
                        if (!regex.IsMatch(lineText))
                            continue;
                        hadHit = true;
                        results.Add(new SearchResultItem(file, Path.GetFileName(file), lineNumber, lineText.Trim()));
                        if (results.Count >= MaxResults)
                            break;
                    }
                    if (hadHit)
                        filesWithHits++;
                }
                catch (IOException) { /* locked/vanished file — skip */ }
                catch (UnauthorizedAccessException) { /* no permission — skip */ }
            }
        }
        return (results, filesWithHits);
    }

    private static bool LooksBinary(byte[] bytes)
    {
        var probe = Math.Min(bytes.Length, 8000);
        for (var i = 0; i < probe; i++)
        {
            if (bytes[i] == 0)
                return true;
        }
        return false;
    }
}
