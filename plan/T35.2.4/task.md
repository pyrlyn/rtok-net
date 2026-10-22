---
status: todo
priority: P3
complexity: 2
readiness: 0%
agent:
---

# T35.2.4. Host: Amazon Q Developer IDE

The Amazon Q Developer IDE plugin (VS Code, JetBrains) reads global MCP servers from `~/.aws/amazonq/default.json`, and still reads the legacy `~/.aws/amazonq/mcp.json`. Its CLI is now Kiro CLI, which rtok already supports.

Approved by the creator from the T35.2 shortlist (its research was not spot-checked). Sources:

- <https://docs.aws.amazon.com/amazonq/latest/qdeveloper-ug/mcp-ide.html>

Done when `rtok agents install amazonq` writes rtok's entry into Amazon Q Developer IDE's user-level config and uninstall takes it out, or, if the docs do not hold up, Amazon Q Developer IDE is a not-supported row (T35.1's `AgentHosts.Unsupported`) with the reason.

1. Re-read the vendor docs above first. If the user-level path or a surface does not hold up, that surface reports "not supported" with the reason; if MCP does not hold up, the whole host becomes a not-supported row and the task ends there.
2. Expected surfaces: MCP (JSON `mcpServers`); hooks, rules and proxy not supported.
3. Decide from the docs which of the two files to write; uninstall removes rtok's entry from both.

Ports: rtok open T413 ([open work](../../docs/port/open-work.md)).

Tests:
- `Rtok.Agents.Hosts.Tests` unit: the host has a dated research row with sources; the support matrix and `docs/agents.md` carry its row.
- `Rtok.Agents.Hosts.Tests` integration: install, install again, update and uninstall on fake Linux, macOS and Windows homes leave at most one rtok entry per surface.
- `Rtok.Agents.Hosts.Tests` CsCheck: install and uninstall keep every byte outside rtok's entry in the host's config format.
- `Rtok.Agents.Hosts.Tests` unit: a surface the host does not document reports "not supported" with a reason and writes nothing; with the app absent, install fails with `error: amazonq is not installed`.
- `Rtok.Mcp.Install.Tests` unit: the host's `McpSpec` rows pass the `McpHostsTests` cycle.

Fuzz: the host joins T35's `agents-install` target.

Check: Amazon Q Developer IDE passes the T35 install-matrix test, or has a dated not-supported row with sources.
