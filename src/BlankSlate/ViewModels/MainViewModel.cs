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

    // ---- Phase 6b state ----

    [ObservableProperty]
    public partial bool IsSplitViewActive { get; set; }

    [ObservableProperty]
    public partial bool IsDocumentMapVisible { get; set; }

    [ObservableProperty]
    public partial bool IsFunctionListVisible { get; set; }

    public ObservableCollection<FunctionEntry> FunctionList { get; } = [];

    [ObservableProperty]
    public partial bool IsRecordingMacro { get; set; }

    public ObservableCollection<Macro> SavedMacros { get; } = [];

    // ---- Phase 7: plugins ----

    public PluginHost? PluginHost { get; private set; }

    public ObservableCollection<PluginEntry> Plugins { get; } = [];

    /// <summary>Folder names of plugins the user disabled; persisted in settings.json.</summary>
    private readonly HashSet<string> _disabledPlugins = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<MacroStep> _recordingSteps = [];
    private Macro? _lastMacro;
    private DispatcherTimer? _functionListTimer;
    private DocumentViewModel? _functionListDoc;

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
        WatchFunctionListDocument(newValue);
        if (newValue is not null)
            PluginHost?.RaiseActiveDocumentChanged(newValue);
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
        PluginHost?.RaiseDocumentOpened(doc);
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
        PluginHost?.RaiseDocumentSaved(doc);
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

    /// <summary>File &gt; Rename: renames the file on disk (saved docs) or retitles the tab (untitled docs).</summary>
    [RelayCommand]
    private async Task RenameAsync()
    {
        if (SelectedDocument is not { } doc || _dialogs is null)
            return;
        var newName = await _dialogs.ShowTextInputAsync("Rename", "New name:", doc.Title);
        if (newName is null || newName == doc.Title || newName.Contains(Path.DirectorySeparatorChar))
            return;

        if (doc.FilePath is { } oldPath)
        {
            var newPath = Path.Combine(Path.GetDirectoryName(oldPath)!, newName);
            if (File.Exists(newPath))
                return; // don't clobber an existing file
            try
            {
                if (File.Exists(oldPath))
                    File.Move(oldPath, newPath);
            }
            catch (IOException)
            {
                return;
            }
            RecentFiles.Remove(oldPath);
            doc.FilePath = newPath;
            doc.Title = newName;
            doc.LanguageId = SyntaxService.DetectLanguageId(newPath);
            AddRecentFile(newPath);
        }
        else
        {
            doc.Title = newName;
        }
        UpdateWindowTitle();
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
            foreach (var name in s.DisabledPlugins)
                _disabledPlugins.Add(name);
        }

        Settings.PropertyChanged += (_, _) => SaveSettings();
        LoadSavedMacros();

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
            DisabledPlugins = _disabledPlugins.ToList(),
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

    // ---- Function list (View > Function List) ----

    partial void OnIsFunctionListVisibleChanged(bool value)
    {
        if (value)
            RefreshFunctionList();
    }

    /// <summary>Re-parses the active document; debounced on text changes via <see cref="_functionListTimer"/>.</summary>
    public void RefreshFunctionList()
    {
        FunctionList.Clear();
        if (!IsFunctionListVisible || SelectedDocument is not { } doc)
            return;
        foreach (var entry in FunctionListService.GetFunctions(doc.LanguageId, doc.Document.Text))
            FunctionList.Add(entry);
    }

    /// <summary>Called from OnSelectedDocumentChanged to track the active document's edits.</summary>
    private void WatchFunctionListDocument(DocumentViewModel? doc)
    {
        if (_functionListDoc is not null)
            _functionListDoc.Document.TextChanged -= OnFunctionListTextChanged;
        _functionListDoc = doc;
        if (doc is not null)
            doc.Document.TextChanged += OnFunctionListTextChanged;
        RefreshFunctionList();
    }

    private void OnFunctionListTextChanged(object? sender, EventArgs e)
    {
        if (!IsFunctionListVisible)
            return;
        _functionListTimer ??= CreateFunctionListTimer();
        _functionListTimer.Stop();
        _functionListTimer.Start();
    }

    private DispatcherTimer CreateFunctionListTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            RefreshFunctionList();
        };
        return timer;
    }

    [RelayCommand]
    private void GoToFunction(FunctionEntry? entry)
    {
        if (entry is not null && SelectedDocument is { } doc)
            doc.RequestGoToLine(entry.Line);
    }

    // ---- Macros (Macro menu) ----

    [RelayCommand]
    private void StartMacroRecording()
    {
        _recordingSteps.Clear();
        IsRecordingMacro = true;
    }

    [RelayCommand]
    private void StopMacroRecording()
    {
        IsRecordingMacro = false;
        if (_recordingSteps.Count > 0)
            _lastMacro = new Macro { Name = "(last recorded)", Steps = [.. _recordingSteps] };
    }

    /// <summary>Called by MainWindow's tunnel handlers while recording.</summary>
    public void RecordMacroStep(MacroStep step)
    {
        if (IsRecordingMacro)
            _recordingSteps.Add(step);
    }

    [RelayCommand]
    private void PlaybackMacro() => PlayMacro(_lastMacro, 1);

    [RelayCommand]
    private void PlaySavedMacro(Macro? macro) => PlayMacro(macro, 1);

    [RelayCommand]
    private async Task RunMacroMultipleTimesAsync()
    {
        if (_lastMacro is null || _dialogs is null)
            return;
        var answer = await _dialogs.ShowTextInputAsync("Run a Macro Multiple Times", "Times to run:", "2");
        if (answer is not null && int.TryParse(answer, out var times) && times is > 0 and <= 10_000)
            PlayMacro(_lastMacro, times);
    }

    private void PlayMacro(Macro? macro, int times)
    {
        if (macro is null || IsRecordingMacro || SelectedDocument?.EditorHandle is not { } handle)
            return;
        for (var i = 0; i < times; i++)
        {
            foreach (var step in macro.Steps)
            {
                switch (step)
                {
                    case MacroTextStep t:
                        handle.ReplayText(t.Text);
                        break;
                    case MacroKeyStep k:
                        handle.ReplayKey(k.Key, k.Modifiers);
                        break;
                }
            }
        }
    }

    [RelayCommand]
    private async Task SaveCurrentMacroAsync()
    {
        if (_lastMacro is null || _dialogs is null)
            return;
        var name = await _dialogs.ShowTextInputAsync("Save Macro", "Macro name:", "My Macro");
        if (name is null)
            return;
        var existing = SavedMacros.FirstOrDefault(m => m.Name == name);
        if (existing is not null)
            SavedMacros.Remove(existing);
        SavedMacros.Add(new Macro { Name = name, Steps = [.. _lastMacro.Steps] });
        PersistMacros();
    }

    public void LoadSavedMacros()
    {
        foreach (var data in PersistenceService.LoadMacros())
        {
            SavedMacros.Add(new Macro
            {
                Name = data.Name,
                Steps = data.Steps.Select(s => s.ToStep()).ToList(),
            });
        }
    }

    // ---- Plugins (Phase 7) ----

    /// <summary>
    /// Discovers, loads and initializes plugins. Called once at startup after settings
    /// are read. A failing plugin is recorded and skipped, never fatal.
    /// </summary>
    public void LoadPlugins() => LoadPluginsFrom(PersistenceService.PluginsDir);

    /// <summary>Loads plugins from a specific folder. Separate from <see cref="LoadPlugins"/> so tests can stage a folder.</summary>
    public void LoadPluginsFrom(string pluginsDir)
    {
        PluginHost ??= new PluginHost(this);
        Plugins.Clear();
        PluginHost.Commands.Clear();

        foreach (var entry in PluginLoader.Discover(pluginsDir))
        {
            entry.IsEnabled = !_disabledPlugins.Contains(entry.Name);
            if (entry.IsEnabled)
            {
                PluginLoader.Load(entry);
                if (entry.Instance is not null)
                {
                    PluginHost.CurrentPluginName = entry.Instance.Name is { Length: > 0 } n ? n : entry.Name;
                    PluginLoader.Initialize(entry, PluginHost);
                }
            }
            Plugins.Add(entry);
        }
        OnPropertyChanged(nameof(PluginCommands));
    }

    public IReadOnlyList<PluginCommand> PluginCommands => PluginHost?.Commands ?? [];

    [RelayCommand]
    private void RunPluginCommand(PluginCommand? command)
    {
        if (command is not null)
            PluginHost?.Invoke(command);
    }

    /// <summary>Turns a plugin on/off; takes effect after a restart, like Notepad++.</summary>
    public void SetPluginEnabled(PluginEntry entry, bool enabled)
    {
        entry.IsEnabled = enabled;
        if (enabled)
            _disabledPlugins.Remove(entry.Name);
        else
            _disabledPlugins.Add(entry.Name);
        SaveSettings();
    }

    public void ShowPluginMessage(string title, string message) => _dialogs?.ShowMessage(title, message);

    [RelayCommand]
    private void OpenPluginsFolder() => OpenFolder(PersistenceService.PluginsDir, "Plugins");

    /// <summary>Language &gt; Open Grammars Folder: where users drop their own TextMate grammars.</summary>
    [RelayCommand]
    private void OpenGrammarsFolder() => OpenFolder(SyntaxService.UserGrammarsDir, "Grammars");

    private void OpenFolder(string path, string label)
    {
        try
        {
            Directory.CreateDirectory(path);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            ShowPluginMessage(label, $"Could not open the folder.\n\n{ex.Message}");
        }
    }

    private void PersistMacros()
    {
        PersistenceService.SaveMacros(SavedMacros
            .Select(m => new MacroData
            {
                Name = m.Name,
                Steps = m.Steps.Select(MacroStepData.From).ToList(),
            })
            .ToList());
    }
}
