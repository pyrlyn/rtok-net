# T17. Done

`src/Rtok.Plugins.Cmd` ports rtok's `src/plugins/cmd` and is linked as a built-in plugin in `eng/RtokPlugins.targets` (which also defines `RTOK_PLUGIN_CMD` for the CLI):

- The rule engine reads `rules/default.toml` copied from rtok. User rules (`plugins.cmd.rules`) and sorted `rules.d/*.toml` merge on top in rtok's order. Its passes are drop/keep, collapse_columns, dedupe (adjacent, normalized), JSON compaction, dir and diag grouping, logfmt factoring, the 80-line fail tail, the head/tail cut with omitted markers, and trace blocks that are never cut.
- `Bounded` is the quote-aware lexer. Bounded commands pass through unchanged.
- The family dispatch and formatters that T17 needs: catfile, read/grep/find/ls rows, mise and loop peeling. The family formatter arms (git, cargo, gh, docker, go, …) are T18; their 47 goldens are skipped with `PendingT18`.
- `Emitter` holds the `run` output contract (rtok T536):
  - the trailer `[rtok <id> · N lines]` and markers;
  - the never-worse-than-raw guard for valid UTF-8; invalid UTF-8 prints lossily with a trailer while the archive keeps the exact bytes;
  - a zero-saving Measurement when nothing was cut;
  - raw bytes plus a newline when the store fails.
- `Capture` runs the shell body through `Rtok.Process`'s `ProgressRunner`. It merges stdout and stderr, caps the capture at 32 MiB and still drains the pipes (`[rtok: output capped at 32 MiB; N bytes not captured]`), and keeps the exit code (128+signal on Unix).
- `BashRewrite` is the PreToolUse(Bash) rewrite, with every never-wrap rule. An existing `rtok run --agent <id> -- ` prefix is stripped by the guard (B13), never doubled.
- `StdinFilter` serves `rtok filter --stdin [--cmd]`. `CmdCommands` is the facade the CLI calls.

CLI (`src/Rtok.Cli/RunCommands.cs`):
- `rtok run [--agent] [--no-ref] [--config] [--] <command…>`.
- `rtok filter [--stdin] [--cmd <hint>] [--archive] [--no-ref] [--config]`; `--exit` is hidden and used by `--archive`.
- Both stay registered without the cmd plugin: `run` errors with exit 1, `filter` passes stdin through.
- The command model (`docs/cli/commands.json`), help pages, completions and man page were regenerated.

The real `FileSystem` for loading rules from disk comes from `TestableIO.System.IO.Abstractions.Wrappers`, which T35 already added to `Directory.Packages.props`.

Until T9 lands the CLI fails open: it passes no store host, so `rtok run` prints the command's output raw and `rtok filter` only compresses (no archive, no Measurement, no saving claim).

Moved to T17.1 (depends on T8 and T9), with its criteria taken out of T17's task.md:
- the CLI's archive and Measurement host for `run` and `filter`;
- `rtok expand <id>` byte for byte after a shortened `rtok run`;
- `rtok discover`, with a `hook_inputs_since` store query and a port of `measure::discover::rank`.

The `git status` stdin filter test waits on T18's git arm.

Deviations from rtok:
- `tail +5 log` (obsolete start-at-line form) is not bounded; rtok treated it as bounded.
- The rule family is picked after `cd …`/`export` hops (`cd x && cargo test` is cargo); rtok picked `cd`.
- A Python traceback block keeps its closing `ValueError: …` line; rtok left it to the budget, which could drop it.
- When the last line is popped for the trailer and that bares the previous marker, the two merge into one marker; rtok printed two markers in a row.
- `StdinFilter` without a host returns its input when the cut is not byte-shorter (it used to eat the final newline).
- Further changes:
  - `Ansi` moved to `Rtok.Core`.
  - `MemoryHost` gained `PutArchiveFor`, `ArchiveInSession` and `ArchiveFails`.
  - logfmt factoring is part of T17.
  - There is no TOON output.

Checked:
- `Rtok.Plugins.Cmd.Tests`:
  - the never-wrap rewrite table and a CsCheck property (wraps once or not at all, never throws);
  - bounded and unbounded tables;
  - dispatch after cd hops and env prefixes, and the rule-source merge order;
  - capture: the cap notice, 40 MiB capped at 32 MiB, labels;
  - the emitter: lossy non-UTF-8, the failing store, tiny output, whitespace-only cuts;
  - the rtok cmd goldens with their `min_saving` floors (T18's arms skipped);
  - Verify snapshots of each rule pass in `RulePassTests`: dedupe `(×N)`, collapse_columns, group dir, group diag, built-in keep, the 80-line fail tail, an uncut traceback, and dropped lines counted in the `· 60 lines` trailer.
- `Rtok.Cli.Tests`, `RunCommandTests`:
  - `rtok run -- 'echo out; echo err >&2; exit 3'` merges the streams in order and exits 3;
  - SIGTERM exits 143 (skipped on Windows);
  - no command exits 2;
  - `rtok filter --stdin --cmd` compresses and never runs the hint;
  - `--archive` without a store prints the input.
- `Rtok.Fuzz`: `cmd-filter` and `bash-rewrite` are wired with corpora. Their invariants: output is never longer than the input (the lossy text plus the trailer for invalid UTF-8), every archive equals the input, and a rewrite only ever wraps the original once without breaking a never-wrap rule.
- After rebasing on `main` (cdc6d12): Release `-warnaserror` build green, `dotnet test` 1985 passed and 71 skipped (47 goldens pending T18), CSharpier and markdownlint clean. An earlier run on 8f19db2 failed the hook-latency test `A_hung_resident_fails_within_the_budget` once under full parallel load (165 ms); it passed alone three times and in this run, and T17 does not touch hooks.
