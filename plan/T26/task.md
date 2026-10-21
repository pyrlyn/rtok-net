---
status: todo
priority: P2
complexity: 4
readiness: 0%
agent:
---

# T26. SessionStart repo map ranked by personalized PageRank

A ranked repo map at session start saves exploratory reads; rtok's backtest showed +17.4 pp.

Done when:
- A file graph is built from the symbol scan with IDF-weighted edges.
- Global ranks come from CSR power iteration (0.85 damping, 20 iterations) and are stored per root.
- The personalized ranks (dirty and recent files, the last checkpoint's paths) are computed in memory, never stored.
- The map fills its budget in one pass.
- `map_rank` selects refs or pagerank.

Ports: rtok open T370 ([open work](../../docs/port/open-work.md)).

Check: The backtest fixture reproduces rtok's ranking, and SessionStart with the map stays inside the latency gate.
