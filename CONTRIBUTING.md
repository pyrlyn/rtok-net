# Contributing to rtok-net

## Inner loop (Debug, JIT)

Debug is pure JIT: no AOT, analyzers off during build, no doc file. A one-line edit rebuilds in well under a second.

```bash
dotnet build rtok.slnx
dotnet build src/Rtok.Cli --no-restore
./artifacts/bin/Rtok.Cli/debug/rtok --version
dotnet watch --project src/Rtok.Cli
```

Run the apphost in `artifacts/bin/<Project>/debug/` directly; `dotnet run` adds about half a second.

## Checks before a commit

```bash
dotnet build rtok.slnx -c Release -warnaserror
dotnet test
dotnet csharpier check .
npx --yes markdownlint-cli2@0.23.3
```

`dotnet test` runs the JIT, AOT and smoke lanes; the smoke lane publishes `rtok` and `rtok-hook` with AOT first. Lanes, helpers, the latency gate, benchmarks and fuzzing: [tests/README.md](tests/README.md).

Analyzers, their severities and the shared build settings come from [packages/Pyrlyn.CodeStyle](packages/Pyrlyn.CodeStyle/README.md), which the root `Directory.Build.props` imports from source; the root `.csharpierrc.json` and the top of `.editorconfig` are its templates, and `.markdownlint-cli2.jsonc` extends its markdownlint rules. Rules for rtok alone stay in the root files.

Release runs every analyzer, including the banned-API rules in `eng/BannedSymbols.*.txt`: only surface projects (`<RtokSurface>true</RtokSurface>`) write to the console, and only ProgressRunner (`<RtokProcessRunner>true</RtokProcessRunner>`) starts processes.

## Release (NativeAOT)

Executables publish with NativeAOT in `Release` and in `DebugAot` (unoptimized, with symbols, for AOT-only failures). Publish on the OS you target: AOT does not cross-compile between operating systems.

```bash
dotnet publish src/Rtok.Cli -c Release -r osx-arm64
dotnet publish src/Rtok.Hook -c DebugAot -r osx-arm64
```

Output lands in `artifacts/publish/<Project>/<config>_<rid>/`. Trim and AOT warnings are errors.

## CI and releases

`.github/workflows/ci.yml` runs one job per OS (macOS arm64, Ubuntu x64 and arm64, Windows x64) and one lint job on Ubuntu. Every job does the same steps through the build tasks in `eng/Rtok.Build` (Cake Frosting, plan/T57), which run the same way on every OS; without `--target` it lists them all:

```bash
dotnet run --project eng/Rtok.Build -- --target=affected-tests --out=artifacts/affected.slnf --base=main   # JIT tests a change touches
dotnet run --project eng/Rtok.Build -- --target=publish --rid=osx-arm64 --aot-tests   # AOT rtok, rtok-hook (and the AOT lane) into artifacts/dist/<rid>/
dotnet run --project eng/Rtok.Build -- --target=smoke --rid=osx-arm64 --dir=artifacts/dist/osx-arm64
dotnet run --project eng/Rtok.Build -- --target=package-release --rid=osx-arm64 --rtok=artifacts/dist/osx-arm64/rtok --allow-missing
```

`linux-musl-*` publishes and smoke-tests in Alpine containers (docker). A `v<version>` tag runs `.github/workflows/release.yml`: archives `rtok-<rid>.tar.gz` (`.zip` on Windows) with checksums go into a draft GitHub release. Publishing the draft and `ketch push` (`ketch.toml`) are manual. The Claude and Codex marketplace catalogs (`.claude-plugin/`, `.agents/plugins/`) are published by merging to `main`. Actions are pinned to commit SHAs; update the SHA and its version comment together.

## Packages

Add a package version to `Directory.Packages.props` together with the task that first needs it. Libraries are `IsAotCompatible` automatically.

Own NuGet packages live in `packages/<Name>/`; create one with the `new-package` build task and publish with a `<Name>/v<version>` tag. Layout, versioning and publishing: [packages/README.md](packages/README.md).
