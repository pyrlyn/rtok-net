---
status: in progress
priority: P0
complexity: 4
readiness: 100%
agent: Claude Code / claude-opus-5-5
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

Tests:
- `Rtok.Hooks.Tests` integration: a second `rtok hook --serve` on the same home finds the resident's `hook.lock` and exits without binding.
- `Rtok.Hooks.Tests` integration: the resident refuses a client with another version or config fingerprint, and that client takes the fallback.
- `Rtok.Hook.Tests` integration: `rtok-hook` forwards stdin byte for byte to a fake resident over a Unix socket (a named pipe on Windows) and prints its reply unchanged.
- `Rtok.Hook.Tests` integration: a fake resident that never answers makes the client take the T4 fallback after 50 ms, still exiting 0.
- `Rtok.Hook.Tests` integration: with no resident, parallel clients autostart one detached resident, and the lock file rate-limits further starts.
- `Rtok.Hook.Tests` unit: a stdin over the 8 MiB cap bypasses the resident.
- `Rtok.Hooks.Tests` integration: on Unix the socket is created inside a 0700 directory and is never reachable under a wider mode (rtok S7).
- `Rtok.Hooks.Tests` integration: on Windows the named pipe's ACL grants access to the current user only.
- `Rtok.Hooks.Tests` integration: `rtok-hook` is linked beside the first `rtok` on a temp PATH when that binary is the running exe, and a regular file or a foreign link there is left untouched.
- `Rtok.Hooks.Tests` Verify: each generated host manifest names `rtok-hook` first, then `rtok`.
- `Rtok.Smoke` smoke: the published `rtok-hook` with the resident up answers the PreToolUse fixture byte-identical to `rtok hook PreToolUse`.
- `Rtok.Smoke` smoke: the published `rtok-hook` exits 0 with the no-op reply when the resident is killed mid-request.

Spec: catalog A2, catalog B1, catalog B2 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok open S7 ([open work](../../docs/port/open-work.md)).

Check: With the resident up, the latency gate shows `rtok-hook` p95 ≤ 10 ms on the PreToolUse fixture on every OS. With it down, the client answers within 50 ms plus process start.

Plan:
1. `src/Rtok.Hook.Contracts`: the wire (length-prefixed request and reply frames, refusal codes), the env fingerprint, rtok's home and the endpoint (`<home>/run/` at 0700: `hook.sock`, `hook.lock`, `spawn.lock`, `queue/`; a pipe named by home on Windows), and the client exchange with its 50 ms budget.
2. `src/Rtok.Hook`: read stdin (8 MiB cap), then exchange. Resident down or exiting (version refused): print the no-op reply, queue the request, and spawn `rtok hook --serve` detached, at most once per 2 s (`spawn.lock`). Fingerprint refused or no endpoint: print the no-op reply and hand stdin to a detached one-shot `rtok hook`. Any other failure: print the no-op reply.
3. `src/Rtok.Hooks`: the resident. One per home through `hook.lock`; the socket is bound in the 0700 run dir and chmod 0600, or the pipe is created with `CurrentUserOnly`. It refuses another version (then exits) or another fingerprint, replays the queue for writes only, and exits when its lock or socket vanishes. It runs behind a handler seam that T13 fills (no-op until then). The same project gets the `rtok-hook` PATH link and the hook command resolver line for T13's manifests.
4. `src/Rtok.Cli`: `rtok hook [<event>] [--host h] [--serve]`.
5. Tests: wire, fingerprint and home units; the resident in-process (served, refused, one per home, modes, replay, lock vanished); the PATH link cases; smoke tests on the AOT binaries (autostart, served, a hung resident answered within 50 ms). The latency gate measures `rtok-hook` with the resident up and gates p95.
