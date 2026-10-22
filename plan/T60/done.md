# T60. Done

- `Riok.Mapperly` 4.3.1 is a central package version; `Rtok.Store` references it with
  `PrivateAssets="all" ExcludeAssets="runtime"`, so no Mapperly assembly lands in any
  `artifacts/bin` output.
- `src/Rtok.Store/LedgerMapper.cs` maps `MeasurementInput` to `Measurement` and `UsageInput`
  to `Usage`; time, session, once key and call id come in as mapping parameters, the entity
  `Id` is ignored. `RtokStore.MeasurementRow` and `RtokStore.NewUsage` call it in place of
  the hand-written copies.
- `.editorconfig` raises RMG012 and RMG020 to errors: a member either side leaves unmapped
  fails the build, in Debug as well as Release.
- `plan/README.md`: an object-mapping row in the package map, T60 in lane L2.

Checked: `dotnet build rtok.slnx -c Release -warnaserror` green; `dotnet test` 3645 total,
0 failed (11 skipped); `dotnet csharpier check .` and `npx --yes markdownlint-cli2@0.23.3`
clean. A `string? Extra` member seeded into `MeasurementInput` failed the Release and Debug
builds with RMG020 on `LedgerMapper.cs`, then was removed.

Commit: `T60: Mapperly for the store's ledger input rows`.
