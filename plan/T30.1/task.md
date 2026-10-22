---
status: todo
priority: P1
complexity: 3
readiness: 0%
agent:
---

# T30.1. Measure: the replay bench and the stats seams

T30 landed the readers, `rtok stats`, baselines, `--calibrate`, `rtok bench` and the o200k counts. Two parts wait on other lanes and live here.

Done when:
- The replay bench runs the shared fixture through the hook, MCP, cmd and read surfaces and counts only savings with a Measurement row. Needs T13, T15, T17, T19 and T22.
- `rtok stats` fills its seams from the plugins: `StatsSignals` (cmd filter kinds and visible programs from T17, read deny reasons and outlines from T19), `Replay` with the archive clip (T9) and the proxy lanes (T29).
- `rtok bench --suite graph` runs once `rtok mcp` (T15) serves the graph tools.

Tests:
- `Rtok.Measure.Tests` integration: the replay bench on the shared fixture saves at least 76 %, and every saving it counts has a Measurement row whose bytes equal the returned bytes.
- `Rtok.Cli.Tests` Verify: the `rtok stats` goldens on the shared fixture with the filter-kind, deny-reason, archive and proxy-lane rows filled.

Spec: catalog A16, catalog B22 in [docs/port/catalog.md](../../docs/port/catalog.md).

Check: The replay bench reaches rtok's floor (76 %) on the shared fixture, and the filled stats goldens match.
