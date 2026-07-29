using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using BlankSlate.Models;

namespace BlankSlate.Views.Editor;

/// <summary>Gutter margin showing bookmark dots; click a line to toggle its bookmark (Notepad++ style).</summary>
public sealed class BookmarkMargin : AbstractMargin
{
    private const double MarginWidth = 18;
    private static readonly IBrush DotBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x75, 0xD4));

    private BookmarkManager? _bookmarks;

    public BookmarkManager? Bookmarks
    {
        get => _bookmarks;
        set
        {
            if (_bookmarks is not null)
                _bookmarks.Changed -= OnBookmarksChanged;
            _bookmarks = value;
            if (_bookmarks is not null)
                _bookmarks.Changed += OnBookmarksChanged;
            InvalidateVisual();
        }
    }

    private void OnBookmarksChanged(object? sender, EventArgs e) => InvalidateVisual();

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
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (Bookmarks is null || TextView is not { VisualLinesValid: true } textView)
            return;

        foreach (var visualLine in textView.VisualLines)
        {
            var lineNumber = visualLine.FirstDocumentLine.LineNumber;
            if (!Bookmarks.Contains(lineNumber))
                continue;
            var y = visualLine.GetTextLineVisualYPosition(visualLine.TextLines[0], VisualYPosition.LineMiddle)
                    - textView.VerticalOffset;
            context.DrawEllipse(DotBrush, null, new Point(MarginWidth / 2, y), 5, 5);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Bookmarks is null || TextView is not { VisualLinesValid: true } textView)
            return;
        var y = e.GetPosition(this).Y + textView.VerticalOffset;
        foreach (var visualLine in textView.VisualLines)
        {
            if (y >= visualLine.VisualTop && y < visualLine.VisualTop + visualLine.Height)
            {
                Bookmarks.Toggle(visualLine.FirstDocumentLine.LineNumber);
                e.Handled = true;
                return;
            }
        }
    }
}
