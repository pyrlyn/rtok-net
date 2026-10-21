# T34 done

New project `src/Rtok.Mcp.Install` (library, AOT-clean) with `tests/Rtok.Mcp.Install.Tests`. It references Pyrlyn.Jsonc, Pyrlyn.Config.Layered (Tomlyn) and Rtok.Agent.Sdk, and reads through `System.IO.Abstractions` (already pinned). No new package.

- **Host table.** `McpHosts.All(McpHostEnv, IFileSystem)` gives one `McpSpec` per host surface: 43 rows for 31 hosts, taken from rtok's own installers. Each row has the path per OS (Linux, macOS, Windows `%APPDATA%`), the format (strict JSON, JSONC or TOML), the key path (Amp's flat `amp.mcpServers` included), the entry shape, the launch (`--host <id>`; an absolute binary for Claude Desktop and ZCode), the duplicate-name behaviour and the plugin that serves rtok (Gemini).
  - Entry shapes: typed stdio, plain command, Copilot's `type: local` with `tools`, OpenCode/Kilo/MiMo's argv array, and Codex's write approval.
  - Paths the user set (`[setup.<host>]`) come in as `<host>.<surface>` overrides.
  - Goose and Continue (YAML) and Reasonix (a TOML array of tables) keep their own adapters in T35.
- **Core.** `McpInstaller.Apply(spec, Install|Update|Remove, Apply, McpDisk, isBin)` is the one call path for every host. `McpDisk` is the file seam: it reads through `IFileSystem` and writes through a `HostWrite` (the CLI passes `HostFile.Write`).
  - Install and update always write rtok's entry, and an entry that differs only by `"type": "stdio"` is no change. Only remove takes the entry out, through `Ownership.Judge` (D33).
  - `McpConfig` reads and edits the servers table per format:
    - strict JSON: pretty rewrite; a non-object root or table is refused; tables emptied by a remove are dropped;
    - JSONC: lossless edit through Pyrlyn.Jsonc; an emptied table goes only if it holds no comment;
    - TOML: syntax-tree splices; the entry is a `[<key>.rtok]` table under implicit parents, replaced in place or appended, and an inline entry is rewritten as a table.
  - A file that exists but cannot be read, is not UTF-8, or does not parse is refused and kept, never replaced (rtok T652).
- **Status.** `McpStatus.Of` gives an `McpRow`: present, missing or stale with a `runs …, want …` diff. Any rtok binary with `mcp` or `mcp --host <id>` counts, as `command` plus `args` or as one argv array. Strict-JSON files are read leniently as JSONC, and an unreadable file is "missing" with the reason. `McpRow.Text()` gives the `agents info` line, and `McpJson` is the source-generated JSON context.
- **Moved.** The `agents info` / `agents list` criterion (the real MCP state, the same in `--json` and the web model) moved to T35, on the creator's OK. T35's task now lists it, and T43 consumes the web-model part. `McpRow` and `McpJson` are the model they read.

Check: `McpHostsTests` runs install, install again, update, status and remove for every host's spec on fake Linux, macOS and Windows homes (`MemFs`). The rtok-mcp cases are ported in `McpInstallerTests`:
- the round trip for every format;
- the TOML table shape;
- edited and foreign entries left;
- the launch with a type-only difference;
- the ownership × `--yes`/dry-run table;
- the strict-JSON refusals;
- the unreadable file refused.

`McpStatusTests` covers the stale diff and the row text. `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test` (509 tests: 508 passed, 1 skipped, 0 failed) and `dotnet csharpier check .` are green.
