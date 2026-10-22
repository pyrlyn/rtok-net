---
status: todo
priority: P3
complexity: 3
readiness: 0%
agent:
---

# T35.2.1. Host: Visual Studio

Visual Studio 2022 17.14+ and 2026 read a user-level MCP file, `%USERPROFILE%\.mcp.json` (JSON `servers`), and a user instructions file, `%USERPROFILE%\copilot-instructions.md`. It is Windows only.

Approved by the creator from the T35.2 shortlist (its research was not spot-checked). Sources:

- <https://learn.microsoft.com/en-us/visualstudio/ide/mcp-servers>
- <https://learn.microsoft.com/en-us/visualstudio/ide/copilot-chat-context>

Done when `rtok agents install visualstudio` writes rtok's entry into Visual Studio's user-level config and uninstall takes it out, or, if the docs do not hold up, Visual Studio is a not-supported row (T35.1's `AgentHosts.Unsupported`) with the reason.

1. Re-read the vendor docs above first. If the user-level path or a surface does not hold up, that surface reports "not supported" with the reason; if MCP does not hold up, the whole host becomes a not-supported row and the task ends there.
2. Expected surfaces: MCP in `%USERPROFILE%\.mcp.json`; hooks and proxy not supported; rules only if the instructions file holds up.
3. Only the Windows fake home has the app; on Linux and macOS homes install reports it is not installed.

Ports: rtok open T413 ([open work](../../docs/port/open-work.md)).

Tests:
- `Rtok.Agents.Hosts.Tests` unit: the host has a dated research row with sources; the support matrix and `docs/agents.md` carry its row.
- `Rtok.Agents.Hosts.Tests` integration: install, install again, update and uninstall on fake Linux, macOS and Windows homes leave at most one rtok entry per surface.
- `Rtok.Agents.Hosts.Tests` CsCheck: install and uninstall keep every byte outside rtok's entry in the host's config format.
- `Rtok.Agents.Hosts.Tests` unit: a surface the host does not document reports "not supported" with a reason and writes nothing; with the app absent, install fails with `error: visualstudio is not installed`.
- `Rtok.Mcp.Install.Tests` unit: the host's `McpSpec` rows pass the `McpHostsTests` cycle.

Fuzz: the host joins T35's `agents-install` target.

Check: Visual Studio passes the T35 install-matrix test, or has a dated not-supported row with sources.
