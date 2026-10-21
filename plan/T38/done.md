# T38. Done

`rtok agents usage` in `src/Rtok.Agents` (new app project) and `src/Rtok.Cli`.

- Readers (`Usage/`), all through one log-file walk (`LogFiles`: recursive, symlinks followed and
  deduplicated by real path, a cycle ends, mtime cutoff from `--since`) and one slice builder
  (`LogSlice`: no timestamp, before the window or no tokens yields nothing):
  - Claude Code: one request per `message.id`, a sub-agent transcript counts toward its parent.
  - Codex: `token_count` `last_token_usage`, cached input split out, `session_meta` id.
  - OpenCode and Kilo: `<host>.db` and `<host>-<channel>.db` opened read-only and unpooled
    with Microsoft.Data.Sqlite; assistant `message.data`, reasoning billed as output.
  - Copilot: the last `session.shutdown` per session; `tokenDetails` over `usage`.
  - Gemini: chat JSONL and the legacy `session-*.json`; the last record of a message id wins.
  - pi: an entry a fork copied counts once (id + timestamp, across files).
  - Kimi Code: every `usage.record` of `wire.jsonl`, session from the path.
  - Droid, Grok, ZCode and Antigravity are named in `skipped` when their folder holds anything.
- `UsageReports.Build`: `--source logs|rtok|both`, host filter, `--since`/`--until` as whole
  days in `--tz` (an IANA zone or local; a skipped or repeated midnight handled), daily or
  monthly buckets, `--by agent|model`, the unpriced list. Prices come only from
  `[stats.prices]`, looked up by the raw id, then without the provider prefix, then without
  `-YYYYMMDD`; the raw id stays in the output.
- `--source rtok|both` reads the store through `IUsageLedger` (`StoreLedger`). Two read methods
  were added to `Rtok.Store` for it: `UsageSlices` (usage rows grouped per request, with the
  session's host) and `MeasurementSavedByHost` (`est_before - est_after`, so an `expand` row
  counts as a cost). Saved tokens come only from Measurement rows; `both` adds the agents seen
  only through rtok and each agent's coverage.
- Text screen (decimal SI units with rtok's half-to-even `{:.2}`, `$` with grouping, the
  `! Cost is incomplete` line, tables padded like rtok's) and `--json` (`Utf8JsonWriter`,
  rtok's key order, serde-style floats).
- CLI flags map to `[agents.usage]` keys through `ConfigFlag`; a bad value exits 1 with the
  key named. Dirs come from `[agents.usage.dirs]` and
  `[stats] transcripts_dir`/`codex_dir`; the agents' own variables move an untouched default
  (T7's `ConfigPaths`).
- Wired into T11's shell: errors go through `ExitCodeFilter` (`Error: …`, exit 1; `--daily`
  with `--monthly` exits 2), `docs/cli/commands.json`, the help, man and completion snapshots
  include the command, and `JsonTests` lists it as a reading command.

Checked:
- `Rtok.Agents.Tests` (84): the rtok goldens over `tests/fixtures/usage-logs` (copied from
  rtok's trycmd cases), every reader's edge cases (OpenCode/Kilo against SQLite files built in
  the test, bytes and file list unchanged after the read), unsupported hosts and a bad line,
  dirs from a config file and an agent variable, DST gap and repeat midnights, the 23 h and
  25 h Kyiv days, sources, filters, sort order, price lookup, JSON numbers and field order, and
  a CsCheck property that the shown unit agrees with the exact count.
- Fuzz target `usage-logs` (`tests/Rtok.Fuzz/Targets/UsageLogs.cs`, a seed per reader from the
  fixtures): every host's reader over any text never throws, and each request has non-negative
  legs and total. It found two holes, fixed with a test: Codex's `input_tokens` minus a larger
  `cached_input_tokens` went negative, and counters near `long.MaxValue` could wrap when a
  reader adds them; every counter now goes through `Json.Count` (negatives as 0, capped at
  2^53 - 1).
- `Rtok.Cli.Tests`: Verify snapshots of the screen, `--daily` and `--json` from the built
  binary, `--source rtok` opening the store, bad values and flag conflicts.
- `Rtok.Store.Tests` (`UsageReadTests`) and `Rtok.AotTests` `StoreTests`: both read methods,
  the latter under NativeAOT with precompiled queries.
- `rtok agents usage` from a NativeAOT publish of `Rtok.Cli` over the fixtures.
- `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test` and
  `dotnet csharpier check .` green. Under the full parallel run, T14's hook tests flaked once
  each and pass alone: `HookResidentTests.A_hung_resident_fails_within_the_budget` (92 ms
  against a 90 ms cap), and three `Rtok.Smoke.HookTests` that found a file left in the queue
  directory (12/12 alone).

Notes for later tasks:
- T30 (transcripts, `rtok stats`) should reuse these readers and `UsageCost` rather than write
  its own: one logic, one place.
- Microsoft.Data.Sqlite reads the hosts' own databases, not an EF context: the creator allowed
  this exception to N7 (added to `plan/README.md`). The one parameterized SELECT lives in
  `HostDatabases`, next to the reader, with SQLite fixtures built in its tests.

Commit: `T38: Agent usage and cost: rtok agents usage`.
