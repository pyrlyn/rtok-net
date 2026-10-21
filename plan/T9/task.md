---
status: todo
priority: P1
complexity: 3
readiness: 0%
agent:
---

# T9. Archive and expand: lossless originals with short ids

Everything rtok shortens must come back byte-identical through `expand` (rtok D4).

Done when:
- Archive bodies are content-addressed files, written temp + rename, in hash-sharded directories (rtok I-S3). Rows hold size, sha256, session and agent.
- Ids print as 8 hex digits everywhere. Any unique prefix of 8 or more digits resolves; an ambiguous prefix lists its candidates.
- `rtok expand <id> [--lines a-b] [--grep re] [--context N]` and MCP `expand` behave as in the catalog, including freeze, range validation before the transaction, and the non-hex id hint.
- The line-range parser accepts the spellings agents send (rtok T522's 25 logged strings) and still rejects non-ranges.

Tests:
- `Rtok.Archive.Tests` integration: a body is written temp + rename to a hash-sharded path named by its sha256, the row holds size, sha256, session and agent, and archiving the same bytes twice keeps one file.
- `Rtok.Archive.Tests` CsCheck: for any body, including CRLF, invalid UTF-8 and empty ones, `expand` returns the archived original byte for byte.
- `Rtok.Archive.Tests` unit: every shortening path's trailer prints the id as 8 hex digits, while the Measurement `ref_id` keeps the full id.
- `Rtok.Archive.Tests` unit: a unique prefix of 8 or more hex digits resolves, a shorter one is refused, and an ambiguous one lists its candidates by 12-char prefix.
- `Rtok.Archive.Tests` unit: the line-range parser accepts all 25 strings logged in rtok T522 (`420 - 640`, `580:650`, `[1, 50]`, `610 614`, `95<br>`, `235"-"270`, `1860_1960`, …) as a theory and rejects non-range strings.
- `Rtok.Archive.Tests` CsCheck: for any `a <= b`, the spellings `a-b`, `a:b` and `a - b` parse to the same range.
- `Rtok.Archive.Tests` integration: a range past the body's line count fails before the freeze transaction, so no freeze and no Measurement row is written, and an empty archive rejects every range.
- `Rtok.Archive.Tests` integration: `expand` freezes the id and writes the freeze and its negative Measurement in one immediate transaction.
- `Rtok.Cli.Tests` Verify: `rtok expand <id>` output plain, with `--lines 3-5`, with `--grep` as numbered `N:line` hits, and with `--grep --context 1`.
- `Rtok.Cli.Tests` Verify: `rtok expand` given a non-hex id prints the hint that it takes the hex id from an `expand <id>` trailer and does not read stdin.
- `Rtok.Mcp.Tests` Verify: MCP `expand(id, lines)` returns the same body as the CLI for the same id and range, and an out-of-range request is an error result.

Spec: catalog A6, catalog B10 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T3.5, T36.20, T45.3, T55.11, T67.1, T67.2, T189, T475; open T522, I-S3 ([open work](../../docs/port/open-work.md)).

Check: A CsCheck property test shows that every shortening path expands back byte-identical (CRLF, non-UTF-8, empty bodies), and all 25 range strings parse.
