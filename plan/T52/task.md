---
status: todo
priority: P1
complexity: 2
readiness: 0%
agent:
---

# T52. Hook latency and the store on Windows

T4 measured macOS and Linux only; there was no Windows host. The 10 ms hook budget and the resident fallback (N8) still need Windows numbers, where the socket is a named pipe and process start costs more.

Done when, on a Windows host or Windows CI:
- `research.md` F gets a dated Windows table with the same rows as the macOS and Linux ones (`spikes/T4/bench.sh` ported to PowerShell or run under Git Bash), with `rtok-hook` talking to the resident over a named pipe.
- The detached spawn of the fallback is measured with `CreateProcess` and no inherited handles.
- `plan/README.md` N8 confirms the fallback for Windows or names the Windows one, and records whether SQLite links statically there (StaticLink.e_sqlite3 ships `e_sqlite3.lib`).

Spec: catalog A2, catalog B1 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T53.3; open T687 ([open work](../../docs/port/open-work.md)).

Check: `research.md` F has the dated Windows table, and N8 states the Windows fallback with its measured cost.
