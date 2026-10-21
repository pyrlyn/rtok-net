---
status: in progress
priority: P2
complexity: 3
readiness: 50%
agent: Claude Code / claude-opus-5-5
---

# T43. Operator model shared by web and TUI

Everything printed or stored is a page on both web and TUI, and write actions use the same function as the CLI (rtok D23, D27).

Done when:
- `Rtok.Model` builds one plain-data snapshot off-thread with a collapsing queue: Overview, Plugins, Calls, Sessions, Doctor, Logs, Memory, Graph, Config, Services, Worktrees, Hosts, Stats, Usage, Tasks.
- Parity tests map each command to its pages.

Tests:
- `Rtok.Model.Tests` unit: one snapshot built from the fixture store fills every page: Overview, Plugins, Calls, Sessions, Doctor, Logs, Memory, Graph, Config, Services, Worktrees, Hosts, Stats, Usage and Tasks.
- `Rtok.Model.Tests` unit: every type in the snapshot is plain data (records of primitives and read-only collections), and the snapshot round-trips through its STJ source-generated context.
- `Rtok.Model.Tests` integration: the snapshot is built off the caller's thread, and ten refresh requests during one build collapse into a single following build.
- `Rtok.Model.Tests` integration: a reader never waits on a build in progress; it gets the last complete snapshot.
- `Rtok.Model.Tests` integration: a config reload during a build gives a Config page that matches the old file or the new one, never a mix.
- `Rtok.Model.Tests` unit: the Config page lists `key = value (source)` rows with `otel.headers` redacted.
- `Rtok.Model.Tests` unit: with a fake `TimeProvider`, the Worktrees page is reused within its 300 s TTL, and Hosts shows "probing hosts…" until the cached doctor refresh lands.
- `Rtok.Model.Tests` unit: the parity map gives every reading command in the command model a web page and a TUI page, or an EXEMPT entry with a reason.
- `Rtok.Model.Tests` unit: the parity test fails on a test command model with a reading command that has no page, and on an EXEMPT entry whose command no longer exists.
- `Rtok.Model.Tests` unit: each write action the model offers (plugin toggle, doctor fix) calls the same function as its CLI command and returns a dry-run plan before apply.
- `Rtok.Core.Tests` arch: `Rtok.Tui` references `Rtok.Model` and never `Rtok.Store`.

Spec: catalog A25, catalog B20 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T15.0.

Check: The parity test fails when a reading command has no web or TUI page.

Plan (agreed with the creator: build now on per-page sources, wire real ones as their tasks land; the task stays open until doctor fix exists):
1. `src/Rtok.Model`: `Snapshot` of fifteen plain-data pages with an STJ source-generated context; one `IPageSource<T>` per page. Real sources where the data exists now (Config, Plugins, and the store-backed pages); the rest report "not available yet" until their tasks land.
2. `SnapshotBuilder`: builds off the caller's thread, collapses refresh requests into one following build, and readers take the last complete snapshot without waiting. Config is read once per build from a swappable `LoadedConfig`.
3. Caching: a background source with a TTL and a placeholder on `TimeProvider`, for Worktrees (300 s, "reading worktrees…") and Hosts (30 s, "probing hosts…").
4. `ParityMap`: each reading command (one with `--json` in the command model) maps to a web page and a TUI page, or an EXEMPT reason; `Check` reports gaps and stale entries.
5. Write actions: the plugin toggle plans and applies through the same config writer as `rtok config set`; doctor fix waits for the doctor task.
6. `tests/Rtok.Model.Tests` for every listed case, and the architecture rule that `Rtok.Tui` references `Rtok.Model` and never `Rtok.Store`.

Landed: the snapshot, builder, caches, parity map and plugin toggle with their tests; real
sources for Overview, Plugins, Calls, Sessions, Config, Logs (T10, `rtok logs`), Hosts
(T35, `rtok agents list`, read in the background, 30 s; the host context now comes from
`HostContexts.Of`, shared with the CLI) and Usage (T38, `rtok agents usage`, read in the
background, 120 s). `rtok plugins` now reads `plugins.<id>.enabled` from the
config too. The Rust dashboard has a Skills page and no Memory page; this follows the task's
list.

Pending (the task stays open until each is wired; each page says "not available in this build
yet" until then, and its reading command joins `ParityMap`):
- Doctor page and the doctor fix action (plan → diff → apply, stale plan refused): T32, once
  the doctor CLI function is on `main`.
- Graph: T24 (`graph status`, `graph dead`).
- Stats: T30 (`rtok stats`) and T31 (report).
- Tasks: T41 (`task list`).
- Memory: T22. Worktrees: T40 (`worktree list`), behind the 300 s background source.
  Services: T46 (`demon status`, `otel status`).
