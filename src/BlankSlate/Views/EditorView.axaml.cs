using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using AvaloniaEdit.TextMate;
using BlankSlate.Services;
using BlankSlate.ViewModels;
using BlankSlate.Views.Editor;

namespace BlankSlate.Views;

public partial class EditorView : UserControl, IEditorHandle
{
    private static readonly IBrush CurrentLineBrush = new SolidColorBrush(Color.FromArgb(28, 128, 128, 128));

    private readonly BookmarkMargin _bookmarkMargin = new();
    private readonly MarkAllColorizer _markColorizer = new();
    private readonly TextMate.Installation _textMate;
    private EditorSettings? _settings;
    private DocumentViewModel? _documentVm;

    public EditorView()
    {
        InitializeComponent();

        Editor.TextArea.LeftMargins.Insert(0, _bookmarkMargin);
        Editor.TextArea.TextView.LineTransformers.Add(_markColorizer);

        _textMate = Editor.InstallTextMate(SyntaxService.Registry);
        ApplyEditorTheme();
        if (Application.Current is { } app)
            app.ActualThemeVariantChanged += OnAppThemeChanged;

        Editor.TextArea.Caret.PositionChanged += (_, _) => UpdateCaretPosition();
        Editor.PointerWheelChanged += OnPointerWheelChanged;

        DataContextChanged += (_, _) => OnDataContextSwitched();

        // Focus the editor whenever the tab becomes visible so the caret is
        // immediately blinking and typing just works.
        AttachedToVisualTree += (_, _) =>
        {
            if (IsPrimary)
                Avalonia.Threading.Dispatcher.UIThread.Post(() => Editor.Focus(),
                    Avalonia.Threading.DispatcherPriority.Input);
        };

        DetachedFromVisualTree += (_, _) =>
        {
            if (_settings is not null)
                _settings.PropertyChanged -= OnSettingsChanged;
            if (_documentVm is not null)
                _documentVm.PropertyChanged -= OnDocumentPropertyChanged;
            if (Application.Current is { } application)
                application.ActualThemeVariantChanged -= OnAppThemeChanged;
        };
    }

    private void OnAppThemeChanged(object? sender, EventArgs e) => ApplyEditorTheme();

    private void ApplyEditorTheme()
    {
        var dark = Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
        _textMate.SetTheme(SyntaxService.LoadTheme(dark));
        // TextMate themes only recolor the text; caret, line numbers, and selection
        // need explicit brushes or they vanish against the dark background.
        Editor.TextArea.CaretBrush = dark ? Brushes.White : Brushes.Black;
        Editor.LineNumbersForeground = new SolidColorBrush(dark
            ? Color.FromRgb(0x85, 0x85, 0x85)
            : Color.FromRgb(0x9A, 0x9A, 0x9A));
        Editor.TextArea.SelectionBrush = new SolidColorBrush(dark
            ? Color.FromArgb(0x70, 0x26, 0x4F, 0x78)
            : Color.FromArgb(0x60, 0xAD, 0xD6, 0xFF));
    }

    private void ApplyGrammar()
    {
        try
        {
            _textMate.SetGrammar(SyntaxService.GetScope(_documentVm?.LanguageId));
        }
        catch (Exception)
        {
            // A broken grammar must never take down the editor; fall back to plain text.
            _textMate.SetGrammar(null);
        }
    }

    /// <summary>False for the split-view clone: it shares the document but must not claim the
    /// document's editor handle or drive caret state.</summary>
    public bool IsPrimary { get; set; } = true;

