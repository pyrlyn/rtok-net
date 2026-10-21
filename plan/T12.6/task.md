---
status: todo
priority: P1
complexity: 1
readiness: 0%
agent:
---

# T12.6. Packages Pyrlyn.Shlex and Pyrlyn.Durations

The Bash rewrite and the guard need POSIX word splitting; worktrees, junk and stats parse `30m`-style durations.

Done when:
- Shlex splits and quotes POSIX words. An unterminated quote is reported, not guessed.
- Durations parse and format `0s`, `30m`, `24h`, `7d`, and reject `5x`, `-1d` and empty input.

Spec: catalog A4, catalog B13, catalog B5 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: no rtok task; new for .NET.

Check: Table tests from rtok's cases pass for both packages.
