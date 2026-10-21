# T33 done

New package `packages/Rtok.Agent.Sdk` (0.1.0, references Pyrlyn.FsKit), the install contract
every host installer shares (rtok `crates/rtok-agent-sdk`, catalog A19 safety, B3 ownership and
`_backup` generations):

- `Apply` (`DryRun`, `Backup`, `BackupFiles` default 5, `Yes`, `Ask`, `Run`): `Writes(report)` is
  the write gate (`no changes`, or only `leave …` / `? …` lines, write nothing). Questions go to
  a `Prompt` delegate the surface supplies (the library never touches the console); no prompt
  is a no, `--yes` accepts. `Prompts.Line` / `Prompts.Answer` give the one `[y/N]` shape.
- `Backups`: `_backup/<name>.bak-<ts>[-<n>]`, one copy per distinct content, dedup only against
  the same file's generations, same-second copies numbered past the highest `-<n>`, never an
  overwrite; `Prune` keeps the newest (0 keeps all, fs errors ignored, only that name shape in a
  `_backup` folder); `Stale` lists what a lowered cap has no room for.
- `BackupRun`: copies taken uncapped during a run; `Finish(changed)` prunes to the cap or, for a
  run that changed nothing, deletes its own copies without costing an older generation.
- `HostFile`: atomic `Write`/`WriteJson` through `Pyrlyn.FsKit.AtomicFile` (symlink chains kept,
  parent created, literal `~` refused); strict `ReadJson` refuses JSONC with `file:line:col`
  and leaves the file; `EditJson`, `ObjectAt`, `ArrayAt`.
- `ServerEntries`: `RegisterMcp`/`Register` (dotted keys; equal entry, a missing
  `"type": "stdio"` included, is `no changes`), `Unregister` only after `Ownership.Judge`.
- `Ownership.Judge`: exact entry → remove; rtok entry the user edited → `? …` on a dry run, else
  `remove … ? you changed it [y/N]` (`leave … (changed by you; remove by hand)` when declined or
  no terminal); foreign → `leave … (not rtok's; remove by hand)`. Any rtok binary path and the
  `--host <id>` pair after `mcp` count as rtok's own.

Left to T34/T35 (host data, not the contract): plugin links, skill copies, JSONC/TOML hosts.

Also fixed main's Release `-warnaserror` break in `Pyrlyn.FsKit` tests (CA1416 on
`File.Get/SetUnixFileMode`): the two Unix-mode tests are marked `[SupportedOSPlatform]` for
linux and macos.

Checked: `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test` (215 passed; 53 in
Rtok.Agent.Sdk.Tests: install twice ≤1 entry and no second copy, dedup, same-second numbering,
cap and newest-kept, no-op run cleanup, each ownership case, symlink write-through, JSONC
refusal), `dotnet csharpier check .`, `dotnet pack` of the package.
