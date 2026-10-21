---
status: in progress
priority: P1
complexity: 4
readiness: 100%
agent: Claude Code / claude-opus-5-5
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

Tests:
- `Rtok.Cli.Tests` Verify: the `--help` of every subcommand in the command model, with a description for every option, rtok's aliases (`-a|--agent`) and usage naming `rtok` (no `.exe`).
- `Rtok.Cli.Tests` integration: a parse error exits 2 with its message on stderr, an unknown command exits 2 after the root help, and an exception inside a command keeps its own exit code.
- `Rtok.Cli.Tests` integration: `rtok run -- <cmd> --json -x` hands the child every argument after `--` unchanged, rtok-looking flags included.
- `Rtok.Cli.Tests` integration: every reading command in the command model accepts `--json` and prints one JSON document with no ANSI escapes.
- `Rtok.Cli.Tests` Verify: success, status, warning and error lines (✅ green, 💡 cyan, ❗ yellow, ❌ red) with `[ui] emoji` and `[ui] color` each on and off.
- `Rtok.Cli.Tests` CsCheck: for any mix of NO_COLOR, CLICOLOR, CLICOLOR_FORCE, FORCE_COLOR, TERM=dumb and a redirected stdout or stderr, each stream's colour follows the precedence table.
- `Rtok.Cli.Tests` integration: hook stdout, `rtok mcp`, `rtok run` output, `--json` output and piped output carry no ANSI escape even with `CLICOLOR_FORCE=1`.
- `Rtok.Core.Tests` arch: `Rtok.Hook`, `Rtok.Hooks`, `Rtok.Mcp` and `Rtok.Proxy` do not reference Spectre.Console, and only ProgressRunner's project references CliWrap.
- `Rtok.Cli.Tests` unit: operation lines take their icon from Pyrlyn.OperationIcons in the 2-column gutter, and `uninstall` never gets the install icon.
- `Rtok.Core.Tests` integration: ProgressRunner runs a child in Capture, Stream, Inherit and Detached modes with cwd, env and stdin, and a timeout kills the child and reports it.
- `Rtok.Core.Tests` unit: the spinner and bars stay hidden on a non-TTY, with `RTOK_NO_PROGRESS=1`, in hooks and in MCP, and the batch bar steps once per host for `agents install/update`.
- `Rtok.Smoke` smoke: under Porta.Pty the published `rtok agents install` against fake host homes draws the batch bar on stderr, and the same run piped prints no progress.
- `Rtok.Cli.Tests` Verify: `rtok completions bash|zsh|fish|powershell|clink` and a `rtok man` page match the goldens generated from the command model.
- `Rtok.Cli.Tests` integration: `completions --install` then `--uninstall` in a temp HOME adds and removes only rtok's file, and `rtok man --dir <tmp>` writes one `rtok-<sub>.1` per subcommand.
- `Rtok.Cli.Tests` unit: no `--help` page, man page or completion script contains a plan id (`T` followed by digits).

Spec: catalog A31, catalog B21 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T20.2, T53.2, T60.1, T60.2, T83.7, T83.16, T107, T276, T276.1, T276.2, T276.3, T276.4, T276.5, T276.6, T315, T316, T317, T318, T319, T345, T348, T408, T409, T436, T436.2, T651, T903; open T436.4 ([open work](../../docs/port/open-work.md)).

Fuzz: `cli-argv` in `tests/Rtok.Fuzz`: NUL-split argv through the generated command tree with no-op command bodies; bad input exits 2, nothing else escapes.

Check: Golden tests pass for every subcommand's `--help` and for each shell's completion script, and the banned-API analyzer reports no `Process.Start` outside ProgressRunner, and the exit-code tests show 2 for a parse error and an unknown command.

Plan:
1. `src/Rtok.Process` (the one project with `RtokProcessRunner`): ProgressRunner on CliWrap with modes Capture, Stream, Inherit and Detached, cwd, env, stdin, timeouts (the child is killed), a byte cap, `128 + signal` exit codes, the Windows `cmd /D /C` shim, the progress parsers (percent, git, cargo, JSON lines, callback), `Batch` and `Wait`. Indicators are hidden unless a surface installs a terminal display, so hooks and MCP never draw. Tests in `tests/Rtok.Process.Tests`.
2. `Rtok.Cli` output style on Spectre.Console: success, status, warning and error lines with icons from Pyrlyn.OperationIcons plus rtok's own, `[ui] emoji` and `[ui] color`, and the colour rule per stream (NO_COLOR, CLICOLOR, CLICOLOR_FORCE, FORCE_COLOR, TERM=dumb, a terminal). The terminal display for ProgressRunner (spinners, bars, the batch bar, suspended around prompts) lives here too.
3. Exit codes: a ConsoleAppFramework filter turns parse and validation errors into exit 2; a command's own exception prints one error line and exits 1. An unknown command or option before the command path exits 2, checked against the embedded command model.
4. `docs/cli/commands.json`: the command model read from the built binary, embedded in `rtok`, with a drift test. `rtok completions bash|zsh|fish|powershell|clink [--install|--uninstall]` and `rtok man [--dir]` render from it (Pyrlyn.CommandLine.Docs). `plugins` becomes a documented method.
5. Tests in `tests/Rtok.Cli.Tests`: a golden for every command's `--help` and for each shell's script, exit codes 2 for a parse error and an unknown command, `--json` on every reading command, and only `Rtok.Process` allowed to start processes.

`rtok run -- <cmd>` belongs to the cmd plugin (T17): ConsoleAppFramework hands it the raw arguments after `--` (`ConsoleAppContext.EscapedArguments`, N9); this task adds no `run` command of its own.

