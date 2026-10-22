# T35. Done

**What.** `src/Rtok.Agents.Hosts` holds the host table: 36 hosts and 52 apps (CLI and desktop), each with binaries, app paths and per-module support (hooks, MCP, proxy, plugin), with the reason when a module can't be written. It also holds:

- **Probes.** PATH lookup with PATHEXT, app bundles, the `RTOK_HOST_SANDBOX` re-rooting, and `--version` with a 3 s limit.
- **The MCP module.** Hosts the MCP core models go through Rtok.Mcp.Install's `McpInstaller`/`McpStatus`. Goose and Continue (YAML) and Reasonix (TOML `[[plugins]]`) get their own text-splicing adapters. The YAML ones are validated with SharpYaml's event parser, which is AOT-clean.
- **`HostRun`.** It runs install, update and uninstall with rtok's blocks, notes and warnings, and backs `agents list`/`info`, text and JSON.

**CLI.** `rtok agents install|update|uninstall|list|info` lives in Rtok.Cli and takes:

- `--cli`, `--desktop` (also `--gui`), `--all`, `--dry-run`, `--yes`, `--mcp` and `--config`;
- rtok's aliases `agent`, `setup` and `remove`, spelled out before routing because ConsoleAppFramework routes `|` aliases only at the top level;
- the `[setup.*]` path keys the user sets, mapped onto the MCP files they move (`SetupPaths`).

**Docs.** The support table in `docs/agents.md` is generated from the host table (`SupportMatrix`). A drift test rewrites it with `RTOK_BLESS=1`.

**Fixed on the way:**
- A shared host (VS Code stable and insiders) installs every surface in one apply.
- Ownership checks drop the entry's `name`, which is always `rtok` and is no sign of what the entry runs.
- Reasonix keeps the blank line before its table under CRLF too.
- After the merge: a YAML key that is an alias or a collection is a parse error (a refusal), not a crash; the `agents-install` fuzz target found it.
- The entry's `args` use `JsonArray`'s `params` constructor: a collection expression calls the generic `Add<T>`, which the AOT publish rejects (IL2026/IL3050) while the library build stays silent.
- `Rtok.Agents.Hosts` has its layer in `ArchitectureTests`.

Only the MCP module installs. Hooks, proxy, plugin and restart are T35.3–T35.6.

**Checked:**
- **`Rtok.Agents.Hosts.Tests`:**
  - The install matrix on fake Linux, macOS and Windows homes: every host installed twice, then updated, then uninstalled. The second install and the update change no byte; each app has one serving entry per surface; nothing of rtok remains afterwards.
  - Goose, Continue and Reasonix cases from rtok: fresh files, foreign lines, CRLF, edited and foreign entries, refused files.
  - CsCheck byte preservation on YAML, TOML and JSONC (Zed's `settings.json`).
  - Dry run; leave and `--yes`; "is not installed"; the not-on-PATH warning and the absolute `rtok.exe` on Windows.
  - Unparseable files refused by name and left untouched.
  - SetupPaths.
  - Verify snapshots of `info` text and JSON, with present, missing and stale rows.
  - The doc drift test.
- **`Rtok.Cli.Tests`:** the aliases; the new flags as action flags; every `SetupPaths` key is a config key; `agents list` and `agents info` print JSON with `--json` (JsonTests). `docs/cli/commands.json` and the help, completion and man snapshots carry the agents commands. The test binary runs with `RTOK_HOST_SANDBOX=1`, so no real agent on the machine is found or run.
- **Fuzz:** `agents-install` in `tests/Rtok.Fuzz` feeds any text to the Goose, Continue and Reasonix adapters, installed and removed as `Apply` does: a refusal is a non-empty `InvalidDataException`; install leaves a current entry and a second install changes no byte; remove leaves no entry. Its seed corpus comes from the adapter tests. Byte preservation of everything else is the CsCheck tests above.
- **Gates:** `dotnet build rtok.slnx -c Release -warnaserror --no-incremental`, `dotnet test` (with the AOT smoke publish) and `dotnet csharpier check .` are green.
- **Smoke test** with the debug `rtok` on a sandboxed home with fake `claude` and `goose` binaries: setup/install twice, info, `--json`, list, update, remove, then update ("nothing to update").
