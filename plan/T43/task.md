---
status: todo
priority: P2
complexity: 3
readiness: 0%
agent:
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
