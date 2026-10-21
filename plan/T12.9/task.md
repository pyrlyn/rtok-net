---
status: todo
priority: P0
complexity: 3
readiness: 0%
agent:
---

# T12.9. Package Pyrlyn.Config.Layered: TOML layers with provenance

Microsoft.Extensions.Configuration binds AOT-safely but loses per-key provenance, which `config show --sources` needs.

Done when:
- The package merges ordered layers into one tree: defaults, files, `.env`, env and flags.
- It records the source of every key: file:line, env var or flag.
- It edits a TOML file in place, comment-preserving, through Tomlyn's syntax tree.

Spec: catalog A30, catalog B19 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: no rtok task; new for .NET.

Check: Tests show provenance per key across all layers, and an in-place edit leaves the rest of a commented file byte-identical.
