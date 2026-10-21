# T11 done

**ProgressRunner** (`src/Rtok.Process`). This is the only project with `RtokProcessRunner`, so it is the only one where `Process.Start` compiles.
- It runs on CliWrap and has four modes:
  - Capture keeps both streams.
  - Stream forwards them live, with the indicator hidden around each write.
  - Inherit gives the child the terminal.
  - Detached is spawn-only. On Unix it goes through `sh -c 'exec …'` with null stdio; on Windows the pipes are drained.
- Options:
  - cwd, and env (a null value removes the variable);
  - stdin, which is closed unless given;
  - raw arguments;
  - the Windows `cmd /D /C` shim, used when `NeedsCmdHost` says so;
  - a timeout that kills the process tree and throws `TimeoutException`;
  - a byte cap shared by stdout and stderr, which keeps the first bytes and counts the rest as `Dropped`.
- A killed child exits `128 + signal`.
- A descendant that holds the pipe does not stall a run. The runner watches the child's pid and drains for 200 ms after it exits.
- Progress parsers: percent, git, cargo (with its env), JSON lines (`{"progress":{...}}`) and a callback. They read both streams, lines split on `\n` and `\r`, each line capped at 8 KiB.
- `Batch` counts finished items for a batch bar. `Wait` keeps a spinner up around in-process work.
- Indicators draw nothing until a surface installs a display (`ProgressDisplay.Install`). `RTOK_NO_PROGRESS=1` and `ProgressDisplay.Disable()` turn them off, so hooks and MCP never draw.

**Output style** (`src/Rtok.Cli/Ui`), on Spectre.Console.
- `Lines` prints four kinds of line:
  - success: stdout, green, ✅
  - status: stdout, cyan, 💡
  - warning: stderr, yellow, ❗
  - error: stderr, red, ❌
- A line that names an operation takes its icon from Pyrlyn.OperationIcons, then from rtok's own verbs (index, worktree, compress, expand, bench, start, stop). Icons sit in the shared two-column gutter.
- An icon needs `[ui] emoji` and a terminal on that stream. Colour needs `[ui] color` and `ColorRule`, applied per stream:
  1. `FORCE_COLOR` decides first (`0`/`false` off, anything else on).
  2. Then `CLICOLOR_FORCE` turns colour on.
  3. Then `NO_COLOR`, `TERM=dumb` or `CLICOLOR=0` turn it off.
  4. Otherwise a terminal turns it on.
- The config is loaded on the first styled line only.
- `TerminalDisplay` is ProgressRunner's display on a terminal stderr. It draws one line per live indicator, so a batch bar keeps its command's spinner under it, and a spinner that learns a total becomes a bar.
- Lines are cut to the terminal width, and the block is erased around every write. Prompts use the same `ProgressDisplay.Suspend`. `rtok` installs the display at start when stderr is a terminal.

**Exit codes.**
- `ExitCodeFilter` handles every command:
  - a parse or validation error prints `Error: …` and `Run 'rtok <command> --help' for usage.`, then exits 2;
  - an exception prints one `Error: …` line and exits 1;
  - otherwise the command's own return value is kept.
- `CommandPath` runs before ConsoleAppFramework. It checks the arguments against the embedded command model and exits 2 on an unknown command or an option where only a command can follow.
- A bare group and any `--help` still print help and exit 0.
- `rtok hook` skips both the model check and the display, so the hook path stays on its budget.

**Command model.**
- `docs/cli/commands.json` is read from the binary's own `--help` and embedded in `rtok`.
- Two tests guard it: one checks that the file matches the binary, and one that the embedded copy matches the file. `RTOK_UPDATE_GOLDENS=1` rewrites the file.
- `rtok completions <shell>` prints the bash, zsh, fish, powershell or clink script.
- `--install` and `--uninstall` write or remove the script in the shell's per-user place, ported from rtok:
  - XDG dirs and `ZDOTDIR` are honoured, and zsh prints a note on how it loads the script;
  - the PowerShell `$PROFILE` gets one dot-source line, and the rest of the profile stays byte for byte;
  - with no shell named, the shell comes from `$SHELL`.
- `rtok man` prints `rtok.1`; `--dir` writes every page and prints the paths.
- `plugins` is now a documented method.
- `--list`, `--dry-run` and the interactive picker of `rtok completions` are not in this task's criteria and are left out. `--dry-run` needs Pyrlyn.ChangePreview.

`rtok run -- <cmd>` belongs to the cmd plugin: ConsoleAppFramework gives it the raw arguments after `--`. This task adds no `run` command.

No new package versions. Rtok.Cli now references Spectre.Console (already central, N10), Rtok.Process, Pyrlyn.OperationIcons and Pyrlyn.CommandLine.Docs. The test projects use CliWrap and Verify, both already central.

Commit: `T11: CLI shell and UX: ConsoleAppFramework, Spectre.Console, ProgressRunner`.

**Checked.**

`tests/Rtok.Process.Tests` (56 tests), ported from rtok-sys:
- capture, missing program, stdin, cwd and env;
- timeouts in Capture and Inherit;
- a descendant holding the pipe;
- the cap;
- 143 and 137 from `kill -TERM` and `kill -KILL`;
- the shim off Windows, `NeedsCmdHost`, and raw arguments;
- each parser, line splitting and the line cap;
- `Batch`, `Wait`, and the display switch.

`tests/Rtok.Cli.Tests` (102 tests):
- Verify goldens for every command's `--help`, each shell's script and the root man page;
- `man --dir`;
- the model drift test;
- exit codes:
  - 2 for parse errors, unknown commands and stray options;
  - 0 for help, a bare group and `--version`;
  - 1 for a failing command;
  - a command's own exit code is kept;
- `--json`: every command is either reading, in which case it has `--json` and prints valid JSON, or listed with the reason it has none;
- only `src/Rtok.Process` sets `RtokProcessRunner` outside test projects, and the banned list covers `Process.Start`;
- the style lines and the colour rule;
- the terminal display, as escape sequences;
- the completion installer.

The flag-to-key coverage test lists `install`, `uninstall` and `dir` as action flags. `Rtok.Process` sits in the core layer of the architecture test and of the solution layout.

`dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test` and `dotnet csharpier check .` are green.
