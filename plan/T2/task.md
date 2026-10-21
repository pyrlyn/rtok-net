---
status: todo
priority: P0
complexity: 3
readiness: 0%
agent:
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
