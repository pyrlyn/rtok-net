---
status: todo
priority: P0
complexity: 4
readiness: 0%
agent:
---

# T14. Resident hook server and the `rtok-hook` client

Process start is most of the hook budget, so the warm state lives in a resident process (N6, N8).

Done when:
- `rtok hook --serve` runs one resident per home. It refuses a client with another version or config fingerprint.
- The `rtok-hook` client:
  - forwards stdin over a Unix socket (a named pipe on Windows) and prints the reply;
  - waits 50 ms at most, then takes the fallback chosen in T4;
  - autostarts the resident detached, rate-limited by a lock file.
- The socket is created inside a 0700 directory (rtok S7). The Windows pipe has an owner-only ACL.
- `rtok-hook` is linked beside the first `rtok` on PATH when that binary is the running exe. It never replaces a regular file or a foreign link.
- Hook manifests prefer `rtok-hook`, then `rtok`.

Spec: catalog A2, catalog B1, catalog B2 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok open S7 ([open work](../../docs/port/open-work.md)).

Check: With the resident up, the latency gate shows `rtok-hook` p95 ≤ 10 ms on the PreToolUse fixture on every OS. With it down, the client answers within 50 ms plus process start.
