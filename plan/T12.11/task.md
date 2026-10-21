---
status: in progress
priority: P1
complexity: 3
readiness: 100%
agent: Claude Code / claude-opus-5-5
---

# T12.11. Package Pyrlyn.CommandLine.Docs: command model, man pages and completions

ConsoleAppFramework generates the parser at compile time and keeps no command tree at runtime (README N9). Man pages, completion scripts and the flag-to-config-key test still need one, and must not be written by hand.

Done when:
- A tool runs a built binary's `--help` for the root and every command path it lists, and parses ConsoleAppFramework's help format into a command model: paths, descriptions, arguments, options with aliases, types and defaults. The model is saved as JSON (`docs/cli/commands.json` in rtok).
- From the model the package emits one roff page per command and bash, zsh, fish, PowerShell and clink completion scripts. rtok embeds them at build.
- A test regenerates the model from the current build and fails when the checked-in JSON differs.
- A help format the parser does not understand fails the tool, not the output.

Spec: catalog A31, catalog B21 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: no rtok task; new for .NET.

Check: For a sample ConsoleAppFramework app the model, roff pages and completion scripts match stored goldens byte for byte, and changing one option's doc comment makes the drift test fail.

Plan:
1. `eng/new-package.sh Pyrlyn.CommandLine.Docs`.
2. A strict parser for one ConsoleAppFramework 5.7 help page (usage path, description, Arguments, Options with aliases, types, `[Default: …]` and `[Required]`, Commands with aliases and continuation lines); any line it does not recognise throws `HelpFormatException` with the line.
3. `CommandModelReader.Read(name, runHelp)`: `--help` for the root, then for every path the root lists, checking each page's usage names the path it was asked for. The caller supplies `runHelp` (rtok runs it through ProgressRunner; the package starts no process). The model serialises to stable JSON (STJ source generation) with a drift helper that names the first differing line.
4. Emitters over the model: one roff page per command (`<name>-<path>.1`), and bash, zsh, fish, PowerShell and clink scripts that complete command paths, options and option values.
5. Tests: a sample ConsoleAppFramework app inside the test assembly (started as a child process); the model, man pages and scripts match goldens byte for byte; a variant of the sample with one option doc comment changed makes the drift check fail; malformed help fails the reader; shells on PATH accept the scripts' syntax and bash completes as expected.
