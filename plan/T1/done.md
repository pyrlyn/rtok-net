# T1. Done

Solution skeleton with a fast JIT Debug loop and NativeAOT Release.

What was done:
- `global.json` (SDK 10.0.402, `latestFeature`, MTP test runner), `rtok.slnx`, `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props` (central versions, transitive pinning, global analyzers Roslynator, Meziantou, BannedApiAnalyzers), artifacts output layout, `.editorconfig`, CSharpier 1.3.0 as a local tool, `CONTRIBUTING.md`.
- Configurations Debug (JIT, analyzers off during build, no doc file, `TieredPGO=false`, apphost), Release (AOT for executables, stripped) and DebugAot (AOT, unoptimized, symbols).
- `PublishAot` is set in `Directory.Build.targets`, where `OutputType` is known. The SDK infers `SelfContained` from `PublishAot` before that file is imported, so `SelfContained` is set there too; without it publish fails with NETSDK1102.
- Libraries get `IsAotCompatible`. Trim/AOT warnings and RS0030 are errors.
- Banned APIs: `eng/BannedSymbols.Console.txt` (off for `RtokSurface` projects) and `eng/BannedSymbols.Process.txt` (off for `RtokProcessRunner`).
- `src/Rtok.Cli` (`rtok`, ConsoleAppFramework) prints `rtok 0.1.0 (<sha9>)` for `--version`; the sha comes from git into the informational version. `src/Rtok.Hook` (`rtok-hook`) prints `{}` and exits 0.
- Deviation: the CLI parser is ConsoleAppFramework (README N9), not System.CommandLine; there are no test projects yet, so `dotnet test` starts with T2.

How it was checked (osx-arm64, M3 Max):
- Debug: a one-line edit plus `dotnet build src/Rtok.Cli --no-restore` took 0.59-0.65 s over three runs.
- `dotnet build rtok.slnx -c Release -warnaserror`: 0 warnings. A build with `-p:RtokSurface=false` reports RS0030 for `Console.Out`, so the banned-API rule is live.
- `dotnet publish -c Release -r osx-arm64`: 0 warnings; `rtok` 1.77 MB, `rtok-hook` 1.09 MB, native Mach-O. `rtok --version` printed `rtok 0.1.0 (3ebd2f71d)`. hyperfine: `rtok-hook` 4.9 ms, `rtok --version` 4.9 ms mean.
- `dotnet publish -c DebugAot -r osx-arm64` works; `dotnet csharpier check .` is clean.

Commit: on branch `T1-solution-skeleton`, fast-forwarded into `main`.
