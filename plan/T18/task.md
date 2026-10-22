---
status: in progress
priority: P1
complexity: 4
readiness: 100%
agent: Claude Code / claude-opus-5-5
---

# T18. cmd formatters for command families

Family formatters give the deepest cuts and are where rtok beats its competitors.

Done when:
- Every formatter in the catalog is ported, among them git log/status/diff, grep/rg, ls, find, cargo, pytest, jest, vitest, go test, docker/podman ps, kubectl, gh, the lint tools, df, sqlite3, awk, and loop collapse. The `cat` outline moved to T18.1, which waits on the tags outline (T24) and the re-read dedup (T19).
- Each keeps rtok's acceptance rules:
  - 100 % of hashes, paths and WARN/ERROR lines are kept;
  - the output is never longer than the raw text;
  - on failure, stderr and the exit line are kept.

Tests:
- `Rtok.Plugins.Cmd.Tests` Verify: every formatter arm has a golden from rtok's `cmd_golden` fixtures (`argv`, `exit`, `min_saving` header) and meets its `min_saving` floor.
- `Rtok.Plugins.Cmd.Tests` unit: a coverage test fails when a registered formatter arm has no golden fixture.
- `Rtok.Plugins.Cmd.Tests` CsCheck: for any input each formatter's output is never longer than the raw text.
- `Rtok.Plugins.Cmd.Tests` CsCheck: every commit hash, path and WARN/ERROR line in a generated input appears in each formatter's output.
- `Rtok.Plugins.Cmd.Tests` unit: on a non-zero exit every formatter keeps the stderr lines and the exit line.
- `Rtok.Plugins.Cmd.Tests` unit: a formatter that declines falls back to the rule, and the Measurement kind is `formatter` or `rule` accordingly.
- `Rtok.Plugins.Cmd.Tests` Verify: git log prints the `[N commits, +A/-D]` header, git status prints one line per XY code with ahead/behind in the branch line, and a diff over 120 lines prints the per-file table with 100 % of paths.
- `Rtok.Plugins.Cmd.Tests` Verify: grep, rg and git grep print each path once with ±60 chars around a match, 12 matches per file and a 16-line budget, then `path:l1,l2` anchors.
- `Rtok.Plugins.Cmd.Tests` unit: loop collapse prints `shape ×N (vals: a..b)` for for/while/until output and never collapses a line with an error word.
- `Rtok.Plugins.Cmd.Tests` unit: `shorten_ago` shortens only number plus unit, `podman ps` takes the docker ps formatter, and kubectl columns split by header offsets.
- `Rtok.Plugins.Cmd.Tests` unit: JSON compaction strips nulls from gh `--json`, aws and `kubectl -o json` output and the result still parses.

Spec: catalog A4, catalog B12 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T3.3, T673, T674, T675, T676, T677, T678, T679, T680, T681, T682, T683, T684, T685, T905.

Fuzz: `cmd-formatters` in `tests/Rtok.Fuzz`: every formatter over any bytes keeps the acceptance rules: never longer than the input, every hash, path and WARN/ERROR line kept.

Check: Every formatter arm has a golden fixture with a `min_saving` floor, and all goldens pass.

Plan:
1. Port rtok's `formatters::format` arms and helpers (git log/status/diff, wc, docker/podman ps, kubectl, ps aux, df, sqlite3) into `src/Rtok.Plugins.Cmd`, plus `compact.rs`, `diffs.rs`, `grep.rs`, `gh.rs`, `python.rs`, `loops.rs` and `mise.rs`.
2. Fill `FamilyFormatters.Format` and `Tidy` with them; the route in `Families` is already in place.
3. Unskip the `PendingT18` goldens and tests, add the coverage test, the CsCheck acceptance properties and the unit tests listed above.
4. Wire the `cmd-formatters` fuzz target with a corpus.
5. Gates, `done.md`, one commit, rebase, ff-merge, remove the worktree.
