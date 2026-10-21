---
status: todo
priority: P1
complexity: 3
readiness: 0%
agent:
---

# T12.2. Package Pyrlyn.ChangePreview: dry-run diffs and stat previews

Every write command has `--dry-run`, and destructive commands default to a dry run. They all print the same preview.

Done when:
- The package prints:
  - a git-style unified diff with `a/` and `b/` headers (on DiffPlex);
  - stat lines and removal lines with sizes, plus a totals line;
  - human-readable byte sizes;
  - colour only where the terminal allows it.
- A serializable model backs `--json` output.

Spec: catalog A31 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T416.1, T416.2, T416.3, T416.4; open T416 ([open work](../../docs/port/open-work.md)).

Check: Verify snapshots match for edit, stat, removal and totals output, with and without colour.
