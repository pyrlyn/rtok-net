# T2. Done

The test infrastructure of N11 is in place. Its lanes and helpers are documented in `tests/README.md`.

**Lanes:**
- **JIT lane:** `*.Tests` projects (in `tests/` and `packages/<Name>/tests`). `tests/Directory.Build.*` gives them xUnit v3 on MTP and `using Xunit`. `Rtok.Testing.Tests` has one sample per library:
  - a Verify snapshot;
  - a CsCheck property;
  - a WireMock.Net fake.
- **AOT lane:** `Rtok.AotTests` uses source-generated xUnit (`xunit.v3.aot.mtp-v2`). It is excluded from the JIT run. The smoke lane publishes it with NativeAOT and runs the executable. Its tests prove they run as native code (empty `Assembly.Location`), without dynamic code, and with source-generated JSON.
- **Smoke lane:** `Rtok.Smoke` runs the published `rtok` and `rtok-hook` through CliWrap and Porta.Pty:
  - `--version`;
  - each hook fixture plus empty stdin, which must give exit 0 and `{}`;
  - a PTY run;
  - the AOT lane.

**Helpers** (`tests/Rtok.Testing`):
- `TempDir`.
- `MemFs`: Testably MockFileSystem with Windows, macOS or Linux rules, over the TestableIO `IFileSystem` seam.
- `RepoPaths`.
- `PublishedApps`: publishes once per run into `artifacts/smoke`, or uses `RTOK_SMOKE_BIN`.
- Fixtures in `tests/fixtures/hooks`.

**Tools** (`RtokJitOnly`, never published with AOT):
- `Rtok.Latency`: hyperfine over every hook fixture. It reports p50/p95 and fails on a median over 10 ms or 20 % over `baseline.json`. The baseline for osx-arm64 is about 4.4 ms.
- `Rtok.Benchmarks`: BenchmarkDotNet; results go to `artifacts/benchmarks`.
- `Rtok.Fuzz`: SharpFuzz targets. They are stubs that name the task that wires each one.

**Verify licence:** Verify runs under its OpenSource sponsorship exemption (`Directory.Build.props`, until 2027-10). The creator's decision is recorded under N11.

**Checked** on macOS arm64, after rebasing on `main`:
- `dotnet build rtok.slnx -c Release -warnaserror`: green.
- `dotnet test`: 245 passed, 1 skipped. This includes Rtok.Smoke and the published AOT lane.
- `dotnet csharpier check .`: clean.
- `dotnet run --project tests/Rtok.Latency -c Release`: passes.

**Follow-up: publish lock.** `dotnet test` runs Rtok.Hooks.Tests and Rtok.Smoke in separate processes, and both publish into `artifacts/smoke` at the same time. That collided with MSB3883 and an ilc exit of 1, and the in-process `Gate` did not cover it.
- `PublishedApps.LockAsync` now holds an exclusive lock on `artifacts/smoke.lock` for the whole publish. Other processes wait, up to 30 minutes, and their publish then finds the output up to date. Each root keeps a single output, so publish time and disk use stay as they were.
- `PublishedApps.Dotnet` is the single place that runs nested `dotnet` with the MSBuild variables cleared.
- `RepoPaths.Root` falls back to the working directory for binaries built outside the repo.
- Tests:
  - `Rtok.Testing.Tests/PublishLockTests`: a second holder waits for the first and times out with the sharing error. Both tests fail when the lock is off.
  - `Rtok.Smoke/PublishLockTests`: two file-based probe processes publish `rtok-hook` at once while the test holds the lock. Both wait, then get the same path, and the executable runs.
