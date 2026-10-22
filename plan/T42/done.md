# T42. Done

The `docs` plugin in `src/Rtok.Plugins.Docs` (new), its store methods in `Rtok.Store` and
`rtok docs fetch` in `src/Rtok.Cli`.

- Store: `DocItemMatch`, a keyless EF entity on the `doc_items_fts` table (no migration SQL:
  the table came with T8's schema). Its hidden `doc_items_fts` column is mapped as `Query`, so
  `f.Query == match` is FTS5 `MATCH`, and `rank` is bm25. `RtokStore` gains `DocCrateCached`,
  `SearchDocItems` (best rank first, ties by id, the text searched as quoted words by
  `PhraseQuery`, so no input is an FTS5 syntax error), `GetDocItem`, `SyncDocCrate` (the SHA
  compare and the write in one immediate transaction; unchanged bytes touch nothing and keep
  every id), `ReplaceDocCrate` and `DeleteDocCrate`.
- Lockfiles: `Cargo.lock` (the only one rtok read) at the root, then `packages.lock.json`
  breadth-first to depth 3 (dot dirs, `bin`, `obj`, `node_modules`, `target`, `artifacts` and
  reparse points skipped, 16 MB cap). A NuGet package is stored as `nuget:<lowercase id>`. A
  name or version outside `[A-Za-z0-9._+-]`, a leading dot or `..` is never used as a path.
  A missing or malformed file resolves to nothing.
- Local docs: `llms-full.txt` or `llms.txt` in the canonical cwd plus the files and
  directories it links to (`.md`, `.markdown`, `.mdx`, `.txt`, contained in the cwd, 200 files,
  1 MB each), split on `#` headings into `rel` or `rel#Heading` items, under the name `llms` and
  the version `local:<canonical cwd>`. Every resolve, query and fetch syncs them first by SHA,
  and a source that disappeared drops its items.
- Remote docs: docs.rs `json.zst` (rustdoc JSON, indexed as rtok's `index_rustdoc` did) and,
  new for .NET, the nupkg from nuget.org's flat container: the `lib/` or `ref/` XML doc with
  the most members, read with DTDs prohibited and no resolver. Downloads are capped (64 MB,
  512 MB unpacked, 30 s); the archive and its `.sha256` are kept under `~/.rtok/docs`.
- `DocsService`: `Resolve`, `Query` (snippets of `snippet_chars` code points while the estimate
  stays within `max_tokens`; a Measurement row `docs/query` with the before side estimated on
  the whole bodies it considered, `ref_id` `name@version`), `Get` and `FetchAsync` (a non-200,
  an over-cap body or a timeout leaves the cache as it was).
- `DocsPlugin`: `Mcp | Cli`, off by default, tools `docs_resolve`, `docs_query`, `docs_get`;
  `[plugins.docs]` `query_limit`, `snippet_chars`, `max_tokens`. Registered in
  `eng/RtokPlugins.targets`.
- `rtok docs fetch <name> [--config]`: help, man, completions and `docs/cli/commands.json`
  regenerated; `JsonTests` lists it. The User-Agent is `rtok/<version>` (`BuildInfo`, now
  shared with `--version`).
- New dependency: ZstdSharp.Port 0.8.8, a managed zstd decoder for docs.rs `json.zst`.

Checked:
- `Rtok.Plugins.Docs.Tests` (30): every case in the task's test list. Fixtures ingested and
  queried, `docs_query`/`docs_get` answers verified, the Measurement row, an unchanged
  re-ingest keeping ids, WireMock fetches for docs.rs and nuget.org (200 ingested; a 404, a
  timeout and an archive over either size cap leave the docs as they were), local docs and `llms.txt` links,
  each resolver's fixture and broken input, off by default with no tools listed.
- `Rtok.Store.Tests` `DocsTests` and `Rtok.AotTests` `StoreTests`: every doc method, the
  latter under NativeAOT on precompiled queries.
- Fuzz target `lockfiles` (`tests/Rtok.Fuzz/Targets/Lockfiles.cs`, five seeds): both resolvers
  and the `llms.txt` reader over any bytes give a safe pinned version or none.
- `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test`, `dotnet csharpier check .`
  and markdownlint green.

Notes for later tasks:
- T15 (MCP server): superseded by the follow-up below; the server dispatches through
  `IPlugin.CallTool`.
- T22 and any other FTS reader: EF's query precompiler (NativeAOT) refuses anonymous types,
  nested types and member-init projections, and drops variables captured inside a join's
  inner query (`KeyNotFoundException` at run time). What works: a keyless entity on the FTS
  table with a reference navigation to the content row, every captured value at the top
  level, and a projection to a top-level positional record.
- System.Text.Json source generation zeroes an `init` property with a default when its key is
  missing; config classes use `set`. `InjectConfig` and `SetupConfig` use `init` with
  defaults and may have the same hole.
- rtok's error said `not in Cargo.lock`; with two lockfiles it is `not in a lockfile: <name>`.

Commit: `T42: docs plugin: offline dependency docs`.

Follow-up (the creator approved the SDK member): `DocsPlugin.CallTool` answers `docs_resolve`,
`docs_query` and `docs_get` through a `DocsService` the host hands in. The cwd is the dispatch's,
else the process's. A missing argument, an unknown id or a host with no service throws, so it
becomes a tool error. `IPlugin.CallTool` itself came with T24. `MemoryHost.Cwd` lets a test name
the dispatch directory. `DocsCallToolTests` covers each tool, the missing arguments, another
plugin's tool (`null`) and a host with no service. Commit: `T42: docs plugin answers CallTool`.
