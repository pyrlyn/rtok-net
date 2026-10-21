---
status: in progress
priority: P1
complexity: 3
readiness: 100%
agent: Claude Code / claude-opus-5-5
---

# T33. Package Rtok.Agent.Sdk: reversible, idempotent installs

Every host installer shares one contract for safe edits (rtok D28).

Done when:
- The Apply contract supports dry-run, backup and yes, and prints NO_CHANGES on an empty diff.
- Atomic writes go through Pyrlyn.FsKit.
- `_backup/<name>.bak-<ts>` generations work: capped by `[setup] backup_files`, deduped per file, the newest copy kept, and a no-op run deletes its own copies.
- Removal is ownership-checked: rtok's exact entry, an edited entry (prompt), or a foreign entry (leave it).
- The package is published from `packages/` (rtok T464).

Spec: catalog A19, catalog B3 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok open T464 ([open work](../../docs/port/open-work.md)).

Check: Tests cover install twice (≤1 entry), the backup prune and dedup rules, and each ownership case.

Plan:
1. `eng/new-package.sh Rtok.Agent.Sdk`; reference `Pyrlyn.FsKit`.
2. `Apply` (dry-run, backup, backup_files, yes; `Writes(report)` gate, `NoChanges`), `Prompter` seam for confirm/accept (no TTY → no).
3. `Backups`: `_backup/<name>.bak-<ts>[-<n>]`, dedup per base name, `Prune` keeps the newest, `Stale`; `BackupRun` collects a run's copies and deletes them when the run changed nothing.
4. `HostFile.Write` / `WriteJson` through `AtomicFile`; strict JSON read with a file:line refusal; `EditJson`, `ObjectAt`, `RegisterServer`/`RegisterMcp`.
5. Ownership: `JudgeOwned` (exact → remove, edited → prompt, foreign → leave), `UnregisterOwned`.
6. Tests: install twice (≤1 entry, NO_CHANGES, no new copy), prune/dedup/same-second numbering, each ownership case, symlink write-through.
