# BlankSlate

A cross-platform (macOS-first) text editor inspired by [Notepad++](https://github.com/notepad-plus-plus/notepad-plus-plus), built with .NET and [Avalonia UI](https://avaloniaui.net/).

## Why

Notepad++ is Windows-only. BlankSlate brings the Notepad++ feature set to macOS (and Linux/Windows) from a single .NET codebase.

## Tech stack

- **.NET 10** / C#
- **Avalonia UI 12**: cross-platform XAML UI framework
- **AvaloniaEdit**: text editor engine (Scintilla's spiritual counterpart in .NET)
- **AvaloniaEdit.TextMate**: TextMate grammar-based syntax highlighting (82 languages)
- **CommunityToolkit.Mvvm**: MVVM source generators

## Install

Download `BlankSlate.app.zip` from the [latest release](https://github.com/lokeshvenkat17/blank-slate/releases/latest),
unzip it, and drag **BlankSlate.app** into your Applications folder.

### First launch

BlankSlate is not yet notarized by Apple, so macOS will refuse to open it on the first
try with a message like *"Apple could not verify BlankSlate is free of malware."*
This is expected for an independently distributed app, not a sign that anything is wrong.

To open it the first time:

1. **Right-click** (or Control-click) BlankSlate.app in Applications
2. Choose **Open**
3. Click **Open** in the dialog

You only need to do this once. Afterwards it launches normally.

If macOS still blocks it, run:

```sh
xattr -dr com.apple.quarantine /Applications/BlankSlate.app
```

Notarization is planned, which will remove this step entirely. Maintainers: see
[docs/notarization.md](docs/notarization.md) for the signing and notarization setup.

## Building from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```sh
git clone https://github.com/lokeshvenkat17/blank-slate.git
cd blank-slate
dotnet run --project src/BlankSlate
```

To build a distributable `.app`:

```sh
./scripts/package-macos.sh          # arm64 (Apple Silicon)
./scripts/package-macos.sh x64      # Intel
```

## Tests

Headless Avalonia tests cover the editor surface, plugin system, and text operations,
and render real windows to PNGs so the UI can be inspected rather than assumed:

```sh
dotnet test
BLANKSLATE_SHOT_DIR=/tmp/shots dotnet test --filter FullyQualifiedName~ScreenshotTests
```

## Roadmap

| Phase | Scope | Status |
|---|---|---|
| 1 | Core editor shell: tabs, open/save, dirty tracking, close prompts, menus, status bar, drag-and-drop | ✅ |
| 2 | Editor essentials: encoding & EOL detection/conversion, word wrap, zoom, whitespace display | ✅ |
| 3 | Search suite: Find/Replace (regex), Find in Files, bookmarks | ✅ |
| 4 | Syntax highlighting for 82 languages via TextMate grammars, themes. See [docs/adding-languages.md](docs/adding-languages.md) | ✅ |
| 5 | Sessions, settings persistence, recent files, auto-backup | ✅ |
| 6 | Power features: macros, two-view tab groups, function list, document map, word completion | ✅ |
| 7 | Plugin system (.NET plugin API). See [docs/writing-plugins.md](docs/writing-plugins.md) | ✅ |
| 8 | macOS .app packaging & ad-hoc signing (`scripts/package-macos.sh`) | ✅ |
| 3b/6c | Parity extras: 5 token styles, change history, brace matching, incremental search, Begin/End Select | ✅ |

## License

[MIT](LICENSE)
