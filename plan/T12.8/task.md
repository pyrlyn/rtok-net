---
status: todo
priority: P2
complexity: 1
readiness: 0%
agent:
---

# T12.8. Package Pyrlyn.OperationIcons: verb icons and gutter

Commands share one verb-to-icon table and a measured two-column gutter.

Done when the table and gutter behave as in the catalog: substring matching in order (uninstall wins over install), and the width is measured, not assumed.

Spec: catalog B21 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok open T436.3 ([open work](../../docs/port/open-work.md)).

Check: Width tests pass for every icon in the table.
