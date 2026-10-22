# T9. Archive and expand: lossless originals with short ids — done

What was done:
- Rtok.Plugin.Sdk 0.2.0: `ArchiveIds.Abbreviate` prints an archive id as 8 hex digits and is
  the one place every trailer takes its id from. `InjectBudget`'s `dropped:post_tool` marker
  uses it, and so do the cmd plugin's markers and `IdenticalResult`, which used
  `Names.ShortId` (removed; it did the same). `MemoryHost` ids are now the full sha256, as the
  store's are.
- Rtok.Store:
  - Archive files are sharded as `<archive_dir>/<id[0..2]>/<id>`, still written temp + rename.
    A read tries the live sharded path first, then the path the row stored.
  - `RtokStore.Archive.cs`:
    - `PutArchive(session, body, agentId)` writes the file and its row (size, sha256, session,
      agent); the same bytes twice keep one file and one row. A failed row insert removes a
      file this call created.
    - `GetArchive`.
    - `ArchiveIdsWithPrefix`: a range compare on the id, no `LIKE`, lowercase hex only.
    - `PutArchiveDecision` (the first writer wins) and `LivePointer`, which says which plugin
      shortened the id.
    - `FreezeRecorded`: the freeze and the expand Measurement in one immediate transaction,
      the Measurement only when this call froze the id.
- Rtok.Archive (new, foundation layer):
  - `LineRange` ports rtok's `--lines` grammar plus the spellings agents send (rtok T522):
    quotes, brackets, `,` `:` `_` for the dash, spaced and space-separated pairs, quoted
    dashes and `<br>`. The end is clamped to the line count; a start past it fails, so an
    empty body rejects every range.
  - `ArchiveLines`: rtok's line split, `--grep` hits as `N:line` with merged `--context`
    windows and `--` separators, and the `[expand] max_lines` cut with its
    `expand <8 hex>` trailer. A pattern that does not compile is matched literally.
  - `Expander` is the one expand handler (id, lines, grep, context → bytes or error) that
    the CLI calls now and MCP calls in T15. It does the following:
    - It validates the range spelling before any lookup.
    - It resolves a unique prefix of 8 or more hex digits; an ambiguous prefix lists its
      candidates by 12-char prefix, and a non-hex id gets the hint about the `expand <id>`
      trailer.
    - It checks the range against the real line count before the freeze transaction.
    - It writes the freeze and its negative Measurement for the live decision's plugin.
    - It returns the raw bytes when no view is asked for.
- `rtok expand <id> [--lines a-b] [--grep re] [--context N] [--config]` in Rtok.Cli. It
  prints the bytes as they were and never reads stdin. Errors go to stderr with exit 1. The
  command model, help, man and completion snapshots include it, and `FlagCoverageTests` maps
  its flags to `[expand]`.
- `plan/T15/task.md` carries the MCP Verify (creator's decision): MCP `expand` through the
  real server returns what `rtok expand` prints, and an out-of-range request is an `isError`
  result.

Deviations from the plan:
- `ExpandLines` is named `ArchiveLines`.
- There is no `ArchiveDecision` read method and no store-backed `IArchive` host. Nothing calls
  them yet; the hook (T13) and the proxy add them with their first caller.
- The AOT test is a new `tests/Rtok.AotTests/ArchiveTests.cs`, not part of `StoreTests`.

How it was checked:
- `tests/Rtok.Archive.Tests`:
  - store: the sharded path and the row; the same bytes twice keep one file; a read by the
    live path, then by the stored path; prefix lookup at the range edges; the first decision
    wins;
  - CsCheck: any body, including CRLF, invalid UTF-8 and empty ones, expands back byte for
    byte;
  - ids: a prefix of 8 resolves and 7 is refused; an ambiguous prefix lists its candidates by
    12 chars; a non-hex id gets the hint; the `max_lines` trailer prints 8 hex digits while
    the Measurement `ref_id` keeps the full id;
  - `LineRange`: the 25 strings from rtok T522 as a theory, rtok's own grammar, non-ranges
    refused, an empty body rejecting every range, and a CsCheck property that `a-b`, `a:b` and
    `a - b` parse alike;
  - freeze: a range past the line count or a malformed one writes no freeze and no
    Measurement; one freeze and one Measurement per plugin; no decision, no Measurement; a
    failing Measurement insert rolls the freeze back.
- `tests/Rtok.Cli.Tests/ExpandCommandTests` drives the binary:
  - Verify: plain, `--lines 3-5`, `--grep` and `--grep --context 1`;
  - a non-hex id prints the hint and exits 1 with stdin left open;
  - every view the CLI prints equals what `Expander.Run` returns.
- `tests/Rtok.AotTests/ArchiveTests`: an end-to-end expand on precompiled queries in the
  published NativeAOT executable.
- Fuzz target `expand-range` (`tests/Rtok.Fuzz/Targets/ExpandRange.cs`, seeds in
  `corpus/expand-range/`): range and id strings never throw, and an archived body expands
  back byte-identical.
- `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test`, `dotnet csharpier check .`
  and `npx --yes markdownlint-cli2@0.23.3` are green.
