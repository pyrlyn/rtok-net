# T59. Done

- `Bogus` 35.6.5 is a central package version, referenced only by `Rtok.Store.Tests`.
- `tests/Rtok.Store.Tests/Fakes.cs`: seeded generators for `MeasurementInput`, `UsageInput`
  and `Symbol`, plus `Values()` for loose project names, paths, counts and log text. Each
  call starts from the same seed, so a test sees the same rows on every run.
- WriteTests, RetentionTests, BatchTests and LedgerTests take those values in place of
  made-up literals; round-trip asserts now compare against the generated values. The
  hand-built `Symbol` in RetentionTests is gone.
- Left literal on purpose: `UsageReadTests` (sums and savings are the point), `CallIoTests`
  (byte bodies), `SymbolsTests` (names and lines drive search), and all of
  `Rtok.Agents.Tests`, whose `UsageSlice` values are priced models, token sums or expected
  parser output. Parsers, fuzz targets, snapshots and `Rtok.AotTests` keep their data.
- N11, the package map and `tests/README.md` say when a test takes Bogus data.

Checked: `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test`,
`dotnet csharpier check .` and `npx --yes markdownlint-cli2@0.23.3` green;
`git grep -l Bogus -- src packages/*/src tests/Rtok.AotTests tests/Rtok.Fuzz` prints nothing.
