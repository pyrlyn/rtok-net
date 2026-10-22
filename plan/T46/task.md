---
status: todo
priority: P2
complexity: 4
readiness: 0%
agent:
---

# T46. Demon supervisor, `rtok info` and self-update

Long-running services (proxy, mcp, web, the hook resident) need one supervisor (rtok D22, D32).

Done when:
- `rtok demon start|stop|restart|status|kill [<service>]` works: one supervisor per service, backoff, state files, the ownership rules for signalling.
- Detached children inherit no handles.
- `rtok demon upgrade` restarts the same set after an update.
- `rtok info` works.

Tests:
- `Rtok.Demon.Tests` integration: `demon start <fake service>` runs one supervisor through `demon supervise <name>` and writes its state file, and a second start reports it running instead of starting another.
- `Rtok.Demon.Tests` integration: a fake service that keeps exiting is restarted with growing backoff, and `status` reports its bind:port.
- `Rtok.Demon.Tests` integration: `stop`, `kill` and `restart` signal both pids while the supervisor lock is held, only the child for a same-boot state without a lock, and nothing for any other state.
- `Rtok.Demon.Tests` integration: escalation re-reads the state file and never signals a pid it no longer names.
- `Rtok.Demon.Tests` integration: a detached child inherits no handles, so the parent's pipe reaches EOF while the child still runs, on every OS.
- `Rtok.Demon.Tests` integration: `demon upgrade` with `RTOK_UPDATE_CMD` set to a fake updater stops the running services and restarts the same set, also when the updater fails.
- `Rtok.Cli.Tests` Verify: `rtok info` (version, config, store, proxy, disk usage) and `rtok demon status` output, paths scrubbed.
- `Rtok.Smoke` smoke: the published `rtok demon start` then `demon kill` of a fake service leaves no process and no live state behind.

Spec: catalog A28, catalog B23 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T20.1, T40, T43, T73, T80, T597.

Fuzz: `demon-state` in `tests/Rtok.Fuzz`: a corrupt state file reads as stopped and never throws.

Check: Tests start, kill and restart a fake service and check the ownership rules.
