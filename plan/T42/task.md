---
status: in progress
priority: P3
complexity: 3
readiness: 100%
agent: Claude Code / claude-opus-5-5
---

# T42. docs plugin: offline dependency docs

rtok answered dependency-doc questions offline from lockfile-pinned versions. The port keeps the tools and local docs, and adds .NET lockfiles.

Done when:
- MCP `docs_resolve`, `docs_query` and `docs_get` work over an FTS table.
- `rtok docs fetch` works.
- Local docs and `llms.txt` work.
- Lockfile resolvers cover the ecosystems rtok supported plus `packages.lock.json`.
- It is off by default.

Tests:
- `Rtok.Plugins.Docs.Tests` integration: a docs fixture is ingested into the FTS table, and `docs_resolve` resolves a package to its lockfile-pinned version.
- `Rtok.Plugins.Docs.Tests` Verify: `docs_query` and `docs_get` answers on the ingested fixture.
- `Rtok.Plugins.Docs.Tests` unit: a `docs_query` answer writes a Measurement row estimated against the full bodies.
- `Rtok.Plugins.Docs.Tests` integration: an unchanged re-ingest keeps every doc id (SHA compare).
- `Rtok.Plugins.Docs.Tests` WireMock: `rtok docs fetch` downloads pinned-version docs from a fake upstream and ingests them, and a 404 or timeout leaves existing docs untouched.
- `Rtok.Plugins.Docs.Tests` integration: a local docs directory and an `llms.txt` with links are ingested under the scope `local:<canonical cwd>` and are queryable.
- `Rtok.Plugins.Docs.Tests` unit: each lockfile resolver rtok supported, starting with `Cargo.lock`, plus `packages.lock.json`, returns the pinned name and version from its fixture.
- `Rtok.Plugins.Docs.Tests` unit: a missing or malformed lockfile resolves to nothing instead of throwing.
- `Rtok.Plugins.Docs.Tests` unit: with default config the plugin is off and `docs_resolve`, `docs_query` and `docs_get` are not listed.

Spec: catalog A23 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T455.

Fuzz: `lockfiles` in `tests/Rtok.Fuzz`: each lockfile resolver and the `llms.txt` reader over any bytes give pinned versions or none.

Check: Ingest and query fixtures pass, and an unchanged re-ingest keeps doc ids.

Plan:
1. Store: an EF-mapped read-only view of `doc_items_fts` (MATCH via its hidden column, bm25 `rank`) and doc methods on `RtokStore` (sync by SHA, replace, delete, search, get); JIT and AOT store tests.
2. `src/Rtok.Plugins.Docs`: lockfile resolvers (`Cargo.lock`, `packages.lock.json`), `llms.txt` and linked local docs under `local:<canonical cwd>`, rustdoc JSON and nupkg XML-doc indexers, `DocsService` (resolve, query with a Measurement, get, fetch), `DocsPlugin` (Mcp|Cli, off by default).
3. `rtok docs fetch <name>` in `Rtok.Cli`; CLI goldens refreshed.
4. `tests/Rtok.Plugins.Docs.Tests` per the list above (WireMock upstream); fuzz target `lockfiles` with a corpus.
5. Gates, `done.md`.
