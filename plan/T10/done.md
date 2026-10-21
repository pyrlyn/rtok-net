# T10. Logging: rotating log and `rtok logs` — done

What was done:
- `src/Rtok.Log` (foundation layer) builds rtok's one Serilog pipeline in code from `[log]`
  (`RtokLog.Create`):
  - The minimum level is the `[log] level` floor, or `RTOK_LOG` when that asks for more, so an
    event below both reaches no sink. The file and `to_db` sinks sit at the floor, the stderr
    sink at `RTOK_LOG`.
  - Library categories (`Microsoft`, `System`, `ModelContextProtocol`) are held at warn unless
    the floor or `RTOK_LOG` is debug, so EF's SQL commands stay out of an info log.
  - `RotatingFileSink` is an `ILogEventSink` over Pyrlyn.RotatingLog. It handles `.N` rotation,
    the `errors.log` copy and lock-safe appends. Serilog.Sinks.File is not used.
  - Source and name come from the `Source`/`Name` properties (`ForSource`, `Named`), else from
    the `ILogger` category and event id name.
  - `StoreLogSink` (`to_db`) writes a `logs` row for rtok's own events only, those with a
    `Source`. The store opens on the first row; a store that will not open turns the sink off,
    and a refused insert drops that row.
  - A path that cannot be a file leaves the logger without a file. No sink error reaches the
    caller.
  - `RTOK_LOG` (error, warn, info, debug or trace; off, 0 or empty is silent) writes the same
    line format to stderr through Serilog.Sinks.Console.
  - `RtokLog.LoggerFactory` is the Serilog.Extensions.Logging bridge for EF Core, ASP.NET Core
    and the MCP SDK. `StoreOptions.LoggerFactory` hands it to EF (`UseLoggerFactory`).
- Reading the log:
  - `LogTail` reads newest first across `.1`, `.2`, … and stops at the first missing file. It
    drops a half-written last line.
  - `LogScreen` numbers rows `1` = newest and colours the level: red error, yellow warn, dim
    debug.
  - `LogFollow` keeps the file it reads open and tells a rotation by device and inode. For
    that, Pyrlyn.FsKit 0.2.0 adds a public `FsPath.TryGetFileId` (`FileId` record struct).
    - After a rotation it reads the rest of the old file through its handle, then any
      generations that rotated past between two polls, then the new file.
    - A truncation in place restarts from the top.
  - `LogWatch` is one loop: it repaints in place on a TTY (cursor up, clear down; no raw mode)
    and appends with continued numbering when piped. It polls every 200 ms, stops on Ctrl-C,
    and ends cleanly when the reader closes the pipe.
- `rtok logs [--lines N] [--json] [--config]`, `logs export` (same flags) and `logs watch` live
  in Rtok.Cli. `--json` prints the lines as an array, newest first. `logs watch` is in
  JsonTests' list of commands without `--json`. The command model, help, man and completion
  snapshots now include the three commands.
  - `--lines` defaults to `[log] lines`.
  - Empty output prints "no logs yet" as an info line.
  - Colour follows T11's `[ui] color` rule.
  - Rtok.Cli imports `StoreHost.targets`, because `to_db` opens the store.
  - `Program.cs` builds the logger from the config the command loads (with its `--config`). It
    logs argv at debug (`cli/argv`) and disposes the logger at exit. `rtok hook` skips all of
  this to keep its budget.
- `FlagCoverageTests` maps the `logs` flags to `[log]` keys.

Not ported (outside the done criteria): the `tspin` pretty mode and the
demon's child-output piping (the demon is a later task).

How it was checked:
- `tests/Rtok.Log.Tests`:
  - the pipeline: an empty `[log]` from the config loader (home path, 1 MiB, 5 files, info,
    `to_db`), floor and `IsEnabled`, line format, rotation at `max_bytes` keeping `.1` to `.5`
    and dropping the oldest, `errors.log`, `to_db` row for own events only, `to_db` off never
    opening the store, a failing store costing no line, a file that cannot be written costing
    no row, an impossible path, and `RTOK_LOG` on and off;
  - the MEL bridge (category and event name);
  - library levels at info and debug;
  - EF logging through `StoreOptions.LoggerFactory`;
  - tail and screen;
  - follow: rotation, the rest of a file that retention deleted (read through the handle),
    the same first line in two generations, several rotations between polls, truncation, an
    empty replacement, a missing path;
  - the watch loop piped, on a TTY and with a broken pipe, plus a live piped `Run` across a
    rotation;
  - writer processes: four relaunches of the test assembly log through Serilog at 1 KB per
    file, and every line is found in the `.N` files.
- `tests/Rtok.Cli.Tests/LogsCommandTests` drives the binary:
  - Verify: "no logs yet", then a bare `export`; numbering across rotation with `--lines 3`;
  - `export --lines`;
  - `--json`;
  - `RTOK_LOG` off (silent stderr) and on (the argv line);
  - a piped `logs watch` that counts on across a rotation, prints each line once and uses no
    escapes.
- Fuzz target `log-tail` (`tests/Rtok.Fuzz/Targets/LogTailText.cs`, seeds in
  `corpus/log-tail/`): any bytes as a live file, then an append, a rotation or an in-place
  truncation. `LogTail.Read`, `LogFollow.Primed` and `Poll` return one whole line per newline
  and never throw, on non-UTF-8 and half-written files too. Each run gets its own directory,
  because `Rtok.Fuzz.Tests` runs samples on several threads.
- `Rtok.Core.Tests` arch: nothing in `Rtok.Hook`'s project closure has a Serilog
  `PackageReference`.
- `Rtok.AotTests/LogTests` (published NativeAOT, run by Rtok.Smoke): 1 KB `max_bytes` rotates
  to `.1`, the last line is in the live file, and each line is a `logs` row.
- Pyrlyn.FsKit: a file keeps its id across a rename, and a new file at the path gets another.
- Gates: `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test`, `dotnet csharpier
  check .`.
- `dotnet publish src/Rtok.Cli -c Release -r osx-arm64` (NativeAOT) succeeds and shows no IL
  warning from Serilog or its sinks. The rest is EF's own warnings, which StoreHost.targets lets
  through:
  - IL2104 and IL3053, as in T8;
  - IL3002 from `SpatialiteLoader` and `DependencyContext`, now reachable because `rtok` opens
    the store.
  The published `rtok logs` prints the argv line under `RTOK_LOG=debug`. `rtok-hook` references
  none of Serilog.
