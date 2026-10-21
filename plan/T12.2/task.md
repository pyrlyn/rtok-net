---
status: in progress
priority: P1
complexity: 3
readiness: 100%
agent: Claude Code / claude-opus-5-5
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

Plan:
1. `eng/new-package.sh Pyrlyn.ChangePreview`; DiffPlex as the one new dependency.
2. Port the `change-preview` crate: `Preview` with `Edit(path, before, after)` and `Remove(path, bytes, files)`, `Totals`, `Render(Diff|Stat)`; the unified diff with `a/` `b/` headers, three lines of context and `\ No newline at end of file`, built from DiffPlex's line diff; stat lines with bars capped at 40; removal lines; totals lines; `HumanBytes` and `Group`.
3. `Paint(text, color)`: bold file headers, cyan hunks, green and red lines, the crate's escape codes. Whether the terminal allows colour is the caller's decision (T11's per-stream rule), so the package keeps no second copy of it.
4. A source-generated JSON model for `--json`: edits with counts (not the diff text), removals with bytes and files.
5. Tests: the crate's tests, and Verify snapshots of edit, stat, removal and totals output with and without colour.
