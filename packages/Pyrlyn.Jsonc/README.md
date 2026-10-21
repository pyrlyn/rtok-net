# Pyrlyn.Jsonc

Lossless JSONC editing: set or remove one entry by key path, keeping every other byte, comment and comma.

## Install

```bash
dotnet add package Pyrlyn.Jsonc
```

## Use

```csharp
using Pyrlyn.Jsonc;
using System.Text.Json.Nodes;

var doc = JsoncDocument.Load("settings.json");      // JsoncException: settings.json:3:7: ...

doc = doc.Set(["context_servers", "rtok"], new JsonObject { ["command"] = "rtok" });
doc = doc.Remove("chat.pluginLocations", "/old");   // every duplicate of a key goes
bool there = doc.Contains("context_servers", "rtok");
string? raw = doc.GetRaw("theme");                   // the exact text, comments included

File.WriteAllBytes("settings.json", doc.Utf8.ToArray());
```

- A key path mixes object keys and array indices (`["a", 0, "b"]`).
- `Set` replaces the value only, or inserts after the last entry in its indentation, line
  breaks and comma style (trailing commas too); missing objects on the way are created, and
  index `Count` appends to an array. An equal value returns the same document.
- `Remove` takes the entry, one comma and, when nothing else is on them, its own lines.
  Comments next to it stay.
- Every edit is parsed again before it is returned. Comments and trailing commas are the only
  looseness: JSON5 stays rejected.
