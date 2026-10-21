---
status: todo
priority: P1
complexity: 3
readiness: 0%
agent:
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
