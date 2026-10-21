# T12.9 done

New package `packages/Pyrlyn.Config.Layered` (0.1.0, on Tomlyn 2.10.1):

- `LayeredConfigBuilder` merges layers in the order they are added: `AddDefaults`, `AddToml`/`AddTomlFile`, `AddDotEnv`, `AddEnvironment`, `AddFlag`, plus `AddEntry` for a layer it has no method for (legacy folds). Tables merge key by key; a scalar or an array is replaced whole.
- Every leaf keeps a `ConfigSource`: layer name and kind, with `file:line` (TOML and `.env`), the variable or the flag. `LayeredConfig.Entries` lists them sorted by key.
- Environment and `.env` names come from the default leaves (`proxy.openai_upstream` is `<PREFIX>PROXY_OPENAI_UPSTREAM`), so underscores inside keys are never separators; unknown names are ignored; values take the default's type, default arrays take comma lists (`VAR=` is empty), `AddEnvAlias` keeps legacy names. `.env` is parsed, never loaded; the first file defining a variable wins; a malformed file is a warning. `DotEnv.FindUp` only stops at a file.
- `TomlEditor.Set` edits in place through Tomlyn's syntax tree: a live key changes only its value; a commented `# key = value` in the key's table is uncommented in place; else a line is added to the table (or next to a dotted key, or a new `[table]` at the end). Multi-line string payloads are never touched; CRLF is kept; an edit that does not parse back, or a key through a scalar, is refused with `file:line`.
- `KeyPath` (TOML dotted keys, quoted segments), `TomlText.Parse/Format/ParseValueOrString`.

Commit: `T12.9: Package Pyrlyn.Config.Layered: TOML layers with provenance`.

Checked: 29 tests in `packages/Pyrlyn.Config.Layered/tests` (provenance per key across defaults, user and project files, `.env`, env and flags; byte-identical edits of a commented file, including the uncomment and multi-line-string cases); `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test`, `dotnet csharpier check .` green.
