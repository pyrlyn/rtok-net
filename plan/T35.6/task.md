---
status: in progress
priority: P2
complexity: 3
readiness: 100%
agent: Claude Code / claude-opus-5-5
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

## Plan

1. A process seam, `IAppProcesses`: app running, bin running, quit and open. The real one, `AppProcesses`, runs on ProgressRunner and follows rtok `restart.rs` per OS:
   - macOS: `osascript`, then `open -a`.
   - Windows: `tasklist`, `taskkill /F`, then `cmd /C start`.
   - Linux: `pgrep -x`, `killall`, then a detached reopen.
   `HostCommands` (`--version`) moves onto ProgressRunner too, and CliWrap leaves Rtok.Agents.Hosts.
2. `HostRun.Run` does what rtok `with_restart` does:
   - Plan first: a dry-run preflight for each host with a running desktop app or CLI binary. Any failure stops the run before anything is quit.
   - Quit each target and wait (100 ms poll, 30 s).
   - Write once for all hosts.
   - Reopen everything in a `finally`, so a failed write still reopens.
   - Quit, timeout and reopen problems are "restart it manually" warnings, never failures.
   - `--dry-run` prints `would close/reopen`.
   - A running CLI binary gets `restart your <bin> session`.
   - `--no-restart` (`HostRequest.NoRestart`, no config key) skips all of it.
3. Only installed app bundles are asked whether they run, so osascript never sees an unknown name. A sandboxed run (`RTOK_HOST_SANDBOX`) starts no process.
4. CLI: `--no-restart` on install, uninstall and update. Warnings go to stderr. Regenerate the snapshots.
5. Tests in `Rtok.Agents.Hosts.Tests/RestartTests.cs`, with a fake process table that logs quit, write and open in order: the rtok `restart.rs` cases, the app name table, and the real runner's exit check on Unix.
