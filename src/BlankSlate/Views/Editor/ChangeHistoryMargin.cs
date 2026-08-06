using System;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using BlankSlate.Models;

namespace BlankSlate.Views.Editor;

/// <summary>
/// Notepad++'s change-history strip: an orange bar on lines edited since the file was
/// opened, green once those edits have been saved.
/// </summary>
public sealed class ChangeHistoryMargin : AbstractMargin
{
    private const double MarginWidth = 5;

    private static readonly IBrush ModifiedBrush = new SolidColorBrush(Color.FromRgb(0xE8, 0xA0, 0x3C));
    private static readonly IBrush SavedBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));

    private ChangeHistory? _history;

    public ChangeHistory? History
    {
        get => _history;
        set
        {
            if (_history is not null)
                _history.Changed -= OnHistoryChanged;
            _history = value;
            if (_history is not null)
                _history.Changed += OnHistoryChanged;
            InvalidateVisual();
        }
    }

    private void OnHistoryChanged(object? sender, EventArgs e) => InvalidateVisual();

    protected override Size MeasureOverride(Size availableSize) => new(MarginWidth, 0);

    protected override void OnTextViewChanged(TextView? oldTextView, TextView? newTextView)
    {
        if (oldTextView is not null)
            oldTextView.VisualLinesChanged -= OnVisualLinesChanged;
        base.OnTextViewChanged(oldTextView, newTextView);
        if (newTextView is not null)
            newTextView.VisualLinesChanged += OnVisualLinesChanged;
    }

    private void OnVisualLinesChanged(object? sender, EventArgs e) => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        if (History is null || TextView is not { VisualLinesValid: true } textView)
            return;

        foreach (var visualLine in textView.VisualLines)
        {
            var state = History.GetLineState(visualLine.FirstDocumentLine.LineNumber);
            if (state == ChangeState.None)
                continue;

            var top = visualLine.VisualTop - textView.VerticalOffset;
            context.FillRectangle(
                state == ChangeState.Modified ? ModifiedBrush : SavedBrush,
                new Rect(1, top, MarginWidth - 2, visualLine.Height));
        }
    }
}
