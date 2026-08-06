# Adding languages

BlankSlate highlights **82 languages** out of the box:

- **64** from [TextMateSharp.Grammars](https://github.com/danipen/TextMateSharp) (the same
  grammars VS Code ships) — C/C++, C#, Python, JavaScript/TypeScript, Java, Go, Rust, PHP,
  Ruby, Swift, HTML/CSS, SQL, YAML, Markdown, shell, and more.
- **18** authored for BlankSlate to cover languages Notepad++ has and TextMateSharp doesn't:
  TOML, CMake, Haskell, Erlang, Tcl, D, Nim, Scheme/Lisp, Fortran, Verilog, VHDL, Ada,
  COBOL, Smalltalk, MATLAB, GDScript, Windows Registry, and NSIS.

You can add any other language yourself — this is BlankSlate's equivalent of Notepad++'s
**User Defined Languages**, except it uses the standard TextMate format, so grammars
written for VS Code work unchanged.

## Adding your own grammar

**Language → Open Grammars Folder…** opens:

```
~/Library/Application Support/BlankSlate/grammars/
```

Create one sub-folder per grammar pack, laid out like a VS Code extension:

```
grammars/
└── my-languages/
    ├── package.json
    └── syntaxes/
        └── mylang.tmLanguage.json
```

`package.json` declares the languages and points at the grammar files:

```json
{
  "name": "my-languages",
  "version": "1.0.0",
  "engines": { "vscode": "*" },
  "contributes": {
    "languages": [
      { "id": "mylang", "extensions": [".ml2", ".mylang"], "aliases": ["MyLang"] }
    ],
    "grammars": [
      { "language": "mylang", "scopeName": "source.mylang", "path": "./syntaxes/mylang.tmLanguage.json" }
    ]
  }
}
```

Restart BlankSlate. The language appears in the **Language** menu and is auto-detected for
its file extensions.

### Where to find grammars

Most VS Code language extensions contain a `syntaxes/*.tmLanguage.json` file you can copy,
along with the matching entries from their `package.json`. Check the extension's licence
before redistributing it — for personal use, copying one into your own grammars folder is
fine.

## Writing a grammar

A minimal grammar needs a scope name and some patterns:

```json
{
  "name": "MyLang",
  "scopeName": "source.mylang",
  "patterns": [
    { "include": "#comment" },
    { "include": "#string" },
    { "include": "#keyword" }
  ],
  "repository": {
    "comment": { "match": "#.*$", "name": "comment.line.number-sign.mylang" },
    "string":  { "begin": "\"", "end": "\"", "name": "string.quoted.double.mylang" },
    "keyword": { "match": "\\b(if|else|while|return)\\b", "name": "keyword.control.mylang" }
  }
}
```

Use **standard scope names** so the theme actually colours them:

| Scope | Used for |
|---|---|
| `comment.line.*` / `comment.block.*` | comments |
| `string.quoted.double.*` | strings |
| `constant.numeric.*` | numbers |
| `constant.language.*` | `true`, `false`, `null` |
| `keyword.control.*` | keywords |
| `storage.type.*` | type names |
| `entity.name.function.*` | function names |
| `entity.name.tag.*` | section headers |

A scope the theme doesn't recognise loads fine but renders in the default colour — if
something stays black, the scope name is usually the reason.

The bundled grammars in
[`src/BlankSlate/Grammars/blankslate/syntaxes`](../src/BlankSlate/Grammars/blankslate/syntaxes)
are short, readable working examples.

## Still missing

A handful of Notepad++'s most obscure languages have no grammar yet — BaanC, Gui4Cli,
Hollywood, ESCRIPT, OScript, MMIXAL, KiXtart, Spice, txt2tags, Csound, AviSynth,
Visual Prolog, ASN.1, Forth, BlitzBasic/FreeBasic/PureBasic, Inno Setup, SAS, REBOL,
and nnCronTab. Drop a grammar in the folder above and they light up.
