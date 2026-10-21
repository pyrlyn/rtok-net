---
status: in progress
priority: P1
complexity: 2
readiness: 100%
agent: Claude Code / claude-opus-5-5
---

# T12.10. Package Pyrlyn.RotatingLog: rotating file log for short processes

The hook path cannot afford a logging framework, but it still has to write log lines.

Done when:
- Appends are size-bounded, with `.N` shifting under a lock.
- A level floor is checked before any IO.
- A sibling error copy is kept.
- No DI and no host are needed.
- It is safe across processes.

Spec: catalog A27 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: no rtok task; new for .NET.

Check: A concurrency test with several writer processes loses no line and never leaves more than the configured file count.

Plan:
1. `eng/new-package.sh Pyrlyn.RotatingLog`.
2. `RotatingFileLog` over options (path, max bytes, files, level, errors copy, lock timeout): the level floor is checked before formatting or any IO; a line is `<UTC yyyy-MM-dd HH:mm:ss> <level> <source>/<name>: <message>` with newlines folded to spaces; errors are also copied to a sibling `errors.log` with the same rotation. Writes fail open.
3. Every append runs under a cross-process lock (a temp-dir sibling keyed by the full path, as rtok-log did): re-check the size, shift `.N` (drop the oldest, `files = 0` truncates), then append.
4. Tests: level floor, line shape and civil date, rotation keeps `files` + 1, error copy, a stale-size rotation is dropped, and a concurrency test with several writer processes (the test assembly relaunched as a writer) that checks every line arrived and the file count stays bounded.
