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

Spec: catalog A6, catalog B10 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T3.5, T36.20, T45.3, T55.11, T67.1, T67.2, T189, T475; open T522, I-S3 ([open work](../../docs/port/open-work.md)).

Check: A CsCheck property test shows that every shortening path expands back byte-identical (CRLF, non-UTF-8, empty bodies), and all 25 range strings parse.
