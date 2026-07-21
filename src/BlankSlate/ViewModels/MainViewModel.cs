using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using BlankSlate.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlankSlate.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IDialogService? _dialogs;

    public ObservableCollection<DocumentViewModel> Documents { get; } = [];

    [ObservableProperty]
    public partial DocumentViewModel? SelectedDocument { get; set; }

    [ObservableProperty]
    public partial string WindowTitle { get; set; } = "BlankSlate";

    /// <summary>Designer-only constructor.</summary>
    public MainViewModel() : this(null) { }

    public MainViewModel(IDialogService? dialogs)
    {
        _dialogs = dialogs;
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
        var doc = new DocumentViewModel();
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
}
