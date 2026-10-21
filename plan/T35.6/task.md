---
status: todo
priority: P2
complexity: 3
readiness: 0%
agent:
---

# T35.6. Agent hosts: desktop restart

A desktop host app that is running reads its config only at start, so rtok closes it before writing and reopens it afterwards.

Done when:
- Before writing a desktop host's config, a running app is closed (waiting up to 30 s) and reopened afterwards; every preflight runs before any app is quit; a failure prints a "restart manually" warning; `--no-restart` skips it.
- Processes go through T11's ProgressRunner.

Tests:
- `Rtok.Agents.Hosts.Tests` unit: a running desktop app (fake process seam) is closed before the write and reopened after, every preflight runs before any quit, a failure prints "restart manually", and `--no-restart` skips it.

Spec: catalog A19, catalog B3 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: the restart part of T35's list.

Check: Restart tests with a fake process table cover close, wait, reopen, the timeout warning and `--no-restart`.
