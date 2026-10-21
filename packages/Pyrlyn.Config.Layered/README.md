# Pyrlyn.Config.Layered

Merges ordered configuration layers (defaults, TOML files, `.env`, environment, flags) into
one TOML tree with per-key provenance, and edits TOML files in place without losing comments.
AOT-compatible; built on [Tomlyn](https://github.com/xoofx/Tomlyn)'s syntax tree.

## Install

```bash
dotnet add package Pyrlyn.Config.Layered
```

## Use

```csharp
using Pyrlyn.Config.Layered;

var config = new LayeredConfigBuilder("MYAPP_")
    .AddDefaults(TomlText.Parse("[proxy]\nport = 8080\nupstream = \"https://api\"\n"))
    .AddTomlFile("user", "/home/me/.myapp/config.toml")
    .AddTomlFile("project", "/work/.myapp.toml")
    .AddDotEnv("dotenv", [DotEnv.FindUp(Environment.CurrentDirectory) ?? "", "/home/me/.env"])
    .AddEnvironment("env")
    .AddFlag("flag", "proxy.port", 9000L, "--port")
    .Build();

foreach (var e in config.Entries)
{
    Console.WriteLine($"{e.Key} = {TomlText.Format(e.Value)}  ({e.Source})");
}
// proxy.port = 9000  (flag --port)
// proxy.upstream = "https://api"  (user /home/me/.myapp/config.toml:3)
```

- Layers apply in the order they are added; a later layer wins key by key. Tables merge, a
  scalar or an array is replaced whole.
- Every leaf keeps its `ConfigSource`: the layer name and kind, plus `file:line`, the
  environment variable or the flag.
- Environment variables are named after the default leaves (`proxy.openai_upstream` is
  `MYAPP_PROXY_OPENAI_UPSTREAM`); others are ignored. A value takes its default's type, and a
  default array takes a comma-separated list. `AddEnvAlias` keeps an old name working.
- `.env` files are parsed, never loaded into the process environment. The first file that
  defines a variable wins; a malformed file becomes a warning.

Edit a file in place:

```csharp
var text = File.ReadAllText(path);
File.WriteAllText(path, TomlEditor.Set(text, "proxy.port", 9090L, path));
```

Only the value changes; comments, padding and every other byte stay. A commented
`# port = 8080` line in the right table is uncommented in place. Text inside multi-line strings
is never touched, and an edit that would not parse back is refused with `file:line`.
