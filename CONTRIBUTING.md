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
```

Release runs every analyzer, including the banned-API rules in `eng/BannedSymbols.*.txt`: only surface projects (`<RtokSurface>true</RtokSurface>`) write to the console, and only ProgressRunner (`<RtokProcessRunner>true</RtokProcessRunner>`) starts processes.

## Release (NativeAOT)

Executables publish with NativeAOT in `Release` and in `DebugAot` (unoptimized, with symbols, for AOT-only failures). Publish on the OS you target: AOT does not cross-compile between operating systems.

```bash
dotnet publish src/Rtok.Cli -c Release -r osx-arm64
dotnet publish src/Rtok.Hook -c DebugAot -r osx-arm64
```

Output lands in `artifacts/publish/<Project>/<config>_<rid>/`. Trim and AOT warnings are errors.

## Packages

Add a package version to `Directory.Packages.props` together with the task that first needs it. Libraries are `IsAotCompatible` automatically.
