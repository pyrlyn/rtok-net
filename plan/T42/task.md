---
status: todo
priority: P3
complexity: 3
readiness: 0%
agent:
---

# T42. docs plugin: offline dependency docs

rtok answered dependency-doc questions offline from lockfile-pinned versions. The port keeps the tools and local docs, and adds .NET lockfiles.

Done when:
- MCP `docs_resolve`, `docs_query` and `docs_get` work over an FTS table.
- `rtok docs fetch` works.
- Local docs and `llms.txt` work.
- Lockfile resolvers cover the ecosystems rtok supported plus `packages.lock.json`.
- It is off by default.

Spec: catalog A23 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T455.

Check: Ingest and query fixtures pass, and an unchanged re-ingest keeps doc ids.
