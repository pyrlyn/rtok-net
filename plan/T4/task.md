---
status: todo
priority: P0
complexity: 3
readiness: 0%
agent:
---

# T4. Spike: hook latency and the store on NativeAOT (go/no-go)

The 10 ms hook budget decides the design. It needs numbers from the real binaries on all three OSes before the hook and store code is written (N6-N8).

Done when, on macOS, Linux and Windows:
- A dated table in `research.md` gives the p50/p95 start-to-exit time for each of these binaries:
  - an empty `rtok-hook`;
  - `rtok-hook` with a Unix-socket or named-pipe round trip to a resident;
  - the full `rtok hook` with the config load;
  - an EF Core compiled-model open, insert and query;
  - plain Microsoft.Data.Sqlite.
- The table also records the static-init and module-initializer costs of the full binary.
- `plan/README.md` records the fallback when the resident is absent. Candidates:
  - print the host's no-op reply and spawn the resident detached;
  - run the full hook within a hard deadline.
- It also records whether SQLite links statically into both binaries on each OS.

Spec: catalog A2, catalog B1 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T53.3; open T687 ([open work](../../docs/port/open-work.md)).

Check: `research.md` has the dated three-OS table, and `plan/README.md` N8 names the chosen fallback with its measured cost.
