---
status: todo
priority: P0
complexity: 4
readiness: 0%
agent:
---

# T6. Core runtime: registry, dispatcher, estimator, measurements

The core wires plugins to surfaces and is the only path by which a saving enters the store (rtok D3).

Done when:
- The plugin catalogue is a single list of id and default-on pairs, asserted against the registry.
- Plugins are chosen at build time with MSBuild properties (a build with only `measure` works) and turned on or off at runtime with `[plugins.<id>] enabled`.
- The dispatcher applies rtok's merge rules:
  - the first deny wins;
  - the last rewrite wins;
  - additional context is concatenated under the inject budget;
  - injections are sorted by priority up to the budget.
- Each plugin call has its own try/catch: a throwing plugin loses only its own output.
- The token estimator uses chars per token by class: Code 3.5, Prose 4.2, Json 3.0, Cjk 1.0.
- `rtok plugins` prints id, kind, enabled and surfaces (`--json` too).
- An architecture test fails on any upward project reference.

Spec: catalog A1 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T0.1, T0.8, T23.4.

Check: Dispatcher tests cover every merge rule and a throwing plugin, the architecture test passes, and the measure-only build publishes with AOT.
