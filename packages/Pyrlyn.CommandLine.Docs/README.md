# Pyrlyn.CommandLine.Docs

A command model read from ConsoleAppFramework --help output, saved as JSON, with roff man pages and static bash, zsh, fish, PowerShell and clink completions generated from it.

## Install

```bash
dotnet add package Pyrlyn.CommandLine.Docs
```

## Use

```csharp
using Pyrlyn.CommandLine.Docs;

// Run the built program for each page; the package starts no processes itself.
var model = CommandModelReader.Read("rtok", args => RunAndCaptureStdout("rtok", args));

File.WriteAllText("docs/cli.json", CommandModelJson.Serialize(model));
foreach (var file in ManPages.Render(model).Concat(CompletionScripts.Render(model)))
{
    File.WriteAllText(Path.Combine("dist", file.Name), file.Content);
}

// In a test: fail when the program's help no longer matches the committed model.
var drift = CommandModelJson.Diff(File.ReadAllText("docs/cli.json"), model);
Assert.Null(drift); // "command model drift at line 42:\n- ...\n+ ..."
```

- ConsoleAppFramework generates its parser at compile time and keeps no command tree at
  run time, so `--help` is the only description of the program. The reader asks for the root
  page, then `<path> --help` for every command the root lists. It is strict: a line outside
  the format, sections out of order, or a page that is not the one asked for (an unknown path
  prints the root help) throws `HelpFormatException` with the line number.
- The model keeps what help shows: paths, aliases (whole paths of their own, as
  ConsoleAppFramework treats them: `config set|put` runs as `put`), descriptions, arguments by
  type, options with every spelling, type, default and `[Required]`. Doc-comment markup
  (`<c>`, `<see cref>`, `&amp;`) becomes plain text. Hidden commands and options are not in
  help, so not in the model; enum values are not printed, so not modelled.
- `CommandModelJson` writes indented JSON with `\n` line ends through a source-generated
  context (AOT-safe); `Diff` names the first differing line.
- `ManPages.Render` writes one section-1 roff page per command: `rtok.1`,
  `rtok-config-get.1`. No date or version, so the pages are byte-stable.
- `CompletionScripts.Render` writes `rtok.bash`, `_rtok` (zsh), `rtok.fish`, `rtok.ps1` and
  `rtok.lua` (clink). Each walks the typed words to the command path, skipping the value after
  an option that takes one, then offers the next words or the options with their first
  description line as the hint. Option values and arguments fall back to file names. The
  bash script runs on bash 3.2.
