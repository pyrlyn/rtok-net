# T12.6 done

Two new packages, no new dependencies:

- `packages/Pyrlyn.Shlex` (0.1.0): `ShellWords.Split`/`TrySplit` split POSIX words without expansion (blanks and newlines separate; single quotes literal; double quotes escape only `$`, `` ` ``, `"`, `\` and newline; backslash-newline continues; `#` at a word start comments to the end of the line). An unterminated quote or a trailing backslash fails with its offset (`unterminated single quote at 5`), never a guess. `Quote` and `Join` write words that split back unchanged.
- `packages/Pyrlyn.Durations` (0.1.0): `HumanDuration.Parse`/`TryParse` read the humantime grammar (`0s`, `30m`, `24h`, `7d`, `1h 30m`, `ns` to `y`); `ParseDaysOrHours` reads the `--since` grammar (`<n>` days, `<n>d`, `<n>h`). Both reject `5x`, `-1d` and empty input and report overflow. `Format` writes exactly and parses back (`24h` → `1d`); `FormatShort` is rtok's two-unit column (`45s`, `1m05s`, `1d01h`).

Commit: `T12.6: Packages Pyrlyn.Shlex and Pyrlyn.Durations`.

Checked: table tests from the cases rtok relied on: the shlex crate's split table plus guard and doctor command lines (38 tests); rtok's `[agents] idle`/`--idle`/`--stale-lock`, `parse_since` and session-duration cases (47 tests). `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test` (114) and `dotnet csharpier check .` green.
