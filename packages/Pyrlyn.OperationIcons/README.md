# Pyrlyn.OperationIcons

Emoji icons for the verb of a status line, matched by substring in order, and a measured gutter that keeps lines with and without an icon aligned.

## Install

```bash
dotnet add package Pyrlyn.OperationIcons
```

## Use

```csharp
using Pyrlyn.OperationIcons;

VerbIcons.Icon("installing");          // "📦"
VerbIcons.Icon("Uninstalled");         // "🧹": uninstall is matched before install
VerbIcons.Icon("thinking");            // null: fall back to the line kind's own icon

VerbIcon[] own = [new("index", "📚"), new("start", "🚀")];
var icon = VerbIcons.Icon("indexing") ?? VerbIcons.Icon(own, "indexing") ?? "✅";

VerbIcons.Gutter("📦");   // "📦 "
VerbIcons.Gutter("→");    // "→  ": a narrow icon is padded
VerbIcons.Gutter(null);   // "   ": a line without an icon lines up too
```

- `Shared` holds the twelve rows rtok and ketch share (🧹 uninstall, remove, prune; 📦 install;
  ⏫ upgrade, update; ⏬ download, fetch; 🔗 link; ⏪ roll back; 🔍 search; 🩺 doctor). A key
  is matched as a substring of the lowercased verb, first row wins, so `installing`,
  `installed` and `already installed` share one row.
- `Icon(rows, verb)` runs the same lookup over a program's own rows.
- `Gutter` pads every icon to `IconWidth` (2) terminal columns plus one space. The width is
  measured with [Wcwidth](https://www.nuget.org/packages/Wcwidth), not assumed; a narrow
  symbol followed by U+FE0F (`⚠️`) counts as two. Table icons are single code points that are
  wide by default, because a terminal that ignores U+FE0F would draw a narrow-plus-selector
  icon over the space after it.
- The package knows nothing about colour or tones.
