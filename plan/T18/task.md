---
status: todo
priority: P1
complexity: 4
readiness: 0%
agent:
---

# T18. cmd formatters for command families

Family formatters give the deepest cuts and are where rtok beats its competitors.

Done when:
- Every formatter in the catalog is ported, among them git log/status/diff, grep/rg, ls, find, cat, cargo, pytest, jest, vitest, go test, docker/podman ps, kubectl, gh, the lint tools, df, sqlite3, awk, and loop collapse.
- Each keeps rtok's acceptance rules:
  - 100 % of hashes, paths and WARN/ERROR lines are kept;
  - the output is never longer than the raw text;
  - on failure, stderr and the exit line are kept.

Spec: catalog A4, catalog B12 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T3.3, T673, T674, T675, T676, T677, T678, T679, T680, T681, T682, T683, T684, T685, T905.

Check: Every formatter arm has a golden fixture with a `min_saving` floor, and all goldens pass.
