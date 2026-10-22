---
status: in progress
priority: P1
complexity: 5
readiness: 100%
agent: Claude Code / claude-opus-5-5
---

# T24. graph plugin: tags index and code-navigation tools

The symbol index answers navigation questions far cheaper than reading files.

Done when:
- The symbol index is built from tree-sitter tags (Pyrlyn.TreeSitter) while walking with gitignore.
- It is scoped per canonical root.
- Freshness is stat-gated, and an extractor fingerprint triggers a rebuild.
- The cold index uses batched transactions.
- MCP tools `symbol`, `callers`, `outline`, `impact` and `explore` behave as in the catalog: caps with archive ids, a staleness line, ambiguity marks, co-change data.
- Auto-index and watch run on `FileSystemWatcher` with debounce and overflow handling.
- `/` and `$HOME` are never a root.

Tests:
- `Rtok.Plugins.Graph.Tests` integration: indexing a temp repo fills `symbols` with definitions and references, including type-position and scoped-call refs, from tree-sitter tags and skips gitignored files.
- `Rtok.Plugins.Graph.Tests` unit: a root reached through a symlink shares the canonical root's index, and two distinct roots never see each other's rows.
- `Rtok.Plugins.Graph.Tests` unit: a re-index with unchanged mtime and size hashes no file, an unreadable file is stored with stat (0,0), and a removed directory purges its rows.
- `Rtok.Plugins.Graph.Tests` unit: changing a tags query, a grammar version or `INDEX_VERSION` changes the extractor fingerprint and triggers a full rebuild.
- `Rtok.Plugins.Graph.Tests` integration: a cold index commits 200 files per transaction with a single writer.
- `Rtok.Plugins.Graph.Tests` Verify: `symbol`, `callers`, `outline`, `impact` and `explore` answers on the labelled fixture repo, including "+N other definitions of <name>", ambiguity marks and `paths:` chains.
- `Rtok.Plugins.Graph.Tests` CsCheck: an answer over its cap carries an archive id whose `expand` returns the full answer byte for byte, and `impact` stays within `impact_tokens` 1500 and depth 4.
- `Rtok.Plugins.Graph.Tests` integration: after HEAD moves in a temp git repo, answers carry `index <old> head <new>`, and co-change lines read `changes with: a (7)` with at most 5 entries.
- `Rtok.Plugins.Graph.Tests` integration: with `auto_index=true` and `watch=notify`, an edit re-indexes only the changed path after the debounce, and a watcher overflow falls back to a full walk.
- `Rtok.Plugins.Graph.Tests` unit: `/` and `$HOME` are refused as a root.
- `Rtok.Plugins.Graph.Tests` integration: the labelled truth fixture reaches recall ≥ 0.92.
- `Rtok.Benchmarks` bench: a warm re-index of an unchanged 3000-file fixture takes under 0.1 s.
- `Rtok.AotTests` AOT: the graph plugin indexes a small fixture through the statically linked tree-sitter grammars.

Spec: catalog A11 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T8.1, T8.2, T8.3, T8.4, T8.5, T8.6, T8.7, T8.13, T8.15, T8.16, T8.17, T21.2, T35.1, T35.2, T35.3, T35.4, T35.5, T36.16, T36.17, T39.0, T41.1, T52.1, T52.2, T52.3, T52.4, T52.5, T59.3, T68.1, T68.2, T68.3, T68.4, T68.5, T68.6, T68.7, T68.9, T68.10, T181, T327, T368, T371, T376, T377, T387, T474, T620, T621, T646, T654, T663, T664, T665.

Fuzz: `outline` in `tests/Rtok.Fuzz`: any source bytes through each grammar's tags query give ranges inside the file and no native crash.

Check: The labelled truth test reaches recall ≥ 0.92, and the warm re-index of an unchanged 3000-file fixture takes under 0.1 s.

## Plan

Scope: rtok's `plugins/graph` index, walk, watch and the five tools (`mod.rs`, `index.rs`,
`walk.rs`, `watch.rs`, `cochange.rs`, `resolve.rs`, `blast.rs`, `tally.rs`, `jobs.rs`,
`status.rs`) and `plugins/read/outline.rs`. Projects, links, diff, export, health and
`affected` are T25; the repo map is T26; LSP and text backends are T24.1.

1. `src/Rtok.Plugins.Read/Outline`: rtok's tag hits (imports expanded, byte spans) and the
   map/signatures render over `Pyrlyn.TreeSitter`; the graph plugin and T19 share it, as in rtok.
2. `src/Rtok.Plugins.Graph`: `Matcher` over `Pyrlyn.Ignore` (include/exclude/extensions,
   nested worktrees skipped, `/` and `$HOME` refused); the indexer (stat gate, sha only when
   the stat differs, extractor fingerprint, parse on workers with one writer, batches of 200
   files through `ISymbols.ReplaceSymbolFiles`, scope, doc, content hash).
3. Tools `symbol`, `callers`, `outline`, `impact`, `explore` with filters, caps and archive
   ids, the staleness line, ambiguity marks, name resolution (imported > same dir > IDF),
   co-change from `git log -n300`, the impact budget.
4. Auto-index and watch on `FileSystemWatcher`: 250 ms debounce, pending cap, full walk on
   overflow.
5. `ISymbols` on the store: T8 landed first, so `RtokStore` implements it directly in plain
   SQL on its connection (rtok-store `symbols.rs`; walks, FTS5 and aggregates the EF query
   precompiler cannot take), with rtok's store tests ported to `Rtok.Store.Tests` and one
   AOT pass over every method. No in-memory port: one logic, one place.
6. Tests ported from rtok (`graph/*` unit tests, `graph_truth.rs` recall >= 0.92,
   `graph_contract.rs`), warm re-index of a 3000-file fixture under 0.1 s.

