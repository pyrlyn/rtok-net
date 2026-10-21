---
status: todo
priority: P3
complexity: 3
readiness: 0%
agent:
---

# T24.1. graph backends: LSP and text

Some projects get better answers from a language server; some have no grammar at all.

Done when these backends answer under the same tool names, with fallback to tags per call:
- `lsp`: rust-analyzer, dart, and the rest from the catalog, with deadlines and child shutdown;
- `text`: in-process regex, no child process;
- `auto`;
- `backend_by_language`.

Tests:
- `Rtok.Plugins.Graph.Tests` integration: with `backend="lsp"` and a fake LSP server on stdio, `symbol`, `callers` and `outline` answer under the same tool names as tags.
- `Rtok.Plugins.Graph.Tests` integration: a fake server that stays silent past `lsp_timeout_ms` makes the call answer from tags with the header `(tags; lsp: <reason>)` and write an `lsp_fallback` row.
- `Rtok.Plugins.Graph.Tests` integration: a missing server binary falls back to tags per call with the same header, and the next call tries lsp again.
- `Rtok.Plugins.Graph.Tests` integration: at EOF the LSP child receives shutdown and exit and no child process is left running.
- `Rtok.Plugins.Graph.Tests` unit: didChange versions increase per document across edits.
- `Rtok.Plugins.Graph.Tests` unit: server selection picks rust-analyzer for a Cargo.toml root, `dart language-server` for a pubspec.yaml root, and each other server in the catalog for its marker file.
- `Rtok.Plugins.Graph.Tests` unit: `backend="text"` answers `symbol` and `callers` from in-process regex with definition keywords, offers no dead-code report, and the ProgressRunner substitute receives no call.
- `Rtok.Plugins.Graph.Tests` unit: `auto` uses lsp when the root's server is available and tags otherwise.
- `Rtok.Plugins.Graph.Tests` unit: `backend_by_language` routes each language of a mixed fixture to its configured backend.
- `Rtok.Plugins.Graph.Tests` unit: each answer records a Measurement of kind `lsp.*`, `tags.*` or `text.*` for the backend that served it.

Spec: catalog A11, catalog B15 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T30.0, T30.1, T30.2.

Fuzz: `lsp-frame` in `tests/Rtok.Fuzz`: LSP frames from a server over any bytes decode or refuse, and the `text` backend never throws on any source.

Check: Tests with a fake LSP server cover answers, the timeout fallback header and shutdown at EOF.
