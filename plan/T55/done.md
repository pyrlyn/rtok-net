# T55. Done

`packages/Pyrlyn.CodeStyle` is a development-dependency package with no assembly. Another pyrlyn
repository gets the whole setup from one `GlobalPackageReference`; rtok-net consumes the same
files from source.

## What it packs

- Dependencies (`include="All"`, so their analyzers reach the consumer): Roslynator.Analyzers
  5.0.1, Meziantou.Analyzer 3.0.315, Microsoft.VisualStudio.Threading.Analyzers 18.7.23 (new;
  18.12.1 was under two weeks old). Versions stay only in `Directory.Packages.props`: the
  analyzers carry `PyrlynCodeStyle="true"` there, and the package project turns global references
  off and references the marked ones itself.
- `build/Pyrlyn.CodeStyle.props`: Nullable, AnalysisLevel latest-recommended,
  EnforceCodeStyleInBuild, warnings as errors in CI, `nullable` and IL2026/IL2104/IL3050/IL3053/
  IL3058 as errors. Every value is a default, because NuGet imports package props after
  `Directory.Build.props`.
- `build/Pyrlyn.CodeStyle.globalconfig` through `GlobalAnalyzerConfigFiles`: file-scoped namespaces
  (IDE0161 as a warning), the Performance category as warnings, VSTHRD003 off (it needs a
  JoinableTaskFactory).
- `templates/`: `.editorconfig` (the generic head of rtok's), `.csharpierrc.json` (100 columns,
  LF, XML at 2 spaces: the same output as CSharpier's defaults gave before) and
  `.markdownlint.jsonc` (defaults without MD013 and MD060, MD024 by siblings).
  `NoDefaultExcludes` is needed, or NuGet drops the dotfiles (NU5119).

## rtok-net

- The root `Directory.Build.props` imports the package props from source. The root `.editorconfig`
  starts with the template and keeps only rtok's sections. `.csharpierrc.json` is the template,
  and `.markdownlint-cli2.jsonc` extends it. `Pyrlyn.CodeStyle.Tests` fails when any of them drifts
  (checked by changing `printWidth`).
- VS Threading findings: VSTHRD200 fixed by renaming (`ListenSocketAsync`, `ListenPipeAsync`, and
  the `StartAsync`, `ExitAsync`, `UntilCalledAsync` test helpers; after the rebase on T10 also
  `RunAsync` and `WaitForAsync` in the logs tests and `WriterProcess.RunAsync`). VSTHRD105 fixed with
  `TaskScheduler.Default` in three `StartNew` calls. VSTHRD002 and VSTHRD103 are off on rtok's side
  next to CA2007: no SynchronizationContext, and the hook client blocks by design.
- markdownlint-cli2 0.23.3 runs in the CI lint job and in the AGENTS.md and CONTRIBUTING.md
  checks. rtok's config also turns off MD022, MD032, MD034 and MD038 (house style) and ignores
  `plan/T*/` (task folders belong to their task's agent) and `CLAUDE.md`. Fixed: raw `<name>`
  placeholders that rendered as nothing in `docs/port/catalog.md`, an unescaped `|` that split a
  table row in `docs/port/open-work.md`, fence languages, blank lines, and the lost
  `## Open questions for the creator` heading in `plan/README.md`.
- `eng/publish-package.sh` requires a `.snupkg` only when the project packs symbols.

## Checks

- Before and after the move, the same diagnostics fire on a probe file (CA1805, CA1822, CA1825,
  MA0005). A block-scoped namespace still warns IDE0161, which now comes only from the globalconfig.
- A scratch project outside the repo with nothing but `PackageReference Pyrlyn.CodeStyle` from
  the packed nupkg reports CS8602 as an error and IDE0161, MA0004, VSTHRD100 and VSTHRD200 as
  warnings, and restores Roslynator's analyzers.
- `eng/publish-package.sh Pyrlyn.CodeStyle/v0.1.0 --dry-run` and the same for Pyrlyn.Durations
  (with a `.snupkg`) pass.
- `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test` after the rebase (1504 total, 1493 passed, 11 skipped), `dotnet csharpier check .` and
  `npx --yes markdownlint-cli2@0.23.3` are green.

Not run: shellcheck, which is not installed locally (CI runs it on `eng/*.sh`). jscpd fails on
`main` as well (4.6 % against a 2 % threshold), almost all of it in the generated EF migration
`InitialSchema`. This task adds no clones.
