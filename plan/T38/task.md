---
status: todo
priority: P2
complexity: 3
readiness: 0%
agent:
---

# T38. Agent usage and cost: `rtok agents usage`

Users want token use and cost across all their agents, from the agents' own logs.

Done when:
- Log readers exist for Claude Code, Codex, OpenCode/Kilo (read-only SQLite), Copilot, Gemini, pi and Kimi, each with a fixture per shape (rtok T396).
- The flags, the units, the `--tz` buckets with DST and the JSON shape follow the catalog.
- Prices come only from `[stats.prices]`.

Spec: catalog B8 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T358, T358.1, T358.2, T358.3, T358.4, T358.5, T358.6, T389; open T396 ([open work](../../docs/port/open-work.md)).

Check: Reader fixtures for each host give the expected totals, and DST bucket tests pass.
