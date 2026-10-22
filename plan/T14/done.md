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

Tests section of `task.md` (added to `plan/` after the first merge, covered in a follow-up commit). There is no `Rtok.Hook.Tests` project: the client is an exe, so its cases run on the AOT binary in `tests/Rtok.Smoke/HookTests.cs`, against a `FakeResident` where one is needed.
- A second `--serve` on the same home exits without binding: `One_resident_per_home` and `A_second_resident_leaves_the_first_serving`.
- Another version or fingerprint is refused and takes the fallback: `It_refuses_another_version_and_exits`, `It_refuses_another_fingerprint_and_stays` and `A_client_with_another_fingerprint_still_answers`.
- Stdin forwarded byte for byte, the reply printed unchanged: `The_client_forwards_stdin_and_the_reply_unchanged`.
- A resident that never answers costs 50 ms: `A_hung_resident_costs_the_client_its_budget_and_no_more`.
- Parallel clients start one resident: `Parallel_clients_start_one_resident`. With the spawn gate turned off, 8 residents start, so the test catches it. The gate is now an flock on `spawn.lock` that holds the last start's ticks; the first merge only checked its mtime, and parallel clients could race that check.
- Stdin over the cap bypasses the resident: `Stdin_over_the_cap_bypasses_the_resident`.
- The 0700 directory and the 0600 socket: `The_socket_is_owner_only_inside_a_0700_directory` and the autostart smoke test.
- The PATH link: `HookClientLinkTests`.
- Served byte-identical to `rtok hook PreToolUse`: `The_client_starts_the_resident_which_then_serves_it`.
- A resident killed mid-request: `A_resident_gone_mid_request_still_gets_the_no_op_reply`.
- Open:
  - the Windows pipe ACL is T52's (Windows is not run here);
  - Verify on the generated manifests is T13's (it generates them); `HookCommandLineTests` pins the command line they will take.

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

Check: `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test` (1073 passed) and `dotnet csharpier check .` are green. The latency gate passes with exit 0. By hand on the AOT binaries: autostart, then served; deleting `hook.lock` stops the resident.

Follow-up: with `RTOK_SMOKE_BIN` holding the real `rtok` beside `rtok-hook` (fixed
`eng/publish.sh`, b0e3454), the smoke tests that drive the client alone spawned a resident:
`A_hung_resident_costs_the_client_its_budget_and_no_more` could not bind its address, and a
temp home could not be deleted. They now run a copy of the client in the home with no `rtok`
beside it (`LoneClientAsync`). `HookTests` pass 17 of 17 against `artifacts/dist/osx-arm64`
and self-published, and the hung test 3 of 3.
