---
status: todo
priority: P2
complexity: 4
readiness: 0%
agent:
---

# T32. Doctor: checks, duplicates and `--fix`

`rtok doctor` finds broken and duplicate hooks and MCP entries across hosts and fixes them minimally.

Done when:
- Read-only `doctor` behaves as in the catalog: hooks, MCP servers with description tokens, the base-URL chain, the instruction and skills audits, overlaps, broken hooks resolved without running them, and duplicates with a keep recommendation.
- `--fix` makes minimal JSONC/TOML edits through Pyrlyn.Jsonc and Tomlyn, with the plan-token flow shared with web and TUI.
- rtok's own MCP entry is never removed (D33).
- An exposure check covers wide binds, missing tokens and loose file modes (rtok I-S1).

Spec: catalog A18, catalog B18 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T1.4, T7.2, T42, T59.7, T61.3, T123, T171, T173, T331, T331.1, T331.2, T331.3, T331.4, T331.5, T331.6, T331.7, T331.8, T331.9, T331.10, T331.11, T331.12, T388, T392, T393, T576; open I-S1 ([open work](../../docs/port/open-work.md)).

Check: Property tests show `--fix` leaves bytes outside the fixed entries unchanged, and goldens match for each problem kind.
