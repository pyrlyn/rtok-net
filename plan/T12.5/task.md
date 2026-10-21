---
status: todo
priority: P1
complexity: 5
readiness: 0%
agent:
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
