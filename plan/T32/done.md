# T32. Done

**What.** `src/Rtok.Doctor` is an AOT-clean library behind `rtok doctor`. It reads the host files through `Rtok.Agents.Hosts` paths and never starts a hook. The files:

- Claude settings at every scope, managed settings, plugins and their `hooks/hooks.json`;
- `~/.claude.json` and `.mcp.json`;
- the Codex and Kimi TOML hooks and MCP tables.

From them it builds:

- **Hooks.** Hooks by event and by tool matcher.
- **MCP servers.** Each server's tool count and estimated description tokens. The real probe runs the server's MCP handshake through CliWrap, with one deadline.
- **Base URLs and advice.** The `ANTHROPIC_BASE_URL` and OpenAI base-URL chains (`proxy none` when there is no chain). The `[proxy.tools_rewrite]` advice is in the JSON only.
- **Audits.** Instruction files and SessionStart injections (`--instructions` or `doctor.instructions`), the skills audit, and the overlaps section with Claude auto-memory.
- **Broken hooks.** Resolved without running anything: `~`, `$HOME`, `${VAR}` and host vars are expanded, interpreter scripts (bash, node, python, npx, `uv run`) are followed, and the PATH is searched with PATHEXT and the cmd/PowerShell rules on Windows.
- **Duplicates.** Duplicate hooks match on event, normalized matcher and normalized command. Duplicate MCP entries are compared with PATH and symlink resolution, `npx pkg@x`, URL case and trailing slash normalized. Each group gets a keep recommendation: plugin > project shared > user > local.
- **Exposure.** Wide binds, a wide bind without a token, and `~/.rtok` modes looser than 0700/0600. The health probe talks only to loopback.

**`--fix`** removes broken hooks and the extra copies of duplicates:

- The edits are minimal: JSONC through Pyrlyn.Jsonc, TOML through the existing editor. Each edit is checked against the same removal on the parsed tree.
- Each file is backed up to `_backup` first, and a file changed since the scan is skipped.
- It never removes the last copy, a plugin's file, a managed file or rtok's own `mcpServers.rtok` entry (D33). A hand-written copy under another name can go.
- `DoctorFix.Plan` and `DoctorFix.Apply` carry a plan token for web and TUI. On a mismatch, Apply writes nothing and returns the fresh plan.

**CLI.** `rtok doctor` lives in Rtok.Cli and takes `--json`, `--agent`, `--instructions`, `--fix`, `--yes`, `--dry-run`, `--stat`, `--only` and `--config`. With `--fix` it exits 1 when a selected fix fails. Help, man page, completions and `docs/cli/commands.json` are regenerated. `ParityMap` maps `doctor` to `Page.Doctor`; the web and TUI pages themselves are T44 and T45.

**Fuzz.** `doctor-config` in `tests/Rtok.Fuzz` takes any text as Claude settings JSONC, `~/.claude.json`, `.mcp.json` or Codex TOML, next to a fixed file it may duplicate. The checks:

- the report and the plan never throw;
- every planned edit only deletes characters;
- applying the plan writes exactly the planned text.

The seeds are in `corpus/doctor-config/01..05`.

**Differs from the task text or rtok:**
- Shell syntax in a hook (`$()`, backticks, eval, pipes) is **unverified** ("cannot verify"), as rtok's code does, not "suspect" as the task's test line says. Suspect stays for a script that is not executable.
- The interactive TTY checklist is not in the CLI: `--fix` without `--yes` is a dry run. Web and TUI choose through the Plan/Apply token API.
- Not ported: read-share, invocations, repeats, and the junk and config notes.
- Formatting: duplicate headers keep a space after `[matcher]`; overlaps use single spaces.
- YAML hosts are skipped.
- JSONC is read leniently.
- TOML hook files are reported, not edited. A refusal says why.
- `HostRun.Rows` gained `versions`, and `TomlText.ToJson` moved to Pyrlyn.Config.Layered so Doctor and Mcp.Install share it.

**Checked:**
- **`Rtok.Doctor.Tests` (new), on MemFs homes for Linux, macOS and Windows:**
  - hooks and MCP listing with tokens, the tools-rewrite advice rules, and the instruction, skills and overlaps audits;
  - broken-hook resolution, and a test that no process is started;
  - PATHEXT and cmd rules;
  - duplicates and keep order, and the D33 and last-copy refusals;
  - the plan token;
  - the `_backup` generation on a real disk, and dry-run;
  - exposure.
- **CsCheck:** MCP spelling variants are one duplicate and no env value is printed. `--fix` on generated JSONC and TOML files only deletes.
- **`Rtok.Cli.Tests`:**
  - Verify goldens for the text page and the `--json` problem rows for all nine kinds;
  - `--fix --yes --only broken-hooks` exits 0, and exits 1 when the backup fails;
  - dry-run;
  - bad flags exit 2.
- **`Rtok.Fuzz.Tests`:** runs `doctor-config` over its corpus, random and mutated input.
- **Gates:**
  - `dotnet build rtok.slnx -c Release -warnaserror --no-incremental`;
  - `dotnet test`;
  - `dotnet csharpier check .`;
  - `npx --yes markdownlint-cli2@0.23.3`.
