---
status: todo
priority: P3
complexity: 3
readiness: 0%
agent:
---

# T35.2.8. Host: Mistral Vibe

Mistral Vibe CLI keeps MCP servers as `[[mcp_servers]]` in `~/.vibe/config.toml` (moved by `VIBE_HOME`); its README names `~/.vibe/hooks.toml` and `~/.vibe/AGENTS.md`.

Approved by the creator from the T35.2 shortlist (its research was not spot-checked). Sources:

- <https://docs.mistral.ai/vibe/code/cli/configuration>
- <https://github.com/mistralai/mistral-vibe>

Done when `rtok agents install vibe` writes rtok's entry into Mistral Vibe's user-level config and uninstall takes it out, or, if the docs do not hold up, Mistral Vibe is a not-supported row (T35.1's `AgentHosts.Unsupported`) with the reason.

1. Re-read the vendor docs above first. If the user-level path or a surface does not hold up, that surface reports "not supported" with the reason; if MCP does not hold up, the whole host becomes a not-supported row and the task ends there.
2. Expected surfaces: MCP in `config.toml`; hooks and rules if the docs site (not only the README) documents them.
3. `[[mcp_servers]]` is an array of tables; the TOML edit reuses `TomlEdit` from T35.4.

Ports: rtok open T413 ([open work](../../docs/port/open-work.md)).

Tests:
- `Rtok.Agents.Hosts.Tests` unit: the host has a dated research row with sources; the support matrix and `docs/agents.md` carry its row.
- `Rtok.Agents.Hosts.Tests` integration: install, install again, update and uninstall on fake Linux, macOS and Windows homes leave at most one rtok entry per surface.
- `Rtok.Agents.Hosts.Tests` CsCheck: install and uninstall keep every byte outside rtok's entry in the host's config format.
- `Rtok.Agents.Hosts.Tests` unit: a surface the host does not document reports "not supported" with a reason and writes nothing; with the app absent, install fails with `error: vibe is not installed`.
- `Rtok.Mcp.Install.Tests` unit: the host's `McpSpec` rows pass the `McpHostsTests` cycle.

Fuzz: the host joins T35's `agents-install` target.

Check: Mistral Vibe passes the T35 install-matrix test, or has a dated not-supported row with sources.
