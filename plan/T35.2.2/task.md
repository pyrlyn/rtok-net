---
status: todo
priority: P3
complexity: 4
readiness: 0%
agent:
---

# T35.2.2. Host: OpenClaw

OpenClaw keeps its main config in `~/.openclaw/openclaw.json` (JSON5, key `mcp.servers`; moved by `OPENCLAW_STATE_DIR`), hooks as `HOOK.md` folders under `~/.openclaw/hooks/`, and rules in `~/.openclaw/workspace/AGENTS.md`.

Approved by the creator from the T35.2 shortlist (its research was not spot-checked). Sources:

- <https://docs.openclaw.ai/tools/mcp>
- <https://docs.openclaw.ai/help/faq/where-things-live-on-disk>

Done when `rtok agents install openclaw` writes rtok's entry into OpenClaw's user-level config and uninstall takes it out, or, if the docs do not hold up, OpenClaw is a not-supported row (T35.1's `AgentHosts.Unsupported`) with the reason.

1. Re-read the vendor docs above first. If the user-level path or a surface does not hold up, that surface reports "not supported" with the reason; if MCP does not hold up, the whole host becomes a not-supported row and the task ends there.
2. Expected surfaces: MCP in `openclaw.json`; hooks and rules if their docs hold up.
3. The config is JSON5, so writing it byte for byte needs a JSON5 editor that keeps comments, trailing commas and unquoted keys. Check NuGet first; if no package round-trips, this task adds a new one, `Pyrlyn.Json5` (generic, under `packages/`), with its own CsCheck round-trip test. If that is too large, OpenClaw becomes a not-supported row with that reason.

Ports: rtok open T413 ([open work](../../docs/port/open-work.md)).

Tests:
- `Rtok.Agents.Hosts.Tests` unit: the host has a dated research row with sources; the support matrix and `docs/agents.md` carry its row.
- `Rtok.Agents.Hosts.Tests` integration: install, install again, update and uninstall on fake Linux, macOS and Windows homes leave at most one rtok entry per surface.
- `Rtok.Agents.Hosts.Tests` CsCheck: install and uninstall keep every byte outside rtok's entry in the host's config format.
- `Rtok.Agents.Hosts.Tests` unit: a surface the host does not document reports "not supported" with a reason and writes nothing; with the app absent, install fails with `error: openclaw is not installed`.
- `Rtok.Mcp.Install.Tests` unit: the host's `McpSpec` rows pass the `McpHostsTests` cycle.

Fuzz: the host joins T35's `agents-install` target.

Check: OpenClaw passes the T35 install-matrix test, or has a dated not-supported row with sources.
