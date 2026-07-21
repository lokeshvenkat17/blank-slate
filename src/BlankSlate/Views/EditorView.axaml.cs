using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using BlankSlate.ViewModels;

namespace BlankSlate.Views;

public partial class EditorView : UserControl, IEditorHandle
{
    private static readonly IBrush CurrentLineBrush = new SolidColorBrush(Color.FromArgb(28, 128, 128, 128));

    private EditorSettings? _settings;

    public EditorView()
    {
        InitializeComponent();

        Editor.TextArea.Caret.PositionChanged += (_, _) => UpdateCaretPosition();
        Editor.PointerWheelChanged += OnPointerWheelChanged;

        DataContextChanged += (_, _) =>
        {
            if (_settings is not null)
                _settings.PropertyChanged -= OnSettingsChanged;

            if (DataContext is DocumentViewModel vm)
            {
                vm.EditorHandle = this;
                _settings = vm.Settings;
                if (_settings is not null)
                {
                    _settings.PropertyChanged += OnSettingsChanged;
                    ApplySettings();
                }
                UpdateCaretPosition();
            }
        };

        DetachedFromVisualTree += (_, _) =>
        {
            if (_settings is not null)
                _settings.PropertyChanged -= OnSettingsChanged;
        };
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
        if (DataContext is not DocumentViewModel vm)
            return;
        vm.CaretLine = Editor.TextArea.Caret.Line;
        vm.CaretColumn = Editor.TextArea.Caret.Column;
    }

    public void Undo() => Editor.Undo();
    public void Redo() => Editor.Redo();
    public void Cut() => Editor.Cut();
    public void Copy() => Editor.Copy();
    public void Paste() => Editor.Paste();
    public void SelectAll() => Editor.SelectAll();
}
