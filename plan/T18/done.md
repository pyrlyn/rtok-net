# T18. cmd formatters for command families — done

What was done:

- Ported rtok's family formatters into `src/Rtok.Plugins.Cmd`:
  - `FamilyFormatters.Format`: one arm per (bin, sub), as in rtok's `format()`, plus `git log` and the `Keep` fallbacks.
  - `Compact`: cargo build/test, git status/diff, `ls -l`.
  - `Diffs`: the two tiers of git diff and git show.
  - `Grep`: grep, rg and git grep.
  - `Gh`: pr/issue view, pr checks and run logs.
  - `Python`: pytest, unittest and tracebacks.
  - `Tables`: docker/podman ps, kubectl get, ps aux, df, sqlite3, wc.
  - `Loops` (loop collapse) and `Mise` (install chatter), run as the `Tidy` pre-passes.
- `find`, the lint tools and `awk` stay on their TOML rules, as in rtok. Their goldens pass.
- The JSON view (T17) strips nulls from gh `--json`, aws and `kubectl -o json` before any table formatter runs.
- The cat outline (rtok `catfile.rs`, T674) needs the tags outline (T24) and the re-read dedup (T19). It moved to T18.1, right after T18 in lane L1.

Acceptance rules, enforced in one place (`FamilyFormatters.Format`). An arm whose text breaks a rule declines, and the rule runs instead:

- The text is never longer than the raw output.
- Every line with `ERROR` or `WARN` is kept.
- After a non-zero exit, every stderr verdict line is kept: `error:`, `error[`, `fatal:`, `Error:`. This is how "stderr and the exit line" is read; the exit code itself is the trailer's job (T17.1).

Deviations from rtok:

- `CargoTest` keeps cargo's closing `error: test failed, to rerun pass …` lines after the totals. rtok dropped them.
  - The `cargo_test_panic` golden gains that line.
  - Its floor drops from 70 to 65 (the saving is now 69 %).
- New goldens `rg_group` and `git_grep` give the `rg` and `git grep` arms coverage.
- `git log` lists commits up to the 100-commit cap. Past the cap, and for shapes it does not parse, it cuts the head, as rtok does. The archived original comes back with `expand`.

Tests (`Rtok.Plugins.Cmd.Tests`):

- Every golden passes and clears its `min_saving` floor. `Every_formatter_arm_has_a_golden` fails when an arm in `FamilyFormatters.Arms` has no golden it formats.
- rtok's unit tests are ported in `CompactTests`, `DiffsTests`, `GrepTests`, `GhTests`, `PythonTests`, `LoopsTests`, `MiseTests` and `FormatterTests`. They cover `shorten_ago`, podman ps, the kubectl header offsets, ps aux, git log and the table stand-downs.
- `AcceptanceTests`, with CsCheck properties over every arm and its mutated goldens:
  - nothing gets longer, and planted WARN/ERROR lines are kept;
  - every commit hash (git log) is kept;
  - every path (git status, grep, git diff) is kept.
- `AcceptanceTests` unit tests:
  - a failed run keeps its stderr verdict;
  - a declining arm is measured as `rule` or `unmatched`, a formatting one as `formatter`;
  - JSON compaction leaves no null, and every line reads back under the relaxed-JSON grammar with valid string literals.
- Verify snapshots:
  - the git log `[N commits, +A/-D]` header;
  - git status XY codes with diverged ahead/behind;
  - the diff per-file table over 120 lines;
  - grep, rg and git grep sharing one shape.
- Fuzz target `cmd-formatters` (`tests/Rtok.Fuzz/Targets/CmdFormatters.cs`), with a 10-seed corpus taken from the goldens.
  - Any bytes go through the arm the first byte picks; the top bit marks a failed run.
  - It checks: nothing gets longer, WARN/ERROR lines and verdicts are kept, and the git log listing keeps every hash.

Checked: `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test`, `dotnet csharpier check .` and `npx --yes markdownlint-cli2@0.23.3`, all green.