    private void OnDataContextSwitched()
    {
        if (_settings is not null)
            _settings.PropertyChanged -= OnSettingsChanged;
        if (_documentVm is not null)
            _documentVm.PropertyChanged -= OnDocumentPropertyChanged;

        if (DataContext is not DocumentViewModel vm)
            return;

        _documentVm = vm;
        if (IsPrimary)
            vm.EditorHandle = this;
        vm.PropertyChanged += OnDocumentPropertyChanged;

        _bookmarkMargin.Bookmarks = vm.Bookmarks;
        _markColorizer.Pattern = vm.MarkPattern;
        ApplyGrammar();
        Editor.TextArea.TextView.Redraw();

        _settings = vm.Settings;
        if (_settings is not null)
        {
            _settings.PropertyChanged += OnSettingsChanged;
            ApplySettings();
        }

        if (!IsPrimary)
            return;

        UpdateCaretPosition();

        if (vm.PendingCaretLine is { } line)
        {
            vm.PendingCaretLine = null;
            GoToLine(line);
        }
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentViewModel.MarkPattern))
        {
            _markColorizer.Pattern = _documentVm?.MarkPattern;
            Editor.TextArea.TextView.Redraw();
        }
        else if (e.PropertyName == nameof(DocumentViewModel.LanguageId))
        {
            ApplyGrammar();
        }
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e) => ApplySettings();

    private void ApplySettings()
    {
        if (_settings is null)
            return;
        Editor.WordWrap = _settings.WordWrap;
        Editor.FontSize = _settings.FontSize;
        Editor.Options.ShowSpaces = _settings.ShowWhitespace;
        Editor.Options.ShowTabs = _settings.ShowWhitespace;
        Editor.Options.ShowEndOfLine = _settings.ShowEndOfLine;
        Editor.Options.HighlightCurrentLine = _settings.HighlightCurrentLine;
        Editor.TextArea.TextView.CurrentLineBackground = _settings.HighlightCurrentLine ? CurrentLineBrush : null;
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_settings is null || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;
        _settings.FontSize = Math.Clamp(_settings.FontSize + (e.Delta.Y > 0 ? 1 : -1),
            EditorSettings.MinFontSize, EditorSettings.MaxFontSize);
        e.Handled = true;
    }

    private void UpdateCaretPosition()
    {
        if (!IsPrimary || DataContext is not DocumentViewModel vm)
            return;
        vm.CaretLine = Editor.TextArea.Caret.Line;
        vm.CaretColumn = Editor.TextArea.Caret.Column;
    }

    // ---- IEditorHandle ----

    public void Undo() => Editor.Undo();
    public void Redo() => Editor.Redo();
    public void Cut() => Editor.Cut();
    public void Copy() => Editor.Copy();
    public void Paste() => Editor.Paste();
    public void Delete() => Editor.Delete();
    public void SelectAll() => Editor.SelectAll();

    public string? SelectedText => Editor.SelectedText;

    public int CaretOffset
    {
        get => Editor.CaretOffset;
        set => Editor.CaretOffset = Math.Clamp(value, 0, Editor.Document.TextLength);
    }

    public int SelectionStart => Editor.SelectionStart;
    public int SelectionLength => Editor.SelectionLength;

    public void SelectAndReveal(int start, int length)
    {
        Editor.Select(start, length);
        var location = Editor.Document.GetLocation(start);
        Editor.ScrollTo(location.Line, location.Column);
        Editor.Focus();
    }

    public void GoToLine(int line)
    {
        line = Math.Clamp(line, 1, Editor.Document.LineCount);
        var docLine = Editor.Document.GetLineByNumber(line);
        Editor.CaretOffset = docLine.Offset;
        Editor.ScrollTo(line, 1);
        Editor.Focus();
    }

    public async Task SetClipboardTextAsync(string text)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(text);
    }

    public async Task<string?> GetClipboardTextAsync()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            return await clipboard.TryGetTextAsync();
        return null;
    }

    // ---- Macro replay: re-dispatch recorded input onto the text area ----

    public void ReplayText(string text)
    {
        Editor.TextArea.RaiseEvent(new TextInputEventArgs
        {
            RoutedEvent = TextInputEvent,
            Source = Editor.TextArea,
            Text = text,
        });
    }

    public void ReplayKey(Key key, KeyModifiers modifiers)
    {
        Editor.TextArea.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = KeyDownEvent,
            Source = Editor.TextArea,
            Key = key,
            KeyModifiers = modifiers,
        });
    }
}
