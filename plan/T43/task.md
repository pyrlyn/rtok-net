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

Spec: catalog A25, catalog B20 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T15.0.

Check: The parity test fails when a reading command has no web or TUI page.
