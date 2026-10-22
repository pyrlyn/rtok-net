---
status: todo
priority: P2
complexity: 3
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

Tests:
- `Rtok.Plugins.Graph.Tests` unit: the file graph from a symbol fixture weighs an edge through a rare name above one through a common name (IDF).
- `Rtok.Plugins.Graph.Tests` CsCheck: for any file graph, CSR power iteration with 0.85 damping and 20 iterations gives ranks that sum to 1 and match a dense reference within 1e-9.
- `Rtok.Plugins.Graph.Tests` integration: global ranks are stored per root, and two roots keep separate ranks.
- `Rtok.Plugins.Graph.Tests` unit: dirty files, recent files and the last checkpoint's paths raise their personalized rank, and no personalized rank is written to the store.
- `Rtok.Plugins.Graph.Tests` CsCheck: for any budget, the map is filled in one pass, never exceeds the budget, and is byte-identical for the same input.
- `Rtok.Plugins.Graph.Tests` Verify: the SessionStart map of the backtest fixture with `map_rank=refs` and with `map_rank=pagerank`.
- `Rtok.Plugins.Graph.Tests` integration: the backtest fixture reproduces rtok's ranking order.
- `Rtok.Plugins.Graph.Tests` unit: when rank data is missing or fails to load, SessionStart injects no map and passes through unchanged (fail open).
- `Rtok.Latency` bench: SessionStart with the map stays inside the 10 ms median gate.

Check: The backtest fixture reproduces rtok's ranking, and SessionStart with the map stays inside the latency gate.
