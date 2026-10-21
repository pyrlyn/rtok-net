---
status: todo
priority: P2
complexity: 4
readiness: 0%
agent:
---

# T37. Agent registry, identity, sessions and messaging

Several agents work in one repo at once; each needs an id, and they need to message each other (rtok D34).

Done when:
- The agents table is upserted on every hook event, and sub-agents are re-parented.
- Ids resolve from a prefix of 4 or more hex chars.
- The agent learns its id at SessionStart and SubagentStart.
- `agents whoami/sessions/show/status/send/inbox` and the MCP equivalents work.
- Messages are pushed within `push_bytes` and framed as information, not instructions.

Spec: catalog B6, catalog A26 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T25.0, T25.1, T25.2, T25.3, T282, T283, T283.1, T283.2, T283.3, T284, T287, T288, T290, T473, T613.

Check: Tests cover re-parenting, prefix resolution, message framing and push caps.
