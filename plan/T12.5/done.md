# T12.5. Done

Package `Pyrlyn.TreeSitter` 0.1.0: tree-sitter bindings, 14 grammars linked statically per
RID, a port of tree-sitter-tags and rtok's tags queries as embedded data.

What was done:
- Native sources: the crates.io tarballs that rtok's `Cargo.lock` pins (tree-sitter 0.25.10
  and the 13 grammars; typescript gives typescript and tsx), by version and sha256 in
  `native/sources.txt`. Only their C files are compiled; no Rust code. No new NuGet dependency.
- `native/build-native.sh [--rid]`: downloads, checks the sha256, compiles with the host C
  compiler into `libpyrlyn_tree_sitter.a` (NativeAOT) and a shared library (JIT), cached per
  machine by a key over the sources and flags. Apple clang builds both macOS RIDs
  (`-arch`, `-mmacosx-version-min=12.0`); other cross builds need `CC`/`AR`.
- `build/Pyrlyn.TreeSitter.targets`: runs the script when the RID's library is missing; JIT
  gets the shared library next to the output, NativeAOT gets `DirectPInvoke` + `NativeLibrary`.
  Packing takes all six RIDs from `PyrlynTreeSitterPackNativeRoot`
  (`PyrlynTreeSitterRequireAllRids=true` makes a missing one an error).
- `[LibraryImport]` bindings: `Language` (14 statics), `Parser`, `Tree`, `Node`
  (kinds, fields, positions, children, descendants), `Query`, `QueryCursor`, `QueryMatch`.
  The text predicates the Rust binding adds on top of the C API (`eq?`, `match?`, `any-of?`
  with their `not-`/`any-` forms) are evaluated per match; `set!`, `is?`/`is-not?` and other
  predicates are exposed per pattern. Query errors map to `QueryException` with kind, row and
  detail as the Rust binding reports them.
- `Tags`: `TagsConfiguration` and `TagsContext` port tree-sitter-tags 0.25.10: locals scopes
  with `local.scope-inherits`, `#is-not? local`, docs with `select-adjacent!`/`strip!`, one tag
  per node with the lower pattern winning, line ranges cut at 180 bytes on a character
  boundary, UTF-16 columns, `HasError`, cancellation. Deviation: names claimed by `@ignore` are
  never emitted, also in the final drain (README).
- `Grammar.All`/`Grammar.Find`: the 14 grammars with rtok's ids and extensions; each tags query
  is upstream `tags.scm` (+ `locals.scm`) then rtok's extras, copied verbatim from rtok's
  `outline.rs` (ef091589) into `Queries/<grammar>/`.
- CI: `.github/workflows/tree-sitter.yml` builds the library, runs the package tests and the
  NativeAOT lane per RID (linux-x64, linux-arm64, osx-arm64, osx-x64, win-x64, win-arm64) and
  uploads the library; `packages.yml` runs it for `Pyrlyn.TreeSitter/v*` tags and packs every RID.

How it was checked (osx-arm64, M3 Max):
- 66 package tests: one fixture per language with a stored golden of definitions,
  references, ranges, UTF-16 columns, line ranges and docs; predicates, query errors, locals,
  docs, line ranges, ignore, cancellation, ABI of every grammar.
- The goldens' definitions and imports for rust, typescript, tsx, javascript, python, go, dart
  and c match the real rtok's `outline` on the same fixtures. java, kotlin, swift, c#, ruby and
  php are not wired into rtok's outline at ef091589, so their goldens come from this port only.
- NativeAOT: `tests/Rtok.AotTests` links the static library and checks every fixture against
  its golden; the published binary has no dynamic tree-sitter dependency (`otool -L`).
- `build-native.sh --rid osx-x64` gives x86_64 objects. Linux and Windows builds were not run
  here; the CI workflow covers them.
- `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test` and `dotnet csharpier check .`
  are green.

Commit: `T12.5: Package Pyrlyn.TreeSitter: bindings, static grammars, tags` on branch
`T12.5-tree-sitter`, fast-forwarded into `main`.
