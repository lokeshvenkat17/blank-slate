using System.Text.RegularExpressions;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace BlankSlate.Views.Editor;

/// <summary>Highlights every match of the active "Mark All" pattern (Notepad++ red-mark style).</summary>
public sealed class MarkAllColorizer : DocumentColorizingTransformer
{
    private static readonly IBrush MarkBrush = new SolidColorBrush(Color.FromArgb(0x60, 0xE5, 0x3E, 0x3E));

    public Regex? Pattern { get; set; }

    protected override void ColorizeLine(DocumentLine line)
    {
        if (Pattern is null || line.Length == 0)
            return;
        var text = CurrentContext.Document.GetText(line.Offset, line.Length);
        foreach (Match match in Pattern.Matches(text))
        {
            if (match.Length == 0)
                continue;
            ChangeLinePart(
                line.Offset + match.Index,
                line.Offset + match.Index + match.Length,
                element => element.TextRunProperties.SetBackgroundBrush(MarkBrush));
        }
    }
}
