---
status: todo
priority: P3
complexity: 3
readiness: 0%
agent:
---

# T35.2.7. Host: Rovo Dev CLI

Atlassian's Rovo Dev CLI stores MCP servers in `~/.rovodev/mcp.json` and reads memory from `~/.rovodev/AGENTS.md`. Its settings page names `mcpConfigPath`, default `~/.rovodev/mcp_config.json`, which disagrees with the MCP page.

Approved by the creator from the T35.2 shortlist (its research was not spot-checked). Sources:

- <https://support.atlassian.com/rovo/docs/connect-to-an-mcp-server-in-rovo-dev-cli/>
- <https://support.atlassian.com/rovo/docs/manage-rovo-dev-cli-settings/>
- <https://support.atlassian.com/rovo/docs/use-memory-in-rovo-dev-cli/>

Done when `rtok agents install rovodev` writes rtok's entry into Rovo Dev CLI's user-level config and uninstall takes it out, or, if the docs do not hold up, Rovo Dev CLI is a not-supported row (T35.1's `AgentHosts.Unsupported`) with the reason.

1. Re-read the vendor docs above first. If the user-level path or a surface does not hold up, that surface reports "not supported" with the reason; if MCP does not hold up, the whole host becomes a not-supported row and the task ends there.
2. Expected surfaces: MCP (JSON `mcpServers`) at `mcpConfigPath` from `~/.rovodev/config.yml`, else the documented default; rules if the memory file holds up.
3. Read `mcpConfigPath` from `config.yml` before writing; a test covers both names.

Ports: rtok open T413 ([open work](../../docs/port/open-work.md)).

Tests:
- `Rtok.Agents.Hosts.Tests` unit: the host has a dated research row with sources; the support matrix and `docs/agents.md` carry its row.
- `Rtok.Agents.Hosts.Tests` integration: install, install again, update and uninstall on fake Linux, macOS and Windows homes leave at most one rtok entry per surface.
- `Rtok.Agents.Hosts.Tests` CsCheck: install and uninstall keep every byte outside rtok's entry in the host's config format.
- `Rtok.Agents.Hosts.Tests` unit: a surface the host does not document reports "not supported" with a reason and writes nothing; with the app absent, install fails with `error: rovodev is not installed`.
- `Rtok.Mcp.Install.Tests` unit: the host's `McpSpec` rows pass the `McpHostsTests` cycle.

Fuzz: the host joins T35's `agents-install` target.

Check: Rovo Dev CLI passes the T35 install-matrix test, or has a dated not-supported row with sources.
