# BlankSlate

A cross-platform (macOS-first) text editor inspired by [Notepad++](https://github.com/notepad-plus-plus/notepad-plus-plus), built with .NET and [Avalonia UI](https://avaloniaui.net/).

## Why

Notepad++ is Windows-only. BlankSlate aims to bring the full Notepad++ feature set to macOS (and Linux/Windows) with a single .NET codebase.

## Tech stack

- **.NET 10** / C#
- **Avalonia UI 12** — cross-platform XAML UI framework
- **AvaloniaEdit** — text editor engine (Scintilla's spiritual counterpart in .NET)
- **AvaloniaEdit.TextMate** — TextMate grammar-based syntax highlighting (82 languages)
- **CommunityToolkit.Mvvm** — MVVM source generators

## Building & running

```sh
dotnet run --project src/BlankSlate
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
| 4 | Syntax highlighting for 82 languages via TextMate grammars, themes — see [docs/adding-languages.md](docs/adding-languages.md) | ✅ |
| 5 | Sessions, settings persistence, recent files, auto-backup | ✅ |
| 6 | Power features: macros, two-view tab groups, function list, document map, word completion | ✅ |
| 7 | Plugin system (.NET plugin API) — see [docs/writing-plugins.md](docs/writing-plugins.md) | ✅ |
| 8 | macOS .app packaging & ad-hoc signing (`scripts/package-macos.sh`) | ✅ |
| 3b/6c | Parity extras: 5 token styles, change history, brace matching, incremental search, Begin/End Select | ✅ |
