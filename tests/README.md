# Tests

xUnit v3 on Microsoft.Testing.Platform (plan/README.md N11). `dotnet test` from the repo root
runs every lane below except the tools at the end.

| Lane | Project | What it runs |
| --- | --- | --- |
| JIT | `*.Tests` (here and in `packages/<Name>/tests`) | unit tests on the JIT: Verify snapshots, CsCheck properties, WireMock.Net fakes |
| AOT | `Rtok.AotTests` | source-generated xUnit (`xunit.v3.aot.mtp-v2`), run only as its published NativeAOT executable |
| Smoke | `Rtok.Smoke` | the published `rtok` and `rtok-hook` through CliWrap and Porta.Pty, and the published AOT lane |

Every test project is named `*.Tests` (or is one of the two above) and gets xUnit and
`using Xunit` from `tests/Directory.Build.*`. Test names read as sentences with underscores.

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
- `Rtok.Fuzz` (`rtok-fuzz <target>`): SharpFuzz targets for libFuzzer, the ones rtok fuzzed.
  Each is wired by the task that ports its code (`--list` names it). To fuzz one, instrument
  the assembly under test with `sharpfuzz` and run it under
  [libfuzzer-dotnet](https://github.com/Metalnem/libfuzzer-dotnet).

These tools set `RtokJitOnly`: they never publish with AOT.
