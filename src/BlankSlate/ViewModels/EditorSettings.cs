using CommunityToolkit.Mvvm.ComponentModel;

namespace BlankSlate.ViewModels;

/// <summary>
/// Global view preferences shared by every open document, mirroring Notepad++'s
/// View menu (word wrap, whitespace/EOL symbols, current-line highlight, zoom).
/// </summary>
public partial class EditorSettings : ObservableObject
{
    public const double MinFontSize = 6;
    public const double MaxFontSize = 60;
    public const double DefaultFontSize = 13;

    [ObservableProperty]
    public partial bool WordWrap { get; set; }

    [ObservableProperty]
    public partial bool ShowWhitespace { get; set; }

    [ObservableProperty]
    public partial bool ShowEndOfLine { get; set; }

    [ObservableProperty]
    public partial bool HighlightCurrentLine { get; set; } = true;

    [ObservableProperty]
    public partial double FontSize { get; set; } = DefaultFontSize;

    public void ZoomIn() => FontSize = System.Math.Min(MaxFontSize, FontSize + 1);
    public void ZoomOut() => FontSize = System.Math.Max(MinFontSize, FontSize - 1);
    public void ZoomReset() => FontSize = DefaultFontSize;
}
