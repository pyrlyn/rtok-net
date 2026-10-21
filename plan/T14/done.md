# T14. Done

What was done:
- `src/Rtok.Hook.Contracts` (no dependencies, shared by client and resident):
  - the wire: a u32 LE length frame; the request carries version, config fingerprint, event, host, cwd, host variables, pid and stdin; the reply is Served, RefusedVersion or RefusedFingerprint, plus stdout;
  - the env fingerprint: `RTOK_*`, HOME, USERPROFILE and the OTLP variables. GROK_HOOK_EVENT and DEVIN_PROJECT_DIR travel in the request, so one resident serves every host;
  - `RtokHome`: T7's resolver, moved here from `Rtok.Config` so that config, client and resident share one home. Rtok.Config now references this project;
  - `HookEndpoint`: `<home>/run/` forced to 0700, a symlink refused, with `hook.sock`, `hook.lock`, `spawn.lock` and `queue/`. On Windows the pipe is `rtok-hook-<fnv(home)>` with `CurrentUserOnly`;
  - `HookBacklog`: the queue, at most 256 entries, files 0600, replayed oldest first;
  - `HookClient`: the exchange within the 50 ms budget.
- `src/Rtok.Hook`, the client. It reads stdin up to 8 MiB, exchanges it and prints the reply, always exiting 0. The fallback follows T4:
  - the resident is down or refused the version → queue the request and spawn `rtok hook --serve` detached, at most once per 2 s;
  - the fingerprint is refused or there is no endpoint → spawn a detached one-shot `rtok hook <event>` with stdin from a 0600 temp file;
  - anything else → `{}`.
  - It spawns through posix_spawn with setsid (CreateProcessW on Windows) and finds `rtok` only beside its own real path.
- `src/Rtok.Hooks`:
  - `HookResident`: one resident per home under `hook.lock`. It binds `.s<pid>`, chmods it 0600 and renames it to `hook.sock`. It refuses another fingerprint, and on another version it answers and exits. It replays the queue and exits within a 1 s tick when its lock or socket vanishes;
  - `HookHandler`, the seam T13 fills (`HookHandlers.NoOp` until then);
  - `HookClientLink`: links `rtok-hook` beside the first `rtok` on PATH when that `rtok` is the running exe. It never replaces a regular file or a foreign link, and re-points a dangling or other-version link;
  - `HookCommandLine.Posix`: the manifest command line. It tries `rtok-hook`, then `rtok hook`, then the same pair in `~/.ketch/bin`, then `exit 0`.
- `src/Rtok.Cli`: `rtok hook [<event>] [--host h] [--serve]`. `--serve` creates the home, enters it, links the client and runs the resident. `hook.serve` is listed as a one-call action key in `FlagCoverageTests`.
- Tests:
  - `tests/Rtok.Hooks.Tests` (66): wire round trips and malformed frames (CsCheck), fingerprint, home, endpoint and backlog. The in-process resident is covered for served, both refusals, one per home, socket modes, replay and lock or socket vanished. Also the PATH link cases and the manifest line;
  - `tests/Rtok.Smoke/HookTests.cs` on the AOT binaries (through `Rtok.Testing.HookSandbox`): autostart, then served with an empty queue and the socket at 0600; a different fingerprint still answered; a hung resident costs the budget and no more;
  - `tests/Rtok.Latency` now starts the resident and gates p95 ≤ 10 ms, plus the median against the baseline.

Measurements (osx-arm64, hyperfine `-N`, 200 runs, resident up):

| fixture | p50 ms | p95 ms |
| --- | --- | --- |
| post_tool_use | 5.84 | 6.87 |
| pre_tool_use | 5.55 | 6.24 |
| session_start | 5.45 | 6.41 |

The baseline moved up from 4.4–4.8 ms (T4: a client that only printed `{}`) to these medians. The client now does a real socket exchange. After the rebase onto T7, the gate ran at load average 15–22 (parallel sessions building). The medians were 6.2–6.6 ms, p95 stayed under 10 ms, and one run failed the 120 % median check. So the baseline above is the quieter run.

Notes for later tasks:
- T13: the manifest generator takes its command from `HookCommandLine.Posix`, and its handler replaces `HookHandlers.NoOp` in `HookCommand`.
- T52: the Windows paths are built but not run: the named pipe, CreateProcessW, and the one-shot fallback (it does not hand stdin over yet). Neither are the latency numbers on Windows.
- Linux: the resident-up gate was run only on osx-arm64; `baseline.json` has no Linux entry, and CI does not run the gate.

Check: `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test` (1055 passed) and `dotnet csharpier check .` are green. The latency gate passes with exit 0. By hand on the AOT binaries: autostart, then served; deleting `hook.lock` stops the resident.
