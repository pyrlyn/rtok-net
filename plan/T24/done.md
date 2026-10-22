# T24. Done

The graph plugin: a tree-sitter tags index per canonical root and the five code-navigation
tools, ported from rtok's `plugins/graph` (index, walk, watch, cochange, resolve, blast) and
`plugins/read/outline.rs`.

What was done:
- `Rtok.Plugins.Read`: `Outline` over `Pyrlyn.TreeSitter` (tag hits with byte spans, imports
  expanded, the `map`/`signatures` renders), shared with T19 as in rtok.
- `ISymbols` on `RtokStore` in plain SQL on its connection (rtok-store `symbols.rs`: walks,
  FTS5, idf, aggregates), with rtok's store tests in `Rtok.Store.Tests` and an AOT pass over
  every method in `Rtok.AotTests`.
- `Rtok.Plugins.Graph`:
  - `GraphRoot` refuses `/`, `$HOME` and non-directories.
  - `Matcher` walks with `Pyrlyn.Ignore` and applies include, exclude and the extension map. It
    skips nested linked worktrees.
  - `Indexer`:
    - A stat gate, with sha256 only when the stat moved.
    - An extractor fingerprint over the queries, grammar versions, the index version and the
      mapping; a mismatch reindexes cold.
    - Parse workers feed one writer, in batches of 200 files.
    - Each row keeps its enclosing scope, doc and content hash. `RunChanged` handles event paths.
  - Tools `symbol` (`name`, `names`, `id`), `callers`, `impact` (`depth`, `to`), `outline`
    (`kind`) and `explore`. They take `path`/`kind`/`all` filters and print a staleness line
    and `index <old> head <new>`. They also print ambiguity marks and "+N other definitions".
    Name resolution takes import evidence, then the same directory; a name referenced by more than about 5 % of files stays unresolved. `impact` takes the token budget and
    the co-change line (last 300 commits, top 5). Caps archive the full answer under an id
    `expand` returns.
  - `GraphWatch.Start` runs `FileSystemWatcher` with a 250 ms debounce and a pending cap of 1024.
    An overflow, a watcher error or a removed directory triggers a full walk. Edits through
    Edit/Write mark the file stale.
  - `Git` reads HEAD and the files of the last 300 commits through LibGit2Sharp (N21). Each
    commit's files come from a tree diff against its first parent: no renames, and no files for
    a merge. The result matches `git log --name-only --relative --no-renames`. A root outside
    any repository opens nothing. LibGit2Sharp never runs on the hook path: `OnPostTool` only
    marks a file stale.
- `Rtok.Git` (new foundation project, the one place that references LibGit2Sharp): the type
  `GitRepository` in `src/Rtok.Git/GitRepository.cs`.
  - `FindTop(path)` walks up to the nearest `.git` directory or linked worktree `.git` file.
  - `Open(path)` opens the repository there.
  - The gap: under NativeAOT, LibGit2Sharp 0.32.0's `Repository.Discover` returns null, while
    `new Repository(top)` works. `tests/Rtok.AotTests/GitTests.cs`
    (`Discover_is_null_on_native_aot_and_git_repository_opens_instead`) proves both. T40 and
    T41 open repositories through it. `tests/Rtok.Git.Tests` covers the walk on the JIT.
- `Pyrlyn.TreeSitter`: `Queries/c_sharp/extra.scm` gives C# record, struct and enum definitions.
  It adds call, member-call, static-receiver, type-position, base-type, type-argument and
  attribute references. rtok's C# query has definitions only, so callers, impact and the truth
  corpus would see nothing in this repository. The golden fixture grew to cover each construct.
- `Rtok.Plugin.Sdk`: `IPlugin.CallTool`. Archive ids print through T9's `ArchiveIds.Abbreviate`.
- `eng/RtokPlugins.targets` links the plugin into `rtok` (id `graph`) and imports the
  Pyrlyn.TreeSitter targets for the AOT link.

