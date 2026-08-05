using System.Text;
using BlankSlate.Plugins;

namespace TextTools;

/// <summary>
/// Reference plugin showing the BlankSlate plugin API: menu commands, reading the
/// active document, editing text, and reacting to editor events.
/// </summary>
public sealed class TextToolsPlugin : IPlugin
{
    private IPluginHost? _host;

    public string Name => "Text Tools";

    public string Description => "Sample plugin: word count, sort selected lines, and wrap selection in quotes.";

    public void Initialize(IPluginHost host)
    {
        _host = host;

        host.RegisterCommand("Word Count", WordCount);
        host.RegisterCommand("Sort Selected Lines", SortSelectedLines);
        host.RegisterCommand("Wrap Selection in Quotes", WrapInQuotes);

        host.DocumentSaved += (_, e) => host.Log($"saved {e.Document.Title}");
    }

    private void WordCount()
    {
        if (_host?.ActiveDocument is not { } doc)
            return;

        var text = doc.SelectedText.Length > 0 ? doc.SelectedText : doc.Text;
        var scope = doc.SelectedText.Length > 0 ? "Selection" : "Document";
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        var chars = text.Length;
        var charsNoSpace = text.Count(c => !char.IsWhiteSpace(c));
        var lines = text.Length == 0 ? 0 : text.Split('\n').Length;

        _host.ShowMessage("Word Count",
            $"{scope} — {doc.Title}\n\n" +
            $"Words: {words}\n" +
            $"Characters: {chars}\n" +
            $"Characters (no spaces): {charsNoSpace}\n" +
            $"Lines: {lines}");
    }

    private void SortSelectedLines()
    {
        if (_host?.ActiveDocument is not { } doc || doc.SelectionLength == 0)
        {
            _host?.ShowMessage("Sort Selected Lines", "Select two or more lines first.");
            return;
        }

        var start = doc.SelectionStart;
        var length = doc.SelectionLength;
        var lines = doc.GetText(start, length).Replace("\r\n", "\n").Split('\n');
        Array.Sort(lines, StringComparer.OrdinalIgnoreCase);
        var sorted = string.Join("\n", lines);

        doc.Replace(start, length, sorted);
        doc.Select(start, sorted.Length);
        _host.Log($"sorted {lines.Length} lines in {doc.Title}");
    }

    private void WrapInQuotes()
    {
        if (_host?.ActiveDocument is not { } doc || doc.SelectionLength == 0)
        {
            _host?.ShowMessage("Wrap in Quotes", "Select some text first.");
            return;
        }

        var start = doc.SelectionStart;
        var length = doc.SelectionLength;
        var wrapped = new StringBuilder().Append('"').Append(doc.GetText(start, length)).Append('"').ToString();
        doc.Replace(start, length, wrapped);
        doc.Select(start, wrapped.Length);
    }
}
