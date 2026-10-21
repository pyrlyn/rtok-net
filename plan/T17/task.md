---
status: todo
priority: P1
complexity: 4
readiness: 0%
agent:
---

# T17. cmd plugin: `rtok run`, rule engine, Bash rewrite, `filter`

Shell output is the largest token sink; `rtok run` shortens it and archives the original.

Done when:
- `rtok run [--agent <id>] -- <cmd>`:
  - runs `sh -c`, merges the streams and keeps the exit code (128+signal on a signal death);
  - caps capture at 32 MiB and still drains the pipes;
  - archives the raw output and prints the trailer and marker under rtok's rules.
- The TOML rule engine is ported, reading `rules/default.toml` copied from rtok. Its passes are dedupe, collapse_columns, dir and diag grouping, keep patterns, and the fail tail. Rule sources merge in rtok's order.
- Bounded commands pass through unchanged.
- The PreToolUse(Bash) rewrite follows every never-wrap rule: sudo, heredocs, `&`, interactive stems, sub-agent git, cd hops.
- `rtok filter --stdin [--cmd]` and `rtok discover` behave as in the catalog.

Tests:
- `Rtok.Plugins.Cmd.Tests` integration: `rtok run -- 'echo out; echo err >&2; exit 3'` runs under `sh -c`, merges both streams in order and exits 3.
- `Rtok.Plugins.Cmd.Tests` integration: a child killed by SIGTERM makes `rtok run` exit 143 (128+signal), skipped on Windows.
- `Rtok.Plugins.Cmd.Tests` integration: a child writing 40 MiB is captured up to 32 MiB, its pipes are still drained and it exits without blocking.
- `Rtok.Plugins.Cmd.Tests` integration: shortened output prints the `[rtok <id> · N lines]` trailer and the `… N lines omitted (expand <id>)` marker, and `rtok expand <id>` returns the raw output byte for byte.
- `Rtok.Plugins.Cmd.Tests` unit: output that differs from raw only in whitespace runs or ANSI CSI/OSC, or whose body is no larger than the trailer, prints raw with no trailer and writes a zero-saving Measurement.
- `Rtok.Plugins.Cmd.Tests` unit: with a failing store `rtok run` writes the raw bytes plus a trailing newline, and non-UTF-8 output is decoded lossily without cutting a multibyte character.
- `Rtok.Plugins.Cmd.Tests` Verify: each rule pass on a fixture: dedupe `line (×N)`, collapse_columns, `group = "dir"`, `group = "diag"`, the built-in keep patterns and an 80-line fail tail, with a stack trace never cut and every dropped line counted in the trailer.
- `Rtok.Plugins.Cmd.Tests` unit: rule sources merge as `rules/default.toml` < `plugins.cmd.rules` < sorted `rules.d/*.toml`, and the rule stem is taken after `cd … &&` hops, env prefixes and pipe stages, never from argument words such as `git commit -m "fix grep"`.
- `Rtok.Plugins.Cmd.Tests` unit: a table of bounded commands (`head -n 5`, `tail -n 5`, `sed -n 1,9p`, `rg -m 3`, `cat -n one.txt`) passes through unchanged, while `tail -f`, `tail +5` and a recursive grep without `-m` are filtered.
- `Rtok.Plugins.Cmd.Tests` unit: the rewrite table leaves every never-wrap case unwrapped: `rtok`, `sudo`/`SUDO`, `<<` heredocs, a background `&`, `-i`/`--interactive`, each interactive stem, a sub-agent command naming `git`, a non-leading cd/pushd/export/source/alias stage, and `plugins.cmd.rewrite=false`.
- `Rtok.Plugins.Cmd.Tests` unit: leading `cd x &&` hops stay outside the wrap, `mise exec` and `just -c` are peeled, and an existing `rtok run --agent <id> -- ` prefix is stripped and never doubled.
- `Rtok.Plugins.Cmd.Tests` CsCheck: for any command string the rewrite returns it unchanged or as `rtok run -- ` plus the original, and never throws.
- `Rtok.Plugins.Cmd.Tests` Verify: the rule goldens from rtok's `cmd_golden` fixtures pass and each meets its `min_saving` floor.
- `Rtok.Cli.Tests` integration: `rtok filter --stdin --cmd "git status"` filters piped text without executing anything, and `rtok discover --json` ranks unfiltered command families by estimated tokens.
- `Rtok.Fuzz` fuzz: the `cmd-filter` target is wired and feeds arbitrary bytes through the rule engine.

Spec: catalog A4, catalog B12 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T3.1, T3.2, T3.4, T3.6, T34.8, T36.3, T36.4, T50.1, T50.2, T58.5, T59.1, T64.1, T64.2, T65.2, T65.3, T65.4, T160, T175, T176, T177, T233, T235.1, T235.2, T235.3, T238, T240, T247, T267, T268, T296, T297, T321, T325, T360, T366, T386, T437, T444, T447, T448, T457, T536, T662, T688.

Fuzz: `cmd-filter` and `bash-rewrite` in `tests/Rtok.Fuzz`: each rule over any output is never longer than its input and archives the input unchanged; a Bash string either keeps every never-wrap rule or comes back unchanged.

Check: Golden tests from rtok's cmd fixtures pass with their `min_saving` floors, and the rewrite table test covers every never-wrap case.
