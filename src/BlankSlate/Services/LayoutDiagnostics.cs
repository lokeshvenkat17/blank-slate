using System;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace BlankSlate.Services;

/// <summary>
/// Dumps the visual tree with bounds when BLANKSLATE_DIAG=1. Used to diagnose
/// rendering/layout problems in the real app, where headless capture is unreliable
/// for AvaloniaEdit's custom-drawn text view.
/// </summary>
public static class LayoutDiagnostics
{
    public static bool IsEnabled => Environment.GetEnvironmentVariable("BLANKSLATE_DIAG") == "1";

    public static void DumpVisualTree(Visual root, string label)
    {
        if (!IsEnabled)
            return;
        var sb = new StringBuilder();
        sb.AppendLine($"===== {label} =====");
        Walk(root, 0, sb);
        var path = Environment.GetEnvironmentVariable("BLANKSLATE_DIAG_FILE")
                   ?? Path.Combine(Path.GetTempPath(), "blankslate-diag.txt");
        File.AppendAllText(path, sb.ToString());
        Console.Error.Write(sb.ToString());
    }

    private static void Walk(Visual visual, int depth, StringBuilder sb, int maxDepth = 24)
    {
        if (depth > maxDepth)
            return;
        var name = (visual as Control)?.Name;
        var b = visual.Bounds;
        sb.AppendLine($"{new string(' ', depth * 2)}{visual.GetType().Name}" +
                      (name is null ? "" : $" #{name}") +
                      $" bounds={b.Width:F0}x{b.Height:F0}@{b.X:F0},{b.Y:F0}" +
                      $" visible={visual.IsVisible}" +
                      $" opacity={visual.Opacity:F2}");
        foreach (var child in visual.GetVisualChildren())
            Walk(child, depth + 1, sb, maxDepth);
    }
}
