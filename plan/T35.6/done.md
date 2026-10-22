# T35.6. Agent hosts: desktop restart — done

`rtok agents install|update|uninstall` now closes a running desktop host app before writing its config and reopens it afterwards, because the app reads its config only at start (rtok `restart.rs`).

## What was done

- **Orchestration** (`Restart/DesktopRestart.cs`, rtok `with_restart`), run from `HostRun.Run`:
  1. It plans every host before any app is quit. A host with a running desktop app or CLI binary gets a dry-run preflight (no backup, no prompt, no output) through the same `ApplyHost`. A failing preflight throws while every app still runs (rtok T629). A host whose config would stay the same is left alone.
  2. It quits each planned app in order, polling every 100 ms for up to 30 s.
  3. It writes once for all hosts.
  4. It reopens, in a `finally`, every app that quit, so a failed write still reopens and then throws.
- **No failures from restarting.** Quit, timeout and reopen problems are warnings, never failures:
  - `warning: could not quit <app>: <error>; restart it manually to load the new config`
  - `warning: <app> did not quit within 30 s; restart it manually to load the new config`. The app is not reopened, since it is probably showing its own quit prompt.
  - `warning: could not reopen <app>: <error>; open it manually`
  - They go through `HostSettings.Warn`, which is stderr in the CLI, as they happen.
- **stdout lines** before the blocks:
  - `<host>: would close/reopen <app>` on `--dry-run`, with nothing quit.
  - `<host>: restart your <bin> session to load the new config` for a running CLI. A CLI is never killed.
- **`--no-restart`** on `agents install`, `uninstall` and `update` (`HostRequest.NoRestart`). It is an action-only flag with no config key, as in rtok, and is listed in `FlagCoverageTests`.
- **The real process table** (`Restart/AppProcesses.cs`) runs on T11's ProgressRunner:
  - Detection is a quiet run: `osascript -e 'application "X" is running'` on macOS, `tasklist /FI "IMAGENAME eq X.exe"` on Windows, `pgrep -x` on Linux and for every CLI binary.
  - Quit runs with a `closing X…` spinner: `osascript … to quit`, `taskkill /IM X.exe /F` or `killall X`.
  - Reopen runs with a `reopening X…` spinner: `open -a X`, `cmd /C start "" <path>`, or on Linux a detached start of the path or `xdg-open X`.
  - A start failure or a non-zero exit becomes `<what>: exit status: N <stderr>`. `rtok` itself is never quit.
- **Process seam.** `IAppProcesses` is the seam, and a context built without one sees nothing running. `HostContexts.Of` passes the real table except in a sandboxed run (`RTOK_HOST_SANDBOX`), so no test, CLI subprocess included, can close an app on the machine.
- **`HostCommands`** (`--version` probes) moved from raw CliWrap onto ProgressRunner (quiet, with the Windows shim), so CliWrap left Rtok.Agents.Hosts.
- **`HostProbe.InstalledApps`** lists every installed bundle of an app, and `AppPath` now uses it.

## Deviations

- **Only installed bundles are asked whether they run.** rtok runs `osascript` for every `apps[]` entry, installed or not, and its own `host_running` comment warns that an unknown name can open a "Choose Application" dialog. On Windows rtok also matches the macOS entry's name and then hands `start` a `/Applications/…` path. Here the bundle found on disk gives both the name and the path to reopen.
- **Each probe, quit and open is capped at 30 s.** rtok waits on them with no limit, and a hung `osascript` would hang the install.
- **Restart warnings are not written to rtok's log.** rtok also appends them as `warn agents restart`. The hosts library has no log sink yet.

## How it was checked

- `tests/Rtok.Agents.Hosts.Tests/RestartTests.cs` uses a fake process table (`FakeApps`), with the disk logging each write into the same call list. It ports rtok's `restart.rs` cases:
  - quit, then write, then open, in order;
  - running but unchanged does nothing;
  - not running only writes;
  - a dry run prints `would close/reopen` and calls nothing;
  - `--no-restart` only writes;
  - a failed quit warns "restart it manually" and still writes;
  - an app that never quits (300 ms wait) is written but not reopened;
  - a failed reopen warns;
  - a failed write still reopens, then throws;
  - a failing preflight on any of three hosts closes and writes nothing;
  - two running apps are both closed, then both reopened;
  - a running CLI is named and never closed;
  - an app that is not installed is never asked;
  - every desktop app of every host resolves its known name (rtok `expected_desktop_name`).
- `AppProcessesTests`:
  - `.exe` is added once;
  - a non-zero exit fails with its stderr (Unix);
  - a program that does not start fails under its name.
- The CLI help, completion and man snapshots and `docs/cli/commands.json` carry `--no-restart`.
- The gates are all green: `dotnet build rtok.slnx -c Release -warnaserror --no-incremental`, `dotnet test`, `dotnet csharpier check .` and `npx --yes markdownlint-cli2@0.23.3`.
