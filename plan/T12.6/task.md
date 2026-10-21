---
status: in progress
priority: P1
complexity: 1
readiness: 100%
agent: Claude Code / claude-opus-5-5
---

# T12.6. Packages Pyrlyn.Shlex and Pyrlyn.Durations

The Bash rewrite and the guard need POSIX word splitting; worktrees, junk and stats parse `30m`-style durations.

Done when:
- Shlex splits and quotes POSIX words. An unterminated quote is reported, not guessed.
- Durations parse and format `0s`, `30m`, `24h`, `7d`, and reject `5x`, `-1d` and empty input.

Spec: catalog A4, catalog B13, catalog B5 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: no rtok task; new for .NET.

Check: Table tests from rtok's cases pass for both packages.

Plan:
1. `eng/new-package.sh` for Pyrlyn.Shlex and Pyrlyn.Durations.
2. Shlex: `Split`/`TrySplit` with POSIX rules (quotes, backslashes, line continuation, `#` comments at a word start); an unterminated quote or a trailing backslash is an error with its position. `Quote` and `Join`.
3. Durations: humantime grammar (`0s`, `30m`, `24h`, `7d`, `1h 30m`) and the `--since` grammar (`<n>`, `<n>d`, `<n>h`); both reject `5x`, `-1d` and empty; overflow is an error. `Format` (exact, round-trips) and `FormatShort` (two units: `45s`, `1m05s`, `3d04h`).
4. Table tests from the cases rtok relied on (the shlex crate's split table, rtok's idle, since and render tests).
