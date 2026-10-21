---
status: in progress
priority: P0
complexity: 4
readiness: 80%
agent: Claude Code / claude-opus-5-5
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

Plan:
1. `src/Rtok.Core`: `Catalogue` (id, default-on), `PluginRegistry` (catalogue order, then external plugins; enabled from `plugins.<id>.enabled` read through `IHost.ConfigJson`, else the manifest default), `TokenEstimator` (rates from `[estimator]`), `Dispatcher` (rtok's pre_tool, post_tool with `cap_budget`, injections with `inject::apply`, each plugin call in its own try/catch).
2. `src/Rtok.Plugins.Measure`: the measure plugin's manifest and page; linked by `eng/RtokPlugins.targets`, which picks built-in plugins with `-p:RtokPlugins=<ids>` (default `all`).
3. `rtok plugins`: id, kind (`builtin` for a catalogue id, `external` otherwise), enabled, surfaces; `--json`. Until T7 the CLI passes no config, so every plugin shows its default.
4. `tests/Rtok.Core.Tests`: registry, estimator and dispatcher tests, plus the architecture test over every `ProjectReference`.
5. Smoke: an AOT publish with `-p:RtokPlugins=measure` lists only measure; the sample-plugin build adds `-p:RtokPlugins=none` and lists only the sample.

