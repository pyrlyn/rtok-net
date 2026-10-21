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

Spec: catalog A11, catalog B15 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T30.0, T30.1, T30.2.

Check: Tests with a fake LSP server cover answers, the timeout fallback header and shutdown at EOF.
