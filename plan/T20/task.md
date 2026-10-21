---
status: todo
priority: P2
complexity: 3
readiness: 0%
agent:
---

# T20. guard plugin: deny repeated read-only calls

Agents re-run the same read-only command or Read; the guard points them at the archive instead.

Done when:
- A duplicate Read or Bash within `window_turns` is denied with the archive id.
- Keys are normalized as rtok does: cwd, cd hops, quoting, the `rtok run` wrapper stripped.
- Read-only command classes are flag-aware, and writers clear the keys.
- The optional Grep/Glob redirect and the skill digest behave as in the catalog.
- The deny path reads only archive metadata.

Spec: catalog A8, catalog B13 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T2.6, T50.4, T55.8, T55.9, T55.16, T57.1, T62.1, T299, T322, T350, T369, T445, T450, T555, T556, T557, T558, T559.

Check: Table tests cover the key normalization and every read-only and writer class from rtok, plus non-ASCII input.
