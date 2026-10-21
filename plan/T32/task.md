---
status: todo
priority: P2
complexity: 4
readiness: 0%
agent:
---

# T32. Doctor: checks, duplicates and `--fix`

`rtok doctor` finds broken and duplicate hooks and MCP entries across hosts and fixes them minimally.

Done when:
- Read-only `doctor` behaves as in the catalog: hooks, MCP servers with description tokens, the base-URL chain, the instruction and skills audits, overlaps, broken hooks resolved without running them, and duplicates with a keep recommendation.
- `--fix` makes minimal JSONC/TOML edits through Pyrlyn.Jsonc and Tomlyn, with the plan-token flow shared with web and TUI.
- rtok's own MCP entry is never removed (D33).
- An exposure check covers wide binds, missing tokens and loose file modes (rtok I-S1).

Tests:
- `Rtok.Doctor.Tests` unit: on a fake home, hooks are listed by event and tool, and MCP servers from `~/.claude.json` and `.mcp.json` come with tool counts and estimated description tokens.
- `Rtok.Doctor.Tests` unit: the `[proxy.tools_rewrite]` advice appears only when description tokens reach `doctor.tools_rewrite_min_desc_tokens` and the ANTHROPIC_BASE_URL chain contains rtok's proxy; `https://api.anthropic.com` never counts as a custom base.
- `Rtok.Doctor.Tests` unit: the instruction audit counts CLAUDE.md and plugin SessionStart tokens and flags duplicates, the skills audit flags duplicate skills and descriptions over 120 chars, and the overlaps section names Claude auto-memory.
- `Rtok.Doctor.Tests` unit: broken-hook resolution expands `~`, `$HOME`, `${VAR}` and host vars, follows interpreter scripts (bash, node, python, npx, `uv run`) and the PATH, marks `$()`, backticks, eval and pipes as suspect, and never starts a process.
- `Rtok.Doctor.Tests` unit: on `MemFs.Windows()` the PATH lookup applies PATHEXT and the cmd/PowerShell rules.
- `Rtok.Doctor.Tests` unit: duplicate hooks match on event, normalized matcher and normalized command, and the keep recommendation follows plugin > project shared > user > local.
- `Rtok.Doctor.Tests` CsCheck: MCP entries that differ only by PATH/symlink, `npx pkg@x`, URL case or trailing slash are one duplicate, and no env value appears in the output.
- `Rtok.Doctor.Tests` CsCheck: `--fix` on generated JSONC and TOML host files changes only the fixed entries and keeps every other byte.
- `Rtok.Doctor.Tests` unit: `--fix` never removes the last copy, a plugin's own file, a managed file or rtok's own `mcpServers.rtok` entry (D33), while a hand-written copy under another name is removable.
- `Rtok.Doctor.Tests` unit: the plan token applies only when a rescan yields the same token; a file changed in between returns a fresh plan and writes nothing.
- `Rtok.Doctor.Tests` integration: `--fix --yes` writes one `_backup` generation per edited file, and `--dry-run` prints the diff and writes nothing.
- `Rtok.Doctor.Tests` unit: the exposure check reports a non-loopback bind, a wide bind without a token, and `~/.rtok` dirs looser than 0700 or files looser than 0600 (Unix only).
- `Rtok.Cli.Tests` Verify: `rtok doctor` text and `--json problems[]{kind,agent,source,path,detail,fixable,selected,keep}` for each problem kind, with the "N problems can be fixed" line.
- `Rtok.Cli.Tests` unit: `rtok doctor --fix --yes --only broken-hooks` exits 1 when a selected item was not fixed and 0 when all were.

Spec: catalog A18, catalog B18 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T1.4, T7.2, T42, T59.7, T61.3, T123, T171, T173, T331, T331.1, T331.2, T331.3, T331.4, T331.5, T331.6, T331.7, T331.8, T331.9, T331.10, T331.11, T331.12, T388, T392, T393, T576; open I-S1 ([open work](../../docs/port/open-work.md)).

Check: Property tests show `--fix` leaves bytes outside the fixed entries unchanged, and goldens match for each problem kind.
