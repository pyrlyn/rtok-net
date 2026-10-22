---
status: in progress
priority: P1
complexity: 4
readiness: 100%
agent: Claude Code / claude-opus-5-5
---

# T35.3. Agent hosts: hooks module

`rtok agents install` writes rtok's hooks into every host that has hook events; the event names and matchers per host come from T13's hook-event table.

Done when:
- The hooks module of every host descriptor from T35 writes, updates and removes rtok's hook entries in the host's own shape (Claude settings.json, Codex and Kimi TOML, CodeWhale, Cline hook links, Gemini, Copilot, Cursor and the rest in the catalog), keeping foreign entries byte for byte.
- A stale rtok hook is rewritten (`~ <event> <cmd>`), a pair no longer listed is dropped (`- <event>`), a current one gives no changes; `--replace` removes legacy hook stacks.
- `agents list|info` show the hooks module state.

Tests:
- `Rtok.Agents.Hosts.Tests` integration: the T35 install matrix also leaves at most one rtok hook per event per surface.
- `Rtok.Agents.Hosts.Tests` unit: a current hook prints NO_CHANGES and is not rewritten, a stale one prints `~ <event> <cmd>`, and a pair no longer listed prints `- <event>`.
- `Rtok.Agents.Hosts.Tests` unit: on `MemFs.Windows()` without `rtok` on PATH hook commands carry the absolute `rtok.exe`.

Spec: catalog A19, catalog B3 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: the hook part of T35's list.

Check: The T35 install-matrix test covers the hooks module for every host with hook events, with at most one rtok hook per event per surface after installing twice.

Plan:
1. Spec from rtok's hook install per host (shapes, events, ownership, stale/current, `--replace`) and T13's event table.
2. A hooks module in `Rtok.Agents.Hosts` beside the MCP one: per-shape writers (Claude-style JSON, Gemini, Copilot, Cursor, Codex/Kimi/CodeWhale TOML, Cline links), edits through Pyrlyn.Jsonc and the TOML editor, rtok's entries only.
3. Install, update and uninstall print `+`, `~ <event> <cmd>`, `- <event>` or NO_CHANGES; `--replace`; `agents list|info` module state.
4. Tests: install-matrix extension (at most one rtok hook per event per surface), stale/current/dropped units, Windows absolute `rtok.exe`.
5. Gates, `done.md`, merge.
