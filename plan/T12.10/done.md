# T12.10 done

New package `packages/Pyrlyn.RotatingLog` (0.1.0), no new dependencies:

- `RotatingFileLog.Write(level, source, name, message)` over `RotatingFileLogOptions` (path, `MaxBytes` 1 MiB, `Files` 5, `Level` info, `CopyErrors`, `LockTimeout`, `Time`). No DI, no host, no background thread.
- The level floor (`error` > `warn` > `info` > `debug`; an unknown name ranks as `error`) is checked before the line is formatted or the disk is touched.
- A line is `<UTC yyyy-MM-dd HH:mm:ss> <level> <source>/<name>: <message>`, newlines folded to spaces (rtok-log's shape).
- Each append takes an exclusive cross-process lock (a `pyrlyn-log-<fnv64 of the full path>.lock` in the temp dir, `FileShare.None`: flock on Unix, the share mode on Windows; released by the OS when a writer dies), re-checks the size, shifts `.N` (the oldest beyond `Files` is deleted; `Files = 0` truncates) and appends the whole line.
- `error` lines are also copied to a sibling `errors.log` with the same bounds.
- `Write` never throws: an unwritable path or a lock held past `LockTimeout` drops the line.

Commit: `T12.10: Package Pyrlyn.RotatingLog: rotating file log for short processes`.

Checked: 17 tests, ported from rtok-log's (civil date stamps, level floor, below-floor writes touch no disk, one line per event, error copy, rotation keeps `Files` + 1, a stale-size rotation is dropped under the lock, fail open) plus two multi-process tests: six writer processes (the test assembly relaunched as a writer) through dozens of rotations lose no line and keep every file under `MaxBytes`, and with `Files = 2` leave exactly `app.log`, `.1`, `.2` (and the same for `errors.log`) holding only whole lines. With the lock removed the first one fails. `dotnet test` (179) and `dotnet csharpier check .` green. `dotnet build rtok.slnx -c Release -warnaserror` is green for this package; it still fails on `main` in `Pyrlyn.FsKit` tests (CA1416 in `AtomicFileTests.cs`, from T12.1, lane L6), which this task does not touch.
