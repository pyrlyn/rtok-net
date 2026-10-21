---
status: in progress
priority: P0
complexity: 3
readiness: 100%
agent: Claude Code / claude-opus-5-5
---

# T1. Solution skeleton with a fast JIT Debug loop and AOT Release

The repo has no code yet. This task sets up the build that every later task relies on (`plan/README.md` N1-N3, N6).

Done when:
- `global.json` pins SDK 10.0.402 with `rollForward: latestFeature` and selects Microsoft.Testing.Platform as the test runner.
- `rtok.slnx`, `Directory.Build.props`, `Directory.Build.targets` and `Directory.Packages.props` (central package management, transitive pinning) are in place, with the artifacts output layout.
- Debug: JIT only, analyzers off during build, no doc file, `TieredPGO=false`, apphost on.
- Release: `PublishAot=true` on executables only, IL trim and AOT warnings as errors (except the EF Core assemblies' own collapsed warnings, N7). A `DebugAot` configuration exists.
- Libraries set `IsAotCompatible=true`.
- `.editorconfig`, CSharpier as a local tool, and the analyzers from the package map are on. BannedApiAnalyzers forbids `Console.Write*` outside surface projects and `Process.Start` outside the ProgressRunner (T11).
- `src/Rtok.Cli` and `src/Rtok.Hook` exist as empty executables. `rtok --version` prints `rtok X (<sha9>)`.
- A short `CONTRIBUTING.md` section names the inner-loop commands.

Ports: no rtok task; new for .NET.

Execution plan:
1. Repo files: `global.json`, `rtok.slnx`, `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, `.editorconfig`, `BannedSymbols.txt`, `.config/dotnet-tools.json`.
2. `src/Rtok.Cli` (ConsoleAppFramework, `--version`) and `src/Rtok.Hook` (prints the no-op reply).
3. Version: `0.1.0+<sha>` from git into the informational version.
4. Verify: Debug incremental build time, Release AOT publish with zero warnings, `rtok --version`.

Check: On macOS: a one-line edit plus `dotnet build --no-restore` finishes in under 1 s, and `dotnet publish src/Rtok.Cli -c Release -r osx-arm64` produces an AOT `rtok` whose `--version` works, with zero IL warnings.
