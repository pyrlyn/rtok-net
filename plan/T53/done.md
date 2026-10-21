# T53. Done

What was done:
- `tests/Rtok.Fuzz` now has 13 wired SharpFuzz targets in `Targets/`, one per parser of outside input on `main`:
  - `jsonc-edit`, `config-toml`, `toml-edit`, `shell-words`, `ignore`, `durations`, `help-page`, `mcp-config`, `host-json`;
  - `change-preview` (T12.2), `hook-frame` (T14), `trash-info` (T12.7) and `cli-argv` (T11), whose tasks closed while T53 was open.
  - `trash-info` and `cli-argv` reach internals, so `Pyrlyn.Trash` and `Rtok.Cli` grant `InternalsVisibleTo` to `rtok-fuzz`.
- Each target catches only the failure type its code documents and checks that code's own invariants through `FuzzInput.Check`: round trips, unchanged bytes, re-parse, idempotent edits, and a diff that turns the old text into the new one.
- `FuzzTargets` lists every target with its task; 33 later targets are stubs named by their task.
- `rtok-fuzz <target> <file>...` replays inputs without libFuzzer.
- Seed corpora live in `tests/Rtok.Fuzz/corpus/<target>/`: the packages' test cases, real `--help` pages, the hook fixtures, and every crash found here.
- `tests/Rtok.Fuzz.Tests` is in `rtok.slnx`. On each `dotnet test` it runs every wired target over:
  - its corpus;
  - 1000 random byte arrays;
  - 2000 mutated seeds (flip, insert, delete, overwrite, truncate).

  It fails when a closed task leaves its target unwired, when a wired target has no corpus, or when a `Fuzz:` line names a target `FuzzTargets` lacks. A failure prints the input as base64.
- `eng/fuzz.sh <target> [libFuzzer flags]`:
  - builds libfuzzer-dotnet from its pinned source (v2025.05.02.0904);
  - publishes `rtok-fuzz` and instruments the Pyrlyn, Rtok and Tomlyn assemblies with `sharpfuzz` (the `sharpfuzz.commandline` 2.3.0 local tool);
  - runs libFuzzer.

  `tests/README.md` says how.
- The plan:
  - 38 open tasks name their target in a `Fuzz:` line; T35.1 and T35.2 join T35's `agents-install`.
  - T49's line asks `docs/security.md` to name the target behind each surface.
  - `plan/README.md` records the rule after N11, and T53 sits in lane L1.
  - T7 is in progress under another agent and was left untouched.

Bugs the targets found, fixed with a regression test each (the packages are unreleased, so 0.1.0 stays):
- `Pyrlyn.Durations`: `Format` wrote one tick as `1ns`, so `Parse(Format(x))` lost 99 ns. It now writes `100ns`.
- `Pyrlyn.Jsonc`:
  - A key with invalid UTF-8 or a lone surrogate escape threw `InvalidOperationException`; it is now a `JsoncException` with its line.
  - A line comment ended only at `\n`, while `Utf8JsonReader` ends it at `\r` too. `// a\r>` passed as comments only, and the first edit then failed. Comments now end where the reader ends them, and a document of only comments is checked by the reader.
- `Rtok.Agent.Sdk`: `HostFile.ParseStrictJson` accepted a lone surrogate escape that the write then threw on. It is now refused up front with `InvalidDataException`.
- `Pyrlyn.Config.Layered`:
  - Tomlyn throws `ArgumentOutOfRangeException` on a `\uD800` escape or a dangling escape. `ParseSyntax` turns it into `ConfigFileException` with the line, and `ParseValueOrString` keeps such text as a plain string.
  - `TomlEditor.Set` on a file with a BOM spliced one character early (`key =2021`), because Tomlyn's offsets start after the BOM.
- `Rtok.Mcp.Install` `TomlServers`: the same two Tomlyn issues. With a BOM, `With` was not idempotent. A surrogate escape now gives `InvalidDataException`.

Not done:
- libFuzzer itself was not run here. Apple clang ships no libFuzzer, Homebrew llvm is not installed, and no container runtime was up.
- `eng/fuzz.sh` passes shellcheck, but its first real run is still to come (`CLANG=/opt/homebrew/opt/llvm/bin/clang eng/fuzz.sh jsonc-edit -max_total_time=60`).
- `cli-argv` fuzzes `CommandPath.Check` against the real command model: the check that turns an unknown word or option into exit 2. The ConsoleAppFramework parse behind it is not fuzzed. It runs the real command bodies, which write config and spawn processes, and T11 left no way to swap in no-op bodies.

Check:
- `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test` (1267 total, 1256 passed, 11 skipped) and `dotnet csharpier check .` are green.
- `Rtok.Fuzz.Tests` passed three further runs with fresh random seeds.
- jscpd reports 5.52 %, over its 2 % threshold. The clones come from code already on `main` (T8's EF migrations, `tests/Rtok.Smoke/HookTests.cs`); none are in T53's files.