How it was checked:
- `tests/Rtok.Plugins.Graph.Tests` ports rtok's `graph/*` unit tests, plus:
  - matcher, indexer and watch tests;
  - a Verify snapshot of the five tools;
  - CsCheck properties: a cap is lossless through `expand`; `impact` stays in budget and depth.
- `graph_contract.rs` ported (`GraphContractTests`): the five tools byte for byte through
  `CallTool`. Also checked: a second repo leaves the first intact, an edit is re-read, a deletion
  drops rows.
- The truth corpus `tests/fixtures/graph_truth.json`: 43 symbols of this repository's own C#,
  labelled from a plain-text scan, never from the index. It scores precision 1.000 and recall
  1.000. The floors are def recall 0.9, precision 0.99, ref recall 0.98 and recall 0.92. Other
  lanes move code under the labels, so a label whose file no longer mentions its symbol is
  skipped. A definition in a file no label names is not judged. More than a tenth of the labels
  gone stale fails `Most_labels_are_still_backed_by_the_tree`.
- `Rtok.Benchmarks` `GraphIndexBenchmarks` (3000 files, Release, osx-arm64 M3 Max):
  - `WarmReindex` (nothing changed): mean 36.98 ms, median 35.57 ms. The skips that make it this
    fast: the stat gate, HEAD read in-process instead of a spawned git (53 ms), and no idf rebuild
    or missing-file sweep when nothing changed.
  - `EditOneFile` (rewrite one file, then `RunChanged`, the write included): mean 22.24 ms,
    median 19.99 ms. This misses rtok's T35.4 bar of 10 ms.
- `Rtok.AotTests` `GraphTests`: Rust and C# files indexed and answered through the statically
  linked grammars in the NativeAOT binary, and `impact` reading co-change through the libgit2
  that ships beside it.
- Fuzz: `outline` in `tests/Rtok.Fuzz` is wired, with one seed per grammar (the golden
  fixtures). The first byte picks the grammar. It checks that every tag's node, name and line
  ranges lie inside the source, with the name inside its node. `Rtok.Fuzz.Tests` replays it over
  the corpus, random bytes and mutated seeds.
- N21 AOT publish of `rtok` with LibGit2Sharp (`libgit2-5853918` beside the binary, no new
  trim or AOT warnings):
  - osx-arm64: proven here. `rtok` publishes, and `Rtok.AotTests` passes 10/10.
  - osx-x64: proven here. `rtok` publishes, and `Rtok.AotTests` passes 10/10 under Rosetta.
  - linux-x64, linux-arm64 and win-x64: CI only, not yet run. The podman machine here could not
    start (`krunkit` is missing), and the coordinator moved the Linux proof to `ci.yml`.
- `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test`, `dotnet csharpier check .`
  and `markdownlint-cli2` are green.

Left for later tasks:
- Deferred to T15/T329.36:
  - `tally` (the per-call savings rows);
  - `rtok mcp`, the stdio process the contract runs through;
  - the production host that starts `GraphWatch`.
- Deferred to T25:
  - `dead` (nameless `callers`), `affected` (path-only `impact`), `project`, scope;
  - `graph_diff` and `graph_export`.
- Deferred to T26: the rank refresh and the repo map.
- Deferred to T24.1: the LSP and text backends.
- Deferred to T19: the outline guard on `read`.
- Not re-created: rtok's `WalkMemo` (no caller before T25), and rtok's T368 repo-wide rank gate.
  C# imports name namespaces, not files, so the gate's premise does not hold here. The unit
  tests cover ranking.
- Inherited from rtok: the `map` render prints the first word of the line (`pub Point 2`), and
  `outline` `kind` skips words starting with `pub`.
- Follow-up: co-change walks commits topologically, then by time, as `git log` does. Time alone
  put a parent before its child when both shared a second, so
  `Commit_files_match_git_log_name_only_relative` failed on fast machines.
