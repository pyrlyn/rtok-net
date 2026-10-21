---
status: todo
priority: P2
complexity: 2
readiness: 0%
agent:
---

# T21. inject plugin, prompt modes and prose compression

Injected context must stay inside its budget and be byte-stable, so it never breaks the prompt cache (rtok D5, D7).

Done when:
- Injections are emitted by priority up to `inject_budget_tokens`, with `dropped:` lines and Measurements.
- Mode files (`modes/*.md`, copied from rtok) are read capped and are byte-stable.
- `compress_prose` keeps rtok's glue and code-fence rules.

Spec: catalog A7, catalog B16 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T2.4, T7.1, T188, T604, T606, T608.

Check: Repeated runs give byte-identical output, and the fence and glue cases from the catalog pass.
