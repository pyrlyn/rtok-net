---
status: todo
priority: P3
complexity: 3
readiness: 0%
agent:
---

# T35.2.5. Host: Qoder

Qoder (Alibaba; the IDE, its JetBrains plugin and the CLI) keeps MCP servers under `mcpServers` and Claude-style hooks in `~/.qoder/settings.json`.

Approved by the creator from the T35.2 shortlist (its research was not spot-checked). Sources:

- <https://docs.qoder.com/cli/settings>
- <https://docs.qoder.com/qoder/connectors>
- <https://docs.qoder.com/extensions/hooks>

Done when `rtok agents install qoder` writes rtok's entry into Qoder's user-level config and uninstall takes it out, or, if the docs do not hold up, Qoder is a not-supported row (T35.1's `AgentHosts.Unsupported`) with the reason.

1. Re-read the vendor docs above first. If the user-level path or a surface does not hold up, that surface reports "not supported" with the reason; if MCP does not hold up, the whole host becomes a not-supported row and the task ends there.
2. Expected surfaces: MCP and hooks in `settings.json`; rules not supported (no user-level path documented).
3. The hooks reuse T35.3's Claude-style hook writer if the event names match.

Ports: rtok open T413 ([open work](../../docs/port/open-work.md)).

Tests:
- `Rtok.Agents.Hosts.Tests` unit: the host has a dated research row with sources; the support matrix and `docs/agents.md` carry its row.
- `Rtok.Agents.Hosts.Tests` integration: install, install again, update and uninstall on fake Linux, macOS and Windows homes leave at most one rtok entry per surface.
- `Rtok.Agents.Hosts.Tests` CsCheck: install and uninstall keep every byte outside rtok's entry in the host's config format.
- `Rtok.Agents.Hosts.Tests` unit: a surface the host does not document reports "not supported" with a reason and writes nothing; with the app absent, install fails with `error: qoder is not installed`.
- `Rtok.Mcp.Install.Tests` unit: the host's `McpSpec` rows pass the `McpHostsTests` cycle.

Fuzz: the host joins T35's `agents-install` target.

Check: Qoder passes the T35 install-matrix test, or has a dated not-supported row with sources.
