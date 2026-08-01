using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using BlankSlate.Models;
using BlankSlate.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlankSlate.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IDialogService? _dialogs;

    public ObservableCollection<DocumentViewModel> Documents { get; } = [];

    public EditorSettings Settings { get; } = new();

    public FindReplaceViewModel FindReplace { get; }

    public ObservableCollection<SearchResultItem> SearchResults { get; } = [];

    [ObservableProperty]
    public partial string SearchResultsHeader { get; set; } = "";

    [ObservableProperty]
    public partial bool IsSearchResultsVisible { get; set; }

    [ObservableProperty]
    public partial DocumentViewModel? SelectedDocument { get; set; }

    [ObservableProperty]
    public partial string WindowTitle { get; set; } = "BlankSlate";

    /// <summary>Designer-only constructor.</summary>
    public MainViewModel() : this(null) { }

    public MainViewModel(IDialogService? dialogs)
    {
        _dialogs = dialogs;
        FindReplace = new FindReplaceViewModel(this);
        NewFile();
    }

    partial void OnSelectedDocumentChanged(DocumentViewModel? oldValue, DocumentViewModel? newValue)
    {
        if (oldValue is not null)
            oldValue.PropertyChanged -= OnSelectedDocumentPropertyChanged;
        if (newValue is not null)
            newValue.PropertyChanged += OnSelectedDocumentPropertyChanged;
        UpdateWindowTitle();
    }

    private void OnSelectedDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DocumentViewModel.Title) or nameof(DocumentViewModel.IsDirty))
            UpdateWindowTitle();
    }

    private void UpdateWindowTitle()
    {
        WindowTitle = SelectedDocument is null
            ? "BlankSlate"
            : $"{(SelectedDocument.IsDirty ? "•" : "")}{SelectedDocument.FilePath ?? SelectedDocument.Title} — BlankSlate";
    }

    [RelayCommand]
    private void NewFile()
    {
        var doc = new DocumentViewModel { Settings = Settings };
        Documents.Add(doc);
        SelectedDocument = doc;
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        if (_dialogs is null)
            return;
        foreach (var path in await _dialogs.ShowOpenFileDialogAsync())
            await OpenPathAsync(path);
    }

    /// <summary>Opens a file by path (used by Open dialog and drag-and-drop). Re-activates the tab if already open.</summary>
    public async Task OpenPathAsync(string path)
    {
        var existing = Documents.FirstOrDefault(d => d.FilePath == path);
        if (existing is not null)
        {
            SelectedDocument = existing;
            return;
        }

        var doc = await DocumentViewModel.LoadFromFileAsync(path);
        doc.Settings = Settings;

        // Replace a pristine single untitled tab, like Notepad++ does.
        if (Documents.Count == 1 && Documents[0] is { FilePath: null, IsDirty: false } blank
            && blank.Document.TextLength == 0)
            Documents.Remove(blank);

        Documents.Add(doc);
        SelectedDocument = doc;
        UpdateWindowTitle();
    }

    [RelayCommand]
    private Task SaveAsync() => SaveDocumentAsync(SelectedDocument);

    [RelayCommand]
    private Task SaveAsAsync() => SaveDocumentAsAsync(SelectedDocument);

    [RelayCommand]
    private async Task SaveAllAsync()
    {
        foreach (var doc in Documents.ToList())
            await SaveDocumentAsync(doc);
    }

    /// <returns>true if saved; false if the user cancelled Save As.</returns>
    private async Task<bool> SaveDocumentAsync(DocumentViewModel? doc)
    {
        if (doc is null)
            return false;
        if (doc.FilePath is null)
            return await SaveDocumentAsAsync(doc);
        await doc.SaveAsync();
        UpdateWindowTitle();
        return true;
    }

    private async Task<bool> SaveDocumentAsAsync(DocumentViewModel? doc)
    {
        if (doc is null || _dialogs is null)
            return false;
        var path = await _dialogs.ShowSaveFileDialogAsync(doc.Title);
        if (path is null)
            return false;
        doc.FilePath = path;
        await doc.SaveAsync();
        UpdateWindowTitle();
        return true;
    }

    [RelayCommand]
    private async Task CloseDocumentAsync(DocumentViewModel? doc)
    {
        doc ??= SelectedDocument;
        if (doc is null)
            return;
        if (!await TryCloseDocumentAsync(doc))
            return;
        Documents.Remove(doc);
        if (Documents.Count == 0)
            NewFile(); // always keep one tab open, like Notepad++
        else
            SelectedDocument ??= Documents[^1];
    }

    [RelayCommand]
    private async Task CloseAllAsync()
    {
        foreach (var doc in Documents.ToList())
        {
            if (!await TryCloseDocumentAsync(doc))
                return;
            Documents.Remove(doc);
        }
        NewFile();
    }

    /// <summary>Prompts to save if dirty. Returns false if the user cancelled.</summary>
    private async Task<bool> TryCloseDocumentAsync(DocumentViewModel doc)
    {
        if (!doc.IsDirty || _dialogs is null)
            return true;

        SelectedDocument = doc;
        return await _dialogs.ShowConfirmSaveAsync(doc.Title) switch
        {
            SaveConfirmation.Save => await SaveDocumentAsync(doc),
            SaveConfirmation.DontSave => true,
            _ => false,
        };
    }

    /// <summary>Called from window-close. Returns false if the user cancelled.</summary>
    public async Task<bool> TryCloseAllAsync()
    {
        foreach (var doc in Documents.ToList())
        {
            if (!await TryCloseDocumentAsync(doc))
                return false;
        }
        return true;
    }

    // ---- Edit menu: routed to the focused tab's editor ----

    [RelayCommand] private void Undo() => SelectedDocument?.EditorHandle?.Undo();
    [RelayCommand] private void Redo() => SelectedDocument?.EditorHandle?.Redo();
    [RelayCommand] private void Cut() => SelectedDocument?.EditorHandle?.Cut();
    [RelayCommand] private void Copy() => SelectedDocument?.EditorHandle?.Copy();
    [RelayCommand] private void Paste() => SelectedDocument?.EditorHandle?.Paste();
    [RelayCommand] private void SelectAll() => SelectedDocument?.EditorHandle?.SelectAll();

    // ---- Edit menu: EOL conversion (per-document) ----

    [RelayCommand] private void SetEol(EolMode mode) => SelectedDocument?.ConvertEol(mode);

    // ---- Encoding menu (per-document) ----

    [RelayCommand] private void SetEncoding(TextEncodingKind kind) => SelectedDocument?.SetEncoding(kind);

    // ---- Language menu (per-document) ----

    [RelayCommand]
    private void SetLanguage(string? languageId)
    {
        if (SelectedDocument is { } doc)
            doc.LanguageId = languageId;
    }

    // ---- View menu: zoom (global) ----

    [RelayCommand] private void ZoomIn() => Settings.ZoomIn();
    [RelayCommand] private void ZoomOut() => Settings.ZoomOut();
    [RelayCommand] private void ZoomReset() => Settings.ZoomReset();

    // ---- Search menu ----

    [RelayCommand] private void ShowFind() => _dialogs?.ShowFindReplace(FindReplace, 0);
    [RelayCommand] private void ShowReplace() => _dialogs?.ShowFindReplace(FindReplace, 1);
    [RelayCommand] private void ShowMark() => _dialogs?.ShowFindReplace(FindReplace, 2);
    [RelayCommand] private void ShowFindInFiles() => _dialogs?.ShowFindReplace(FindReplace, 3);

    [RelayCommand] private void FindNext() => FindReplace.FindNext();
    [RelayCommand] private void FindPrevious() => FindReplace.FindPrevious();

    /// <summary>Notepad++ "Select and Find Next": seed the search with the current selection, then jump.</summary>
    [RelayCommand]
    private void SelectAndFindNext()
    {
        SeedFindFromSelection();
        FindReplace.FindNext();
    }

    [RelayCommand]
    private void SelectAndFindPrevious()
    {
        SeedFindFromSelection();
        FindReplace.FindPrevious();
    }

    private void SeedFindFromSelection()
    {
        if (SelectedDocument?.EditorHandle?.SelectedText is { Length: > 0 } selection)
            FindReplace.FindWhat = selection;
    }

    [RelayCommand]
    private async Task GoToLineAsync()
    {
        if (SelectedDocument is not { } doc || _dialogs is null)
            return;
        var line = await _dialogs.ShowGoToLineAsync(doc.CaretLine, doc.Document.LineCount);
        if (line is { } l)
            doc.RequestGoToLine(l);
    }

    public void ShowSearchResults(System.Collections.Generic.IEnumerable<SearchResultItem> results, string header)
    {
        SearchResults.Clear();
        foreach (var item in results)
            SearchResults.Add(item);
        SearchResultsHeader = header;
        IsSearchResultsVisible = true;
    }

    [RelayCommand]
    private void CloseSearchResults() => IsSearchResultsVisible = false;

    [RelayCommand]
    private async Task GoToSearchResultAsync(SearchResultItem? item)
    {
        if (item is null)
            return;
        if (item.FilePath is { } path)
        {
            await OpenPathAsync(path);
            SelectedDocument?.RequestGoToLine(item.LineNumber);
        }
        else
        {
            // Unsaved document: find it by tab title.
            var doc = Documents.FirstOrDefault(d => d.FilePath is null && d.Title == item.DisplayName);
            if (doc is not null)
            {
                SelectedDocument = doc;
                doc.RequestGoToLine(item.LineNumber);
            }
        }
    }

    // ---- Search menu: bookmarks ----

    [RelayCommand]
    private void ToggleBookmark()
    {
        if (SelectedDocument is { } doc)
            doc.Bookmarks.Toggle(doc.CaretLine);
    }

    [RelayCommand]
    private void NextBookmark()
    {
        if (SelectedDocument is { } doc && doc.Bookmarks.Next(doc.CaretLine) is { } line)
            doc.RequestGoToLine(line);
    }

    [RelayCommand]
    private void PreviousBookmark()
    {
        if (SelectedDocument is { } doc && doc.Bookmarks.Previous(doc.CaretLine) is { } line)
            doc.RequestGoToLine(line);
    }

    [RelayCommand]
    private void ClearBookmarks() => SelectedDocument?.Bookmarks.Clear();

    [RelayCommand]
    private void InverseBookmarks() => SelectedDocument?.Bookmarks.Inverse();

    [RelayCommand]
    private async Task CopyBookmarkedLinesAsync()
    {
        if (SelectedDocument is { EditorHandle: { } handle } doc
            && doc.GetBookmarkedLinesText() is { } text)
            await handle.SetClipboardTextAsync(text);
    }

    [RelayCommand]
    private async Task CutBookmarkedLinesAsync()
    {
        if (SelectedDocument is { EditorHandle: { } handle } doc
            && doc.GetBookmarkedLinesText() is { } text)
        {
            await handle.SetClipboardTextAsync(text);
            doc.RemoveBookmarkedLines();
        }
    }

    [RelayCommand]
    private async Task PasteToBookmarkedLinesAsync()
    {
        if (SelectedDocument is { EditorHandle: { } handle } doc
            && await handle.GetClipboardTextAsync() is { Length: > 0 } clipboard)
            doc.PasteToBookmarkedLines(clipboard);
    }

    [RelayCommand]
    private void RemoveBookmarkedLines() => SelectedDocument?.RemoveBookmarkedLines();

    [RelayCommand]
    private void RemoveNonBookmarkedLines() => SelectedDocument?.RemoveNonBookmarkedLines();
}
