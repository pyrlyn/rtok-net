# Tests

xUnit v3 on Microsoft.Testing.Platform (plan/README.md N11). `dotnet test` from the repo root
runs every lane below except the tools at the end.

| Lane | Project | What it runs |
| --- | --- | --- |
| JIT | `*.Tests` (here and in `packages/<Name>/tests`) | unit tests on the JIT: Verify snapshots, CsCheck properties, WireMock.Net fakes, Bogus data |
| AOT | `Rtok.AotTests` | source-generated xUnit (`xunit.v3.aot.mtp-v2`), run only as its published NativeAOT executable |
| Smoke | `Rtok.Smoke` | the published `rtok` and `rtok-hook` through CliWrap and Porta.Pty, and the published AOT lane |

Every test project is named `*.Tests` (or is one of the two above) and gets xUnit and
`using Xunit` from `tests/Directory.Build.*`. Test names read as sentences with underscores.

Data a test does not look at (counts, project names, paths, log text) comes from Bogus,
seeded so every run sees the same rows: `Rtok.Store.Tests/Fakes.cs`. Values the test checks
for their meaning (expected parser output, priced models, snapshot inputs, keys the asserts
name) stay literal.

Verify snapshots live next to their test as `*.verified.*`. A mismatch writes a
`*.received.*` file (ignored by git); review it and rename it over the `.verified` one to
accept. Verify runs under its OpenSource sponsorship exemption (`Directory.Build.props`).

## Helpers: `Rtok.Testing`

- `TempDir`: a fresh temp directory that deletes itself on dispose; nothing is left behind.
- `MemFs`: the virtual file-system seam (rtok D29). Code that touches files takes
  `System.IO.Abstractions.IFileSystem`; unit tests pass `MemFs.Create()`, or
  `MemFs.Windows()` / `MacOS()` / `Linux()` for another OS's path rules. Keep real disk for
  end-to-end behaviour.
- `RepoPaths`: the repo root and `tests/fixtures/` files.
- `PublishedApps`: the NativeAOT `rtok`, `rtok-hook` and `Rtok.AotTests`. They are published
  once per test run into `artifacts/smoke/` (its own artifacts root, so the RID restore never
  touches the JIT build), or taken from `RTOK_SMOKE_BIN` when CI has published them.
  Test processes take turns through a lock on `artifacts/smoke.lock` (`PublishedApps.LockAsync`),
  so projects that `dotnet test` runs at once never publish into one root together.
  The plugin variants pick their plugins at build time (N12) and are always published, each
  into its own root: `rtok+sample` links only `tests/Rtok.SamplePlugin`
  (`artifacts/smoke-sample/`), `rtok+measure` only the measure plugin
  (`artifacts/smoke-measure/`).

## Tools

```bash
dotnet run --project tests/Rtok.Latency -c Release                # hook latency gate
dotnet run --project tests/Rtok.Benchmarks -c Release -- --filter '*'
dotnet run --project tests/Rtok.Fuzz -c Release -- --list
```

- `Rtok.Latency`: hyperfine on every `tests/fixtures/hooks/*.json`; prints p50/p95 and fails
  over a 10 ms median or 20 % over `baseline.json` (its README).
- `Rtok.Benchmarks`: BenchmarkDotNet; results land in `artifacts/benchmarks/`.
- `Rtok.Fuzz` (`rtok-fuzz <target>`): SharpFuzz targets for libFuzzer, one per parser of
  outside input (plan/README.md N11). A target lives in `Targets/`, catches only the failure
  type its code documents, checks that code's invariants through `FuzzInput.Check`, and keeps
  seeds in `corpus/<target>/`. Unwired targets name the task that wires them (`--list`).
  `dotnet run --project eng/Rtok.Build -- --target=fuzz --fuzz-target=<target> [--flags="<libFuzzer flags>"]`
  publishes it, instruments the assemblies with
  `sharpfuzz` and runs it under [libfuzzer-dotnet](https://github.com/Metalnem/libfuzzer-dotnet),
  built from source with a clang that ships libFuzzer (`CLANG=/opt/homebrew/opt/llvm/bin/clang`
  on macOS). Add a crash input to the target's corpus once it is fixed.
- `Rtok.Fuzz.Tests` (a normal test project): every wired target over its corpus, random bytes
  and mutated seeds on each `dotnet test`. It also fails when a closed task left its target
  unwired, or a `Fuzz:` line names a target `FuzzTargets` lacks.

These tools set `RtokJitOnly`: they never publish with AOT.

## Fast local runs

`python3 eng/test.py` runs only the JIT test projects affected by your changes since
`origin/main` (commits, staged, unstaged and untracked files), using the same
`eng/affected-tests.sh` rules as CI on pull requests. `--all` runs everything, `--dry-run`
lists the projects, `--no-build` skips the build, and arguments after `--` go to `dotnet test`.
Test modules run in parallel (Microsoft Testing Platform), and `xunit.runner.json` at the root
parallelizes test classes inside each module (aggressive, 2x cores).
