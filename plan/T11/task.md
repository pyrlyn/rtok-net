---
status: todo
priority: P1
complexity: 4
readiness: 0%
agent:
---

# T11. CLI shell and UX: ConsoleAppFramework, Spectre.Console, ProgressRunner

All commands share one style and one way to run child processes (N9, N10).

Done when:
- Every command is a ConsoleAppFramework method with XML doc comments, with the aliases from rtok.
- Bad arguments exit 2: a parse error (through `ConsoleApp.LogError`) and an unknown command, which ConsoleAppFramework answers with the root help and exit 0. An exception inside a command is not a parse error and keeps its own exit code.
- `rtok run -- <cmd>` takes its command from the raw arguments after `--`.
- `--json` exists on every reading command.
- Output style comes from Spectre.Console:
  - line kinds success, status, warning and error, with emoji and colour switches;
  - NO_COLOR, CLICOLOR, CLICOLOR_FORCE, FORCE_COLOR and TERM=dumb are honoured per stream;
  - nothing styled on hook stdout, MCP, the proxy, run/filter output, `--json` or pipes.
- Operation icons come from Pyrlyn.OperationIcons (T12.8).
- ProgressRunner (on CliWrap) is the only process API:
  - modes Capture, Stream, Inherit and Detached;
  - timeouts;
  - a TTY-only spinner and bars, with a batch bar for `agents install/update` (rtok T436.4).
- `rtok completions bash|zsh|fish|powershell|clink [--install|--uninstall]` and `rtok man [--dir]` print the scripts and pages generated from the command model (T12.11), embedded at build.
- No user-visible output contains plan ids.

Spec: catalog A31, catalog B21 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T20.2, T53.2, T60.1, T60.2, T83.7, T83.16, T107, T276, T276.1, T276.2, T276.3, T276.4, T276.5, T276.6, T315, T316, T317, T318, T319, T345, T348, T408, T409, T436, T436.2, T651, T903; open T436.4 ([open work](../../docs/port/open-work.md)).

Check: Golden tests pass for every subcommand's `--help` and for each shell's completion script, and the banned-API analyzer reports no `Process.Start` outside ProgressRunner, and the exit-code tests show 2 for a parse error and an unknown command.
