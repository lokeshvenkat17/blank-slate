using System;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using BlankSlate.Models;

namespace BlankSlate.Views.Editor;

/// <summary>Paints the five Notepad++ token-styling colours behind marked ranges.</summary>
public sealed class StyleMarkColorizer : DocumentColorizingTransformer
{
    /// <summary>Notepad++'s five styles: green, cyan, orange, purple, pink.</summary>
    public static readonly Color[] StyleColors =
    [
        Color.FromRgb(0x9C, 0xE8, 0x9C),
        Color.FromRgb(0x9C, 0xD8, 0xE8),
        Color.FromRgb(0xF5, 0xC9, 0x8B),
        Color.FromRgb(0xD8, 0xB4, 0xEE),
        Color.FromRgb(0xF5, 0xA9, 0xC8),
    ];

    private static readonly IBrush[] Brushes =
    [
        new SolidColorBrush(StyleColors[0], 0.55),
        new SolidColorBrush(StyleColors[1], 0.55),
        new SolidColorBrush(StyleColors[2], 0.55),
        new SolidColorBrush(StyleColors[3], 0.55),
        new SolidColorBrush(StyleColors[4], 0.55),
    ];

    public StyleMarkSet? Marks { get; set; }

    protected override void ColorizeLine(DocumentLine line)
    {
        if (Marks is null || line.Length == 0)
            return;

        for (var style = 0; style < StyleMarkSet.StyleCount; style++)
        {
            var brush = Brushes[style];
            foreach (var segment in Marks.GetOverlapping(style, line.Offset, line.Length))
            {
                var start = Math.Max(segment.StartOffset, line.Offset);
                var end = Math.Min(segment.EndOffset, line.EndOffset);
                if (end > start)
                    ChangeLinePart(start, end, el => el.TextRunProperties.SetBackgroundBrush(brush));
            }
        }
    }
}
