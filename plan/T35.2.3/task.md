---
status: todo
priority: P3
complexity: 3
readiness: 0%
agent:
---

# T35.2.3. Host: Hermes Agent

Hermes Agent (Nous Research) reads MCP servers from `~/.hermes/config.yaml` under `mcp_servers`, and hooks from a `hooks:` block in the same file.

Approved by the creator from the T35.2 shortlist (its research was not spot-checked). Sources:

- <https://hermes-agent.nousresearch.com/docs/user-guide/features/mcp>
- <https://hermes-agent.nousresearch.com/docs/user-guide/features/hooks>

Done when `rtok agents install hermes` writes rtok's entry into Hermes Agent's user-level config and uninstall takes it out, or, if the docs do not hold up, Hermes Agent is a not-supported row (T35.1's `AgentHosts.Unsupported`) with the reason.

1. Re-read the vendor docs above first. If the user-level path or a surface does not hold up, that surface reports "not supported" with the reason; if MCP does not hold up, the whole host becomes a not-supported row and the task ends there.
2. Expected surfaces: MCP and hooks in `config.yaml`; rules not supported (`SOUL.md` is an identity file, not instructions).
3. The YAML edit reuses T35's YAML ownership.

Ports: rtok open T413 ([open work](../../docs/port/open-work.md)).

Tests:
- `Rtok.Agents.Hosts.Tests` unit: the host has a dated research row with sources; the support matrix and `docs/agents.md` carry its row.
- `Rtok.Agents.Hosts.Tests` integration: install, install again, update and uninstall on fake Linux, macOS and Windows homes leave at most one rtok entry per surface.
- `Rtok.Agents.Hosts.Tests` CsCheck: install and uninstall keep every byte outside rtok's entry in the host's config format.
- `Rtok.Agents.Hosts.Tests` unit: a surface the host does not document reports "not supported" with a reason and writes nothing; with the app absent, install fails with `error: hermes is not installed`.
- `Rtok.Mcp.Install.Tests` unit: the host's `McpSpec` rows pass the `McpHostsTests` cycle.

Fuzz: the host joins T35's `agents-install` target.

Check: Hermes Agent passes the T35 install-matrix test, or has a dated not-supported row with sources.
