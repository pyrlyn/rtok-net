---
status: in progress
priority: P0
complexity: 3
readiness: 10%
agent: Claude Code / claude-opus-5-5
---

# T2. Test infrastructure: xUnit v3, Verify, AOT lane, smoke and latency harness

Every later task proves its contracts with tests, so the test lanes come first (N11).

Done when:
- The test stack is set up: xUnit v3 on MTP, Verify.XunitV3 for snapshots, CsCheck for property tests, WireMock.Net for fake upstreams, Porta.Pty for interactive checks.
- There is one shared test-helpers project with a virtual file-system seam (rtok's D29) and temp dirs that delete themselves (`IDisposable`), so tests leave nothing behind (rtok T644).
- Three lanes exist:
  - JIT unit tests.
  - `tests/Rtok.AotTests`: published with AOT and run.
  - `tests/Rtok.Smoke`: drives the published `rtok` and `rtok-hook` through CliWrap.
- A latency harness runs hyperfine on hook fixtures. It fails when the median is over 10 ms or worse than the stored baseline by more than 20 %.
- `tests/Rtok.Benchmarks` (BenchmarkDotNet) and `tests/Rtok.Fuzz` (SharpFuzz) exist. The fuzz targets are the ones rtok fuzzed: CLI argv, config TOML, cmd filter, hook IO, JSONC edit, proxy wire, transcript JSONL, outline, text ops.

Ports: rtok open T644 ([open work](../../docs/port/open-work.md)).

Check: `dotnet test` runs one sample test in each lane (JIT, AOT, smoke) green on macOS, and the latency harness prints a p50/p95 table for the empty `rtok-hook`.

Plan:
1. `tests/Directory.Build.props|targets`: `*.Tests`, `Rtok.Smoke` and `Rtok.AotTests` are xUnit v3 (MTP v2) test executables; `RtokJitOnly` keeps Benchmarks, Fuzz and Latency off AOT.
2. `tests/Rtok.Testing`: `TempDir` (deletes itself), `MemFs` (the VFS seam: System.IO.Abstractions `IFileSystem`, Testably mock with OS simulation), repo/fixture paths, `PublishedApps` (AOT publish into a separate artifacts root, or `RTOK_SMOKE_BIN`).
3. Lanes: `tests/Rtok.Testing.Tests` (JIT: Verify, CsCheck, WireMock samples), `tests/Rtok.AotTests` (`xunit.v3.aot.mtp-v2`, published with AOT), `tests/Rtok.Smoke` (CliWrap and Porta.Pty against published `rtok`, `rtok-hook`; runs the published AOT tests).
4. `tests/Rtok.Latency`: hyperfine over `tests/fixtures/hooks/*.json`, p50/p95 table, fails over 10 ms median or 20 % over `baseline.json`.
5. `tests/Rtok.Benchmarks` (BenchmarkDotNet sample), `tests/Rtok.Fuzz` (SharpFuzz, rtok's nine targets, each naming the task that wires it).
6. Verify: Release `-warnaserror`, `dotnet test`, csharpier, latency table.
