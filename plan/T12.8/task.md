---
status: in progress
priority: P2
complexity: 1
readiness: 100%
agent: Claude Code / claude-opus-5-5
---

# T12.8. Package Pyrlyn.OperationIcons: verb icons and gutter

Commands share one verb-to-icon table and a measured two-column gutter.

Done when the table and gutter behave as in the catalog: substring matching in order (uninstall wins over install), and the width is measured, not assumed.

Spec: catalog B21 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok open T436.3 ([open work](../../docs/port/open-work.md)).

Check: Width tests pass for every icon in the table.

Plan:
1. `eng/new-package.sh Pyrlyn.OperationIcons`; Wcwidth (NuGet) measures terminal columns, which the BCL cannot.
2. Port the shared `operation-icons` table rtok and ketch use: twelve rows matched as substrings of the lowercased verb, in order (`uninstall` before `install`); `Icon(verb)`, `Icon(rows, verb)` for a caller's own rows, `Padding`, `Gutter` to `IconWidth` = 2 columns plus a space.
3. Width is measured per icon (a narrow symbol with U+FE0F counts as two, as unicode-width does), never assumed.
4. Tests: each verb's icon, uninstall over install and case, unknown verbs, caller rows, every table icon measures `IconWidth`, narrow and empty gutters, gutters align.
