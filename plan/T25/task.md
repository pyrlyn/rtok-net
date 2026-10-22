---
status: todo
priority: P2
complexity: 5
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

Tests:
- `Rtok.Plugins.Graph.Tests` integration: projects are registered with origin manual, session, worktree, mcp or reference, and `auto_add_projects` adds them on session start, worktree add/adopt and MCP calls.
- `Rtok.Plugins.Graph.Tests` integration: reference discovery finds Cargo path/[patch]/workspace, npm file:/link:/workspace: with `!negative` patterns subtracted, go.mod replace/go.work, pyproject paths and .gitmodules, one fixture each.
- `Rtok.Plugins.Graph.Tests` unit: discovery stops at `reference_depth` 3 and `max_auto_projects` 20.
- `Rtok.Plugins.Graph.Tests` CsCheck: for any link graph with cycles, scope resolution terminates and returns each project once, and a self link is refused.
- `Rtok.Plugins.Graph.Tests` unit: an unlinked auto link stays unlinked after the next discovery.
- `Rtok.Plugins.Graph.Tests` unit: `symbol`, `callers`, `impact`, `explore` and `outline` take `project`, default to the cwd project plus its links, and a missing path fails with `<path>: No such file or directory (os error 2)`.
- `Rtok.Cli.Tests` Verify: `rtok graph projects --json` output with projects in the ok, stale, not indexed and missing states.
- `Rtok.Plugins.Graph.Tests` Verify: `rtok graph diff --from <ref> --to working` on a temp git repo builds the old side from `git ls-tree` and `cat-file --batch` without a checkout and matches the diff fixtures, including link changes and "changed, not analysed".
- `Rtok.Plugins.Graph.Tests` unit: MCP `graph_diff` returns a capped summary whose paging id expands to the full diff byte for byte.
- `Rtok.Plugins.Graph.Tests` integration: `rtok graph export --format json` validates against the `rtok.graph.v1` schema, redacts home, user and absolute roots by default and keeps them with `--no-redact`.
- `Rtok.Plugins.Graph.Tests` Verify: svg export of a 250-node fixture draws 200 nodes and "50 nodes hidden".
- `Rtok.Smoke` smoke: the published `rtok graph export --format png` writes a valid PNG.
- `Rtok.Plugins.Graph.Tests` unit: the health score weighs freshness 40, backend 30 and links 30, rings switch at 80 and 50, the scope score is the lowest, and the single-flight capability cache recovers only after two good checks.
- `Rtok.Plugins.Graph.Tests` unit: `affected` maps `tests/foo.rs`, `foo_test.rs`, `foo.test.*`, `foo.spec.*`, `__tests__/`, `test_foo.py` and `foo_test.go` to their source and marks them `← via (by name)`.
- `Rtok.Plugins.Graph.Tests` integration: `/` and `$HOME` are refused as roots, and symbol rows already stored for them are dropped (rtok T356).

Spec: catalog A12, catalog B15 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T230, T329, T329.1, T329.2, T329.3, T329.4, T329.4.1, T329.4.2, T329.5, T329.6, T329.7, T329.8, T329.9, T329.10, T329.11, T329.16, T329.17, T329.18, T329.19, T329.24, T329.25, T329.29, T329.30, T329.31, T329.34, T329.40, T329.41, T329.42, T329.54, T334, T335, T336, T337, T367, T372, T451, T476, T477, T481, T485, T486, T542, T543, T544, T545, T546, T561, T567, T571; open T356 ([open work](../../docs/port/open-work.md)).

Fuzz: `manifest-refs` in `tests/Rtok.Fuzz`: each manifest reader over any bytes gives references or none, within the depth and count caps.

Check: Export output validates against the `rtok.graph.v1` schema, diff fixtures match, and refusing `$HOME` as a root is tested.
