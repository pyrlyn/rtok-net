---
status: todo
priority: P1
complexity: 3
readiness: 0%
agent:
---

# T36. Host plugin trees, hook resolver, in-host extensions, skills

Hosts load rtok through plugin trees and scripts, not only config entries. These are data and scripts, copied from rtok and adjusted for the .NET binaries.

Done when:
- These trees are carried over: `plugins/` (claude, codex, cursor, kimi, grok, zcode, antigravity, kilo, devin, cline, gemini), the pi and opencode extensions, and the hook-command resolver lines (POSIX, PowerShell, and the Cursor and Codex forms).
- `rtok filter --stdin` and `rtok guard check` serve the in-host extensions.
- Skills live only in `skills/` and are installed per host with the size limits.

Tests:
- `Rtok.Agents.Tests` unit: each `plugins/<host>` manifest (claude's 9-entry `hooks.json`, codex, cursor, kimi, grok, zcode, antigravity, kilo, devin, cline, gemini `hooks/hooks.json`) lists exactly the events the hook-event table gives that host.
- `Rtok.Agents.Tests` unit: every shipped hook command is the resolver line in its host's form: the POSIX line with no brace group, Cursor's `{ …; }` (bare in the Windows copy), Codex's `commandWindows`, and Copilot's PowerShell twin.
- `Rtok.Agents.Tests` Verify: the missing-rtok note naming `ketch install pyrlyn/rtok` appears only on each host's session-start event, in its shape (Claude `hookSpecificOutput.additionalContext`, Copilot `additionalContext`, Cursor `additional_context`), and never for Grok.
- `Rtok.Agents.Tests` unit: `gemini-extension.json` has `mcpServers.rtok = {command: rtok, args: [mcp]}` and no `trust` field, and every plugin README links the host's official docs.
- `Rtok.Agents.Tests` integration: with neither rtok on PATH nor `~/.ketch/bin/rtok`, the POSIX resolver line run under `sh` prints nothing and exits 0, and the PowerShell twin does the same under `pwsh` on Windows.
- `Rtok.Agents.Tests` integration: the Claude plugin's `worktree.sh` with neither jq nor perl on PATH exits 1 naming "jq or perl".
- `Rtok.Smoke` smoke: the POSIX resolver line with the published `rtok` on PATH runs `rtok hook <event>` and passes the hook's stdout and exit code through.
- `Rtok.Cli.Tests` Verify: `rtok filter --stdin --cmd "git status"` compresses fixture text without starting any process, and `expand` on its id returns the input byte for byte.
- `Rtok.Cli.Tests` unit: `rtok filter --stdin` fails open: on a filter error it exits 0 and writes the input unchanged.
- `Rtok.Cli.Tests` Verify: `rtok guard check --tool <name> --json <input>` returns the allow and deny verdicts in the JSON the pi and opencode extensions read.
- `Rtok.Agents.Tests` unit: skills exist only under `skills/` (rtok, rtok-worktrees, rtok-tasks) and no `plugins/` tree bundles a `SKILL.md`.
- `Rtok.Agents.Tests` unit: each `skills/*/SKILL.md` has a body of at most 2048 bytes and a description of at most 120 chars.
- `Rtok.Agents.Tests` integration: skill install copies into each host's skill root (pi `~/.pi/agent/skills`) on a fake home, and a second sync reports "no changes".

Spec: catalog A20, catalog B2, catalog B25 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T10.2, T10.6, T48.1, T48.2, T62.3, T70.1, T70.2, T70.4, T70.5, T70.6, T70.7, T88, T92, T92.1, T95, T118.3, T132, T155, T174, T195, T197, T214, T234, T248, T250.1, T250.2, T250.3, T250.4, T349, T357, T622, T635.

Check: Manifest drift tests pass against the hook-event table, and the skill size tests pass.
