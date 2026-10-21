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

Ports: rtok open T275.1, T281 ([open work](../../docs/port/open-work.md)).

Check: `rtok mcp ping` passes against a fake headless host and a fake desktop host in tests, and `whoami` over MCP returns the same agent id as the hook.
