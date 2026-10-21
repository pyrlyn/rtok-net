---
status: todo
priority: P2
complexity: 4
readiness: 0%
agent:
---

# T25. graph projects, links, diff, export and health

Graph answers span linked projects, and users need diffs, exports and health signals.

Done when:
- The projects registry has origins and auto-add from manifest references, with depth and count caps.
- Links are cycle-safe, and an unlink is remembered.
- A `project` argument works on every graph tool and command.
- `rtok graph diff` builds its old side from the object database.
- `rtok graph export --format json|svg|png` follows the `rtok.graph.v1` schema, with redaction on by default.
- The health score, alerts and capability cache work.
- `affected` maps tests by name convention.
- `/` and `$HOME` are never indexed, and old rows for them are dropped (rtok T356).

Spec: catalog A12, catalog B15 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T230, T329, T329.1, T329.2, T329.3, T329.4, T329.4.1, T329.4.2, T329.5, T329.6, T329.7, T329.8, T329.9, T329.10, T329.11, T329.16, T329.17, T329.18, T329.19, T329.24, T329.25, T329.29, T329.30, T329.31, T329.34, T329.40, T329.41, T329.42, T329.54, T334, T335, T336, T337, T367, T372, T451, T476, T477, T481, T485, T486, T542, T543, T544, T545, T546, T561, T567, T571; open T356 ([open work](../../docs/port/open-work.md)).

Check: Export output validates against the `rtok.graph.v1` schema, diff fixtures match, and refusing `$HOME` as a root is tested.
