# T35.3. Agent hosts: hooks module — done

`rtok agents install|update|uninstall` now write, update and remove rtok's hooks on every host with hook events, beside the MCP module, and `agents list|info` show a `hooks` row.

## What was done

- **Shared event data.** `HookEvents` and `HookEventRow` moved from `Rtok.Hooks` to `Rtok.Hook.Contracts`, so the hosts library and the hook manifests read one table. `HookLines` builds the POSIX and PowerShell resolver lines (prefer `rtok-hook`, else `rtok hook`, else exit 0) for both of them.
- **Hooks module** (`src/Rtok.Agents.Hosts/Hooks/`). Each writer edits only rtok's entries and leaves every other byte alone:
  - `NestedHooks`: Claude-style `{hooks: {<event>: [{matcher, hooks: [{type, command, timeout}]}]}}`. It covers Claude, Qwen, CodeBuddy and WorkBuddy, Codex (`hooks.json`, and the bare pre-`--host` form is treated as rtok's and rewritten), Devin, ZCode (`hooks.events`, `timeoutMs`, `hooks.enabled`) and Gemini (its own event names, milliseconds, nothing pruned). Edits go through Pyrlyn.Jsonc.
  - `CursorHooks`: Cursor's flat `hooks.json`. It retires the old `beforeSubmitPrompt` hook and writes `{ <resolver>; }` commands.
  - `CopilotHooks`: `~/.copilot/hooks/rtok.json`, a file rtok owns whole. Install writes it when it differs; remove deletes it through the new `HostDelete` seam (`HostFile.Delete`: gated and backed up).
  - `CommandCodeHooks`: one definition per matcher.
  - `KimiHooks`: `[[hooks]]` tables in `config.toml`.
  - `CodeWhaleHooks`: `[[hooks.hooks]] message_submit` plus `[hooks] enabled`.
  - TOML edits are span splices on the Tomlyn syntax tree (`TomlHooks`, which reuses `TomlServers`' splice). A value rewrite keeps its trailing comment, and the result is re-parsed before anything is written.
- **Report lines.** A current hook gives NO_CHANGES and is not rewritten. Otherwise there is one line per change:
  - `+ <event> <matcher> <cmd>` for a hook added;
  - `~ <event> <matcher> <cmd>` for a stale hook rewritten (command or timeout);
  - `- <event> <matcher> <cmd>` for a pair no longer listed, which is dropped.

  Then a count line follows (`9 additions, 1 updates, 2 removals`). Uninstall takes an rtok hook whose entry was edited by hand only on `--yes`. The dry run shows it as `?`, and leaving it is counted.
- **Paths.** `[setup] hooks.<host>.<surface>` paths come from `SetupPaths.HooksOf`, the same way the MCP paths do, and fall back to the host defaults. `[setup] hook_timeout_s` (default 5) sets the timeout.
- **`--replace`** (`agents install claude --replace`, rtok `claude::migrate`):
  - It requires `--yes` or `--dry-run`.
  - It strips the legacy hook stacks from Claude's `settings.json`, both nested and flat entries: rtk, lean-ctx, caveman and `python-launcher.sh`.
  - It drops the `lean-ctx` and `code-review-graph` MCP servers there and in `~/.claude.json`.
  - It prints `- <cmd>` per hook taken, `disabled MCP: …` and `remaining rtok hooks: N`.
- **`HostRun`.**
  - Install and uninstall run the hooks module before MCP.
  - Update skips a host only when neither the hooks nor MCP are installed.
  - The read-back warns "hooks did not read back as installed".
  - `agents list|info` (text and JSON) carry the hooks row with its support and state.
- **Windows.** Without `rtok` on PATH, commands carry the quoted absolute `rtok.exe` and no POSIX resolver.

## Deviations

- **Cline's hook links are not written.** These are the 5 per-event links in `~/Documents/Cline/Hooks`, and they point into rtok's plugin tree, which T36 has not built yet. The Cline row reports support and state, writes nothing and gives no read-back warning. The links come with the plugin link work (T35.5 on top of T36). This needs the creator's OK.
- D21 plugin detection (skipping hooks when rtok's plugin already brings them) waits for T35.5, so for now hooks are always written. Retargeting a legacy proxy `ANTHROPIC_BASE_URL` on `--replace` waits for the proxy module (T35.4).
- CommandCode rewrites a stale hook in place (`~`), where rtok appends a second one.
- A hooks container or event left empty by uninstall is dropped, so install followed by uninstall gives back the original bytes.
- CodeWhale keeps `[hooks] enabled` after uninstall, as rtok does.
- `--replace` writes `settings.json` only when its text changed; rtok always rewrites it.
- `TomlServers.Splice` orders cuts at equal offsets by end, so an insert and a replace at one offset stay apart.
- The `agents-install` fuzz target still fuzzes only the Goose, Continue and Reasonix MCP files. No task assigns a hooks fuzz target, and the TOML and JSONC writers are covered by byte-roundtrip unit tests.

## How it was checked

- `tests/Rtok.Agents.Hosts.Tests/HooksTests.cs` has 14 tests:
  - Claude: empty, stale, JSONC byte roundtrip, a user chain kept foreign, an edited hook asked about.
  - The Windows quoted `rtok.exe`.
  - Codex bare rewrite, ZCode, Gemini.
  - Cursor retire, Copilot delete, CommandCode per matcher.
  - Kimi keeps comments, CodeWhale keeps `enabled = false`.
  - `--replace`.
- `InstallMatrixTests` now also checks:
  - for every host and present surface, the hooks module reads back as installed exactly where it writes;
  - a second install changes no byte;
  - no JSON or TOML file holds two rtok hooks for one (event, matcher) pair.
- The Verify snapshots of `agents info` text and JSON were updated, and so were the CLI help, completion and man snapshots and `docs/cli/commands.json`. `--replace` is an action flag in `FlagCoverageTests`.
- The gates are all green: `dotnet build rtok.slnx -c Release -warnaserror --no-incremental`, `dotnet test`, `dotnet csharpier check .` and `npx --yes markdownlint-cli2@0.23.3`.
