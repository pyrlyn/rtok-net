# Pyrlyn.RotatingLog

A size-bounded rotating text log for short processes: a level floor before any IO, .N rotation under a cross-process lock and an errors.log copy, with no host or DI.

## Install

```bash
dotnet add package Pyrlyn.RotatingLog
```

## Use

```csharp
using Pyrlyn.RotatingLog;

var log = new RotatingFileLog(new RotatingFileLogOptions
{
    Path = "/home/me/.rtok/logs/rtok.log",
    MaxBytes = 1_048_576,   // rotate before the live file passes 1 MiB
    Files = 5,              // keep rtok.log.1 … rtok.log.5
    Level = "info",         // error, warn, info or debug
});

log.Write("info", "hook", "pre_tool_use", "rewrote git status");
log.Write("error", "store", "open", "database is locked");   // also copied to errors.log
```

The file holds one line per call, stamped in UTC:

```text
2026-09-09 15:04:05 info hook/pre_tool_use: rewrote git status
2026-09-09 15:04:05 error store/open: database is locked
```

- **Level floor first.** A line quieter than `Level` returns before the line is formatted or
  anything on disk is touched. An unknown level name ranks as `error`.
- **One line per event.** Newlines in the message become spaces.
- **Rotation.** When the next line would make the live file longer than `MaxBytes`, the
  siblings shift (`.1` → `.2`, the oldest beyond `Files` is deleted) and the live file becomes
  `.1`. `Files = 0` truncates the live file instead. A single line longer than `MaxBytes` is
  still written.
- **Errors copy.** `error` lines are also written to `errors.log` beside the live file, with
  the same bounds. `CopyErrors = false` turns it off.
- **Safe across processes.** Every append takes an exclusive lock shared by all processes that
  write the same path, checks the size again, rotates if needed and appends the whole line.
  The lock is a `pyrlyn-log-<hash>.lock` file in the temp directory, not the log, so the
  rename never meets an open handle. The OS releases it when a process dies.
- **Fails open.** `Write` never throws. A line that cannot be written within `LockTimeout`
  (default 1 s), or at all, is dropped.
- No DI, no host, no background thread; AOT-clean. `Time` takes a `TimeProvider` for tests.
