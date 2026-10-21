---
status: todo
priority: P3
complexity: 2
readiness: 0%
agent:
---

# T51. Experimental extension points: decide and gate

rtok carried flag-gated experiments: embeddings beside FTS5, a WASM plugin host, and tiered L0/L1/L2 context loading. The port must decide for each.

Done when for each experiment there is a recorded decision in `plan/README.md`: port it behind a flag (WASM via WACS, a pure C# interpreter), defer it, or drop it. A port is tested.

Spec: catalog A33 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T29.0, T29.1, T29.2, T32.0, T32.1, T32.2, T33.0, T33.1, T33.2.

Check: `plan/README.md` has one decision line per experiment.
