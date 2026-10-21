---
status: in progress
priority: P1
complexity: 5
readiness: 90%
agent: Claude Code / claude-opus-5-5
---

# T12.5. Package Pyrlyn.TreeSitter: bindings, static grammars, tags

`read map/signatures`, outlines and the graph index all parse code with tree-sitter.

Done when:
- `[LibraryImport]` bindings cover the tree-sitter C API: parse, query, and the tags extraction rtok uses.
- The grammars are built from C sources in CI, per RID, and linked statically (`DirectPInvoke` + `NativeLibrary`). The languages are rust, javascript, typescript, python, c, go, dart, java, kotlin, swift, c#, ruby and php.
- The tags queries from rtok are bundled as data.

Spec: catalog A5, catalog A11 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: no rtok task; new for .NET.

Check: On every RID the package extracts definitions and references from one fixture file per language, matching the stored goldens.

## Plan

1. `eng/new-package.sh Pyrlyn.TreeSitter`. Native sources: the exact crates.io tarballs rtok's
   `Cargo.lock` pins (tree-sitter 0.25.10 core and the 13 grammars), by version and sha256 in
   `native/sources.txt`; only their C files and queries are used.
2. `native/build-native.sh`: fetch, verify, compile with the host C compiler into one static
   archive plus one shared library per RID, cached per machine. The project runs it when the
   host RID's library is missing.
3. `[LibraryImport]` bindings: parser, tree, node, tree cursor, query, query cursor; the text,
   property and general predicates the Rust binding evaluates on top of the C API.
4. Port tree-sitter-tags (locals scopes, docs, one tag per node, 180-byte line ranges) and
   bundle rtok's tags and locals queries per language as embedded data.
5. `buildTransitive` targets: `DirectPInvoke` + `NativeLibrary` for AOT, the shared library
   for the JIT. An AOT smoke project runs the fixtures through the published binary.
6. Tests: one fixture per language with stored goldens of definitions and references;
   predicates, locals, line ranges. CI workflow builds and tests per RID.
