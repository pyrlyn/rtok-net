---
status: in progress
priority: P1
complexity: 2
readiness: 100%
agent: Claude Code / claude-opus-5-5
---

# T10. Logging: rotating log and `rtok logs`

One bounded log for everything in `rtok` (rtok D26), through Serilog (N20). `rtok-hook` does not log.

Done when:
- `Rtok.Log` builds the Serilog logger in code from `[log]`; the keys behave as in the catalog: path, max_bytes 1 MiB, 5 files, level, to_db. The Serilog minimum level is the level floor, so lines below it never reach a sink.
- The file sink is an `ILogEventSink` over Pyrlyn.RotatingLog (T12.10): `.N` rotation, the `errors.log` copy, lock-safe appends. Serilog.Sinks.File is not used.
- One Serilog pipeline is the single funnel: it writes the file line and, with `to_db`, the logs row (a second sink), and fails open: no sink error reaches the caller.
- EF Core, ASP.NET Core and MCP SDK `ILogger` output joins the same pipeline through Serilog.Extensions.Logging.
- `rtok logs [--lines N]`, `logs export` and `logs watch` work. `watch` follows rotation by file identity and restarts after in-place truncation.
- `RTOK_LOG` debug output goes to stderr through Serilog.Sinks.Console.
- Serilog and its sinks add no AOT or trim warnings to the Release build of `rtok`, and `rtok-hook` references none of them.

Tests:
- `Rtok.Log.Tests` unit: an empty `[log]` builds a logger with path `~/.rtok/logs/rtok.log`, `max_bytes` 1048576, 5 files, level info and `to_db` true.
- `Rtok.Log.Tests` unit: the Serilog minimum level equals `level`, so an event below it reaches no sink.
- `Rtok.Log.Tests` unit: rotation at `max_bytes` keeps 5 `.N` files and drops the oldest.
- `Rtok.Log.Tests` unit: an error line is also copied to `errors.log`, and an info line is not.
- `Rtok.Log.Tests` integration: with `to_db` one event writes one file line and one logs row in a temp store, and without it no row.
- `Rtok.Log.Tests` unit: a failing file sink or db sink never throws to the caller, and the other sink still writes.
- `Rtok.Log.Tests` integration: four processes log at once; every line lands in the `.N` files.
- `Rtok.Log.Tests` unit: a message logged through a Serilog.Extensions.Logging `ILogger` reaches the same file sink with its category.
- `Rtok.Log.Tests` integration: follow returns the unread tail across rotation, retention and an empty replacement file, and restarts from the top after in-place truncation.
- `Rtok.Cli.Tests` Verify: `rtok logs --lines 3` output numbered 1=newest across rotated files.
- `Rtok.Cli.Tests` Verify: `rtok logs` prints "no logs yet" on an empty directory, and `rtok logs export` prints no numbers and no colour.
- `Rtok.Cli.Tests` integration: `rtok logs watch` piped appends new lines and keeps following after a rotation.
- `Rtok.Cli.Tests` integration: `RTOK_LOG=debug` writes debug lines to stderr and nothing extra to stdout.
- `Rtok.Core.Tests` arch: `Rtok.Hook` references no Serilog assembly.
- `Rtok.AotTests` AOT: the Release `rtok` publishes with no Serilog trim or AOT warnings and writes a rotated log line.

Spec: catalog A27 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T24.0, T24.1, T24.2, T24.3, T24.4, T24.5, T225, T225.1, T270, T589.

Fuzz: `log-tail` in `tests/Rtok.Fuzz`: `logs` and `watch` over truncated, non-UTF-8 and half-written files print only whole lines and never throw.

Check: Rotation, retention and follow tests pass, including truncation and an empty replacement file. A test logs through Serilog from several processes and finds every line in the `.N` files.

Plan:
1. `src/Rtok.Log` (foundation layer): `RtokLog.Create(LogSection, options)` builds the Serilog logger in code. The minimum level is the lower of `[log] level` and `RTOK_LOG`. The file and db sinks are restricted to `[log] level`, the console sink to `RTOK_LOG`. Library categories (EF Core, ASP.NET Core, MCP SDK) stay at warn unless the floor is debug.
   - `RotatingFileSink` over Pyrlyn.RotatingLog. Source and name come from the `Source`/`Name` properties, else from `SourceContext` and the MEL `EventId`.
   - `StoreLogSink` opens the store lazily, writes the logs row and disables itself on the first failure.
   - `SerilogLoggerFactory` for `ILogger` users; `StoreOptions.LoggerFactory` hands it to EF.
2. Reading: tail (newest first across `.N`, half-written lines dropped), the numbered and coloured screen, and `Follow`.
   - `Follow` keeps the open handle of the file it reads and finds rotation by device and inode, through a new public `FsPath.TryFileId` in Pyrlyn.FsKit. It restarts after truncation in place.
   - One watch loop: repaint on a TTY, append when piped.
3. `rtok logs [--lines N]`, `logs export` and `logs watch` in Rtok.Cli, which imports StoreHost.targets because `to_db` opens the store. Program.cs builds the logger from the loaded config (defaults when the load fails), logs argv at debug, and flushes at exit.
4. Tests:
   - `tests/Rtok.Log.Tests`: pipeline, sinks, fail open, MEL and EF bridges, tail, follow (rotation, several rotations between polls, truncation, empty replacement), and the watch loop. The multi-process test relaunches the test assembly as Serilog writers.
   - Rtok.Cli.Tests: the commands against the binary, and `RTOK_LOG` off and on.
5. Gates, plus an AOT publish of `rtok` with no Serilog AOT or trim warnings.

