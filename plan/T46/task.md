---
status: todo
priority: P2
complexity: 3
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

Spec: catalog A28, catalog B23 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T20.1, T40, T43, T73, T80, T597.

Check: Tests start, kill and restart a fake service and check the ownership rules.
