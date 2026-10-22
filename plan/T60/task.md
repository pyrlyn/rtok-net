---
status: in progress
priority: P3
complexity: 1
readiness: 10%
agent: Claude Code / claude-opus-5-5
---

# T60. Mapperly for the store's ledger input rows

The creator wants Mapperly where code copies one object into another. In the store that is
`MeasurementInput` to `Measurement` (`RtokStore.InsertMeasurements`) and `UsageInput` to
`Usage` (`RtokStore.NewUsage`): every input member lands in the entity column of the same
name, next to the values only the store knows (time, session, once key, call id). A new
input member the hand-written copy forgets is silently dropped; a generated mapper that
fails the build on an unmapped member cannot drop it.

Done when:
- `Riok.Mapperly` is a central package version, referenced by `Rtok.Store` only as a
  compile-time generator (`PrivateAssets` and no runtime asset); the commit gives the
  one-line reason.
- One mapper in `Rtok.Store` maps both inputs; the store's own values come in as mapping
  parameters, and an unmapped source or target member is a build error.
- `InsertMeasurements`, `InsertUsage` and `InsertUsageRows` write the same rows as before.
- The package map in `plan/README.md` lists Mapperly.

Check: `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test`,
`dotnet csharpier check .` and `npx --yes markdownlint-cli2@0.23.3` are green, and a member
added to `MeasurementInput` without a `Measurement` column fails the build.

Tests: `Rtok.Store.Tests` (WriteTests, LedgerTests, BatchTests, UsageReadTests) and
`Rtok.AotTests` StoreTests, unchanged.

## Plan

1. `Directory.Packages.props`: `Riok.Mapperly` 4.3.1; `Rtok.Store` references it with
   `PrivateAssets="all" ExcludeAssets="runtime"`.
2. `src/Rtok.Store/LedgerMapper.cs`: `[Mapper]` with source and target members required,
   `ToMeasurement` and `ToUsage`; entity `Id` ignored.
3. Call it from `RtokStore.State.cs` and `RtokStore.Calls.cs`; drop the hand-written copies.
4. Package map row; build, test, format, lint; the seeded-member check.
