# rtok-latency

The hook latency gate. It starts the resident (`rtok hook --serve`) in a temp home, times
`rtok-hook` against it with hyperfine (`-N`, 20 warmups, 200 runs) on every payload in
`tests/fixtures/hooks/`, prints p50 and p95 per fixture and fails (exit 1) when a p95 is over
10 ms or a median is more than 20 % over this RID's entry in `baseline.json`. The client's
fallback without a resident is bounded by the smoke tests (`tests/Rtok.Smoke/HookTests.cs`).

```bash
dotnet run --project tests/Rtok.Latency -c Release
dotnet run --project tests/Rtok.Latency -c Release -- --update-baseline
dotnet run --project tests/Rtok.Latency -c Release -- --budget-only
```

Both binaries are published with NativeAOT first, or taken from `RTOK_SMOKE_BIN` (CI). Run it on a
quiet machine: parallel builds skew the numbers. Update the baseline only when a slower hook
is accepted, and say why in the commit. CI runs `--budget-only`: its runners are not the machine
the baseline was measured on, so only the 10 ms median applies there.
