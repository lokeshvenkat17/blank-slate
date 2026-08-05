# Writing a BlankSlate plugin

BlankSlate plugins are ordinary .NET class libraries. Notepad++ plugins are Win32 DLLs
and are **not** compatible — this is a fresh .NET API.

> Plugins run in-process with full trust. Only install plugins you trust.

## 1. Create a class library

```bash
dotnet new classlib -o MyPlugin
cd MyPlugin
dotnet add reference path/to/BlankSlate.PluginApi.csproj   # or the shipped DLL
```

Reference the contract **without copying it** — the host supplies it at runtime:

```xml
<ProjectReference Include="../BlankSlate.PluginApi/BlankSlate.PluginApi.csproj" Private="false" />
```

## 2. Implement `IPlugin`

```csharp
using BlankSlate.Plugins;

public sealed class MyPlugin : IPlugin
{
    public string Name => "My Plugin";
    public string Description => "Does something useful.";

    public void Initialize(IPluginHost host)
    {
        host.RegisterCommand("Shout", () =>
        {
            if (host.ActiveDocument is { } doc && doc.SelectionLength > 0)
                doc.Replace(doc.SelectionStart, doc.SelectionLength,
                            doc.SelectedText.ToUpperInvariant());
        });

        host.DocumentSaved += (_, e) => host.Log($"saved {e.Document.Title}");
    }
}
```

The type must be public, non-abstract, and have a public parameterless constructor.

## 3. Install it

Build, then copy the DLL into its own folder under the plugins directory:

```
~/Library/Application Support/BlankSlate/plugins/
└── MyPlugin/
    └── MyPlugin.dll
```

**Plugins → Open Plugins Folder…** opens this location. Restart BlankSlate; your
commands appear under **Plugins → My Plugin**.

## API surface

### `IPluginHost`

| Member | Purpose |
|---|---|
| `ActiveDocument` | Document in the focused tab, or `null` |
| `Documents` | All open documents, in tab order |
| `RegisterCommand(title, action)` | Adds an entry under Plugins → *your plugin* |
| `ShowMessage(title, message)` | Informational dialog |
| `Log(message)` | Writes to the plugin log |
| `DocumentOpened` / `DocumentSaved` / `ActiveDocumentChanged` | Editor events |

### `IEditorDocument`

Read: `FilePath`, `Title`, `LanguageId`, `IsDirty`, `Text`, `TextLength`, `LineCount`,
`CaretLine`, `CaretOffset`, `SelectedText`, `SelectionStart`, `SelectionLength`,
`GetLineText(line)`, `GetText(offset, length)`.

Write: `Text` (setter), `Insert`, `Replace`, `Remove`, `Select`, `GoToLine`.

Line numbers are **1-based**; offsets are **0-based** character positions.

## Error handling

A plugin that throws is contained, not fatal:

- Load or `Initialize` failures are shown in **Plugins → Plugin Manager…** with the message.
- A command that throws is caught, logged, and reported in a dialog.

Each plugin loads in its own `AssemblyLoadContext`, so it can ship its own dependency
versions. The contract assembly is deliberately shared with the host so `IPlugin` refers
to the same type on both sides.

## Working example

See [`samples/TextToolsPlugin`](../samples/TextToolsPlugin) — word count, sort selected
lines, and wrap selection in quotes.

```bash
dotnet build samples/TextToolsPlugin -c Release
mkdir -p ~/Library/Application\ Support/BlankSlate/plugins/TextToolsPlugin
cp samples/TextToolsPlugin/bin/Release/net10.0/TextToolsPlugin.dll \
   ~/Library/Application\ Support/BlankSlate/plugins/TextToolsPlugin/
```
