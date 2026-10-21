---
status: todo
priority: P2
complexity: 3
readiness: 0%
agent:
---

# T16. MCP ping and the session-to-MCP agent link

An agent's hooks and its MCP server must resolve to the same rtok agent, and users need a way to prove the MCP server answers.

Done when:
- `rtok mcp ping <agent> [--json]` proves the agent's rtok MCP server is alive. Headless hosts get one fixed prompt; desktop-only hosts get a spawned server that answers initialize, tools/list and a `ping` call. Timeouts and exit codes are set.
- The MCP process links to its agent by rtok's rules, in order: env var, nearest common ancestor pid, then cwd + host.
- The per-host research table from rtok is carried over.

Tests:
- `Rtok.Mcp.Tests` integration: `rtok mcp ping <agent>` against a fake headless host sends the one fixed prompt and exits 0 on its answer.
- `Rtok.Mcp.Tests` integration: against a fake desktop-only host, ping spawns the configured server and passes initialize, tools/list and a `ping` call.
- `Rtok.Mcp.Tests` integration: a fake host that hangs hits the timeout and exits non-zero with the reason, and an unknown agent exits non-zero.
- `Rtok.Cli.Tests` Verify: `rtok mcp ping <agent> --json` output for a pass, a timeout and an unknown agent.
- `Rtok.Agents.Tests` unit: the link resolver takes the env var first (grok `GROK_SESSION_ID`), then the nearest common ancestor pid within 3 levels, then cwd + host.
- `Rtok.Agents.Tests` unit: cwd + host with one live agent links it, with two answers ambiguous, and with none the process is outside every session.
- `Rtok.Agents.Tests` unit: only env and own links are cached.
- `Rtok.Agents.Tests` integration: the ancestor-pid walk finds a real parent process on every OS.
- `Rtok.Agents.Tests` unit: every host in the carried-over research table is a known host id with a link rule.
- `Rtok.Mcp.Tests` integration: a fake session's SessionStart hook and its `rtok mcp` child answer `whoami` with the same agent id.

Ports: rtok open T275.1, T281 ([open work](../../docs/port/open-work.md)).

Check: `rtok mcp ping` passes against a fake headless host and a fake desktop host in tests, and `whoami` over MCP returns the same agent id as the hook.
