# T12.11 done

New package `packages/Pyrlyn.CommandLine.Docs` (0.1.0):

- `CommandModelReader.Read(name, runHelp)` reads a ConsoleAppFramework 5.7 program from its `--help`: the root page, then `<path> --help` for every command the root lists. The caller supplies `runHelp` (rtok runs the binary through ProgressRunner); the package starts no process. The parser is strict: a line outside the format, sections out of order or repeated, or a page that is not the one asked for (an unknown path prints the root help) throws `HelpFormatException` naming the line.
- The model (`CommandModel`, `CommandInfo`, `ArgumentInfo`, `OptionInfo`): paths, aliases, descriptions, arguments by type, options with every spelling, type, default (a verbatim `@"..."` string unquoted, `null` as none) and `[Required]`. ConsoleAppFramework prints doc comments raw, so `<c>`, `<see cref>`, `<paramref>` and entities become plain text. Aliases are whole paths of their own, as ConsoleAppFramework runs them (`config set|put` runs as `sample put`, not `sample config put`).
- `CommandModelJson`: indented JSON with `\n` line ends through a source-generated context (AOT-safe), `Deserialize`, and `Diff(expectedJson, model)`, which names the first differing line or returns null.
- `ManPages.Render`: one section-1 roff page per command (`sample.1`, `sample-config-get.1`), with NAME, SYNOPSIS (alias lines included), DESCRIPTION, ARGUMENTS, OPTIONS (built-in `--help` and `--version` included) and COMMANDS. No date or version, so the output is byte-stable.
- `CompletionScripts.Render`: `<name>.bash` (bash 3.2 and later), `_<name>` (zsh), `<name>.fish`, `<name>.ps1` and `<name>.lua` (clink). Each walks the typed words to the command path, skipping the value after an option that takes one, and offers the next words or the options, with the first description line as the hint. Option values and arguments fall back to file names.

No new dependency: the test project uses ConsoleAppFramework, already in central packages.

Commit: `T12.11: Package Pyrlyn.CommandLine.Docs: command model, man pages and completions`.

Checked: 44 tests. The test assembly is also the sample ConsoleAppFramework app, started as a child process. The sample has a root handler, a group with its own handler, nested commands, arguments (optional and `string[]`), command and option aliases, global options, defaults, a required option, an enum and a hidden command.
- The model, the seven man pages and the five scripts match goldens in `tests/goldens` byte for byte. `PYRLYN_UPDATE_GOLDENS=1` rewrites them.
- The edited sample makes `Diff` fail. ConsoleAppFramework's generator allows one `stats` command per assembly (CAF007), so the edited variant is the same binary with that option's description changed in its printed `stats --help`, which is what a rebuild with the edited doc comment prints.
- Malformed pages fail the reader, and so does a program that answers every path with the root help.
- The scripts are checked in real shells, each skipped when absent:
  - Syntax: `bash -n`, `zsh -n`, `fish --no-execute`, the PowerShell parser and `luac -p`.
  - Behaviour, in bash, fish (`complete -C`) and zsh (with `_describe` and `_files` stubbed): paths, aliases, option prefixes, global options before the path, and no word offered after a value option.
- pwsh and lua here are mise shims without a default version. They ran with `mise exec lua@5.4.9 powershell@7.6.6 -- dotnet test` (44 of 44). A plain run skips those two tests. The PowerShell completer was also tried with `TabExpansion2`.
- `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test` and `dotnet csharpier check .` are green.

Left to rtok: `docs/cli/commands.json`, its drift test against the real binary, and embedding the pages and scripts at build all need rtok's commands. They belong to the tasks that add those commands, which call this package.
