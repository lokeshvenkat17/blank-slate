using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Threading;
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

    public ObservableCollection<string> RecentFiles { get; } = [];

    private const int MaxRecentFiles = 10;

    /// <summary>When enabled (default, like Notepad++), app exit skips save prompts: dirty buffers are snapshotted and restored next launch.</summary>
    public bool SessionSnapshotEnabled { get; private set; } = true;

    private DispatcherTimer? _backupTimer;
    private int _backupIntervalSeconds = 7;

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
        AddRecentFile(path);
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
        AddRecentFile(path);
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

    // ---- Edit menu: text operations (Phase 6a) ----

    [RelayCommand] private void Delete() => SelectedDocument?.EditorHandle?.Delete();
    [RelayCommand] private void ConvertCase(CaseKind kind) => SelectedDocument?.ApplyCase(kind);
    [RelayCommand] private void LineOperation(LineOpKind kind) => SelectedDocument?.ApplyLineOp(kind);
    [RelayCommand] private void SortLines(SortKind kind) => SelectedDocument?.ApplySort(kind);
    [RelayCommand] private void BlankOperation(BlankOpKind kind) => SelectedDocument?.ApplyBlankOp(kind);
    [RelayCommand] private void CommentOperation(CommentOpKind kind) => SelectedDocument?.ApplyComment(kind);
    [RelayCommand] private void IncreaseIndent() => SelectedDocument?.ApplyIndent(increase: true);
    [RelayCommand] private void DecreaseIndent() => SelectedDocument?.ApplyIndent(increase: false);
    [RelayCommand] private void InsertDateTimeShort() => SelectedDocument?.InsertDateTime(longFormat: false);
    [RelayCommand] private void InsertDateTimeLong() => SelectedDocument?.InsertDateTime(longFormat: true);

    [RelayCommand]
    private async Task CopyToClipboardAsync(string what)
    {
        if (SelectedDocument is not { EditorHandle: { } handle } doc)
            return;
        var text = what switch
        {
            "path" => doc.FilePath,
            "name" => doc.FilePath is { } p ? Path.GetFileName(p) : doc.Title,
            "dir" => doc.FilePath is { } p ? Path.GetDirectoryName(p) : null,
            "allnames" => string.Join("\n", Documents.Select(d => d.FilePath is { } p ? Path.GetFileName(p) : d.Title)),
            "allpaths" => string.Join("\n", Documents.Where(d => d.FilePath is not null).Select(d => d.FilePath)),
            _ => null,
        };
        if (!string.IsNullOrEmpty(text))
            await handle.SetClipboardTextAsync(text);
    }

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

    // ---- Persistence: settings, session snapshot, recent files ----

    /// <summary>Loads settings, starts the periodic backup timer. Called once by App at startup.</summary>
    public void InitializePersistence()
    {
        if (PersistenceService.LoadSettings() is { } s)
        {
            Settings.WordWrap = s.WordWrap;
            Settings.ShowWhitespace = s.ShowWhitespace;
            Settings.ShowEndOfLine = s.ShowEndOfLine;
            Settings.HighlightCurrentLine = s.HighlightCurrentLine;
            Settings.FontSize = Math.Clamp(s.FontSize, EditorSettings.MinFontSize, EditorSettings.MaxFontSize);
            SessionSnapshotEnabled = s.SessionSnapshotEnabled;
            _backupIntervalSeconds = Math.Max(2, s.BackupIntervalSeconds);
            foreach (var path in s.RecentFiles.Take(MaxRecentFiles))
                RecentFiles.Add(path);
        }

        Settings.PropertyChanged += (_, _) => SaveSettings();

        _backupTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_backupIntervalSeconds) };
        _backupTimer.Tick += (_, _) => SaveSession();
        if (SessionSnapshotEnabled)
            _backupTimer.Start();
    }

    private void SaveSettings()
    {
        PersistenceService.SaveSettings(new AppSettingsData
        {
            WordWrap = Settings.WordWrap,
            ShowWhitespace = Settings.ShowWhitespace,
            ShowEndOfLine = Settings.ShowEndOfLine,
            HighlightCurrentLine = Settings.HighlightCurrentLine,
            FontSize = Settings.FontSize,
            RecentFiles = RecentFiles.ToList(),
            SessionSnapshotEnabled = SessionSnapshotEnabled,
            BackupIntervalSeconds = _backupIntervalSeconds,
        });
    }

    /// <summary>Snapshots all tabs (dirty buffers to backup files) into session.json.</summary>
    public void SaveSession()
    {
        var data = new SessionData
        {
            SelectedIndex = SelectedDocument is null ? 0 : Documents.IndexOf(SelectedDocument),
        };
        var keep = new HashSet<string>();
        for (var i = 0; i < Documents.Count; i++)
        {
            var doc = Documents[i];
            var entry = new SessionDocumentData
            {
                FilePath = doc.FilePath,
                Title = doc.Title,
                LanguageId = doc.LanguageId,
                EncodingKind = doc.EncodingKind,
                EolMode = doc.EolMode,
                CaretLine = doc.CaretLine,
            };
            // Snapshot content that isn't safely on disk: dirty buffers and non-empty untitled tabs.
            if (doc.IsDirty || (doc.FilePath is null && doc.Document.TextLength > 0))
            {
                entry.BackupFile = $"buffer-{i}.txt";
                PersistenceService.WriteBackup(entry.BackupFile, doc.Document.Text);
                keep.Add(entry.BackupFile);
            }
            data.Documents.Add(entry);
        }
        PersistenceService.CleanBackups(keep);
        PersistenceService.SaveSession(data);
    }

    /// <summary>Restores the previous session's tabs. Called once by App after the window is shown.</summary>
    public async Task RestoreSessionAsync()
    {
        if (!SessionSnapshotEnabled || PersistenceService.LoadSession() is not { Documents.Count: > 0 } session)
            return;

        var restored = new List<DocumentViewModel>();
        foreach (var entry in session.Documents)
        {
            try
            {
                if (await RestoreDocumentAsync(entry) is { } doc)
                    restored.Add(doc);
            }
            catch (Exception ex)
            {
                // Skip unrestorable buffers rather than fail launch.
                Console.Error.WriteLine($"Could not restore '{entry.Title}': {ex.Message}");
            }
        }
        if (restored.Count == 0)
            return;

        // Bump the untitled counter past restored "new N" titles.
        foreach (var doc in restored)
        {
            if (doc.FilePath is null && Regex.Match(doc.Title, @"^new (\d+)$") is { Success: true } m)
                DocumentViewModel.EnsureUntitledCounterAtLeast(int.Parse(m.Groups[1].Value));
        }

        // Replace the pristine startup tab with the restored set.
        var startupBlank = Documents.Count == 1
            && Documents[0] is { FilePath: null, IsDirty: false } b && b.Document.TextLength == 0;
        if (startupBlank)
            Documents.Clear();
        foreach (var doc in restored)
            Documents.Add(doc);
        SelectedDocument = Documents[Math.Clamp(session.SelectedIndex, 0, Documents.Count - 1)];
    }

    private async Task<DocumentViewModel?> RestoreDocumentAsync(SessionDocumentData entry)
    {
        DocumentViewModel doc;
        if (entry.BackupFile is not null && PersistenceService.ReadBackup(entry.BackupFile) is { } content)
        {
            doc = new DocumentViewModel { Settings = Settings };
            doc.Document.Text = content;
            doc.Document.UndoStack.ClearAll();
            doc.FilePath = entry.FilePath;
            if (entry.Title is not null)
                doc.Title = entry.Title;
            doc.IsDirty = true; // content is not (or may not be) what's on disk
        }
        else if (entry.FilePath is not null && File.Exists(entry.FilePath))
        {
            doc = await DocumentViewModel.LoadFromFileAsync(entry.FilePath);
            doc.Settings = Settings;
            doc.EncodingKind = entry.EncodingKind;
            doc.EolMode = entry.EolMode;
        }
        else
        {
            return null;
        }
        doc.LanguageId = entry.LanguageId
            ?? (doc.FilePath is null ? null : SyntaxService.DetectLanguageId(doc.FilePath));
        doc.PendingCaretLine = entry.CaretLine;
        return doc;
    }

    private void AddRecentFile(string path)
    {
        RecentFiles.Remove(path);
        RecentFiles.Insert(0, path);
        while (RecentFiles.Count > MaxRecentFiles)
            RecentFiles.RemoveAt(RecentFiles.Count - 1);
        SaveSettings();
    }

    [RelayCommand]
    private async Task OpenRecentFileAsync(string? path)
    {
        if (path is null)
            return;
        if (!File.Exists(path))
        {
            RecentFiles.Remove(path);
            SaveSettings();
            return;
        }
        await OpenPathAsync(path);
    }

    [RelayCommand]
    private void ClearRecentFiles()
    {
        RecentFiles.Clear();
        SaveSettings();
    }
}
