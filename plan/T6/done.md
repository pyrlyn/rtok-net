# T6. Done

Commit `T6: Core runtime: registry, dispatcher, estimator, measurements` on `main`.

What landed:
- `src/Rtok.Core`, the runtime between plugins and surfaces:
  - `Catalogue` is the one list of built-in plugin ids and their defaults, in rtok's dispatch order.
  - `PluginRegistry` orders built-in plugins by the catalogue and external ones after them in link order. Kind is `builtin` or `external`. Enabled comes from `plugins.<id>.enabled`, read through the same dotted-path seam as `IHost.ConfigJson`; otherwise the manifest default applies.
  - `TokenEstimator` and `EstimatorRates` count Unicode scalars over the per-class rate (Code 3.5, Prose 4.2, Json 3.0, Cjk 1.0; floor 0.1) and round up. The `[estimator]` section overrides each rate.
  - `InjectBudget` ports `inject::apply`, `cap_budget` and `fit_budget`, including the `dropped:` markers, the archive behind `rtok expand`, the 64-digit placeholder and the `inject` Measurement row. The budget is `plugins.inject.budget_tokens` (default 800).
  - `Dispatcher` handles PreTool (first deny wins, last rewrite wins), PostTool (context joined, then capped), SessionStart, PromptSubmit and SubagentStart (budgeted injections, no Measurement row when nothing is offered), and PreCompact. Each plugin call is guarded on its own, and a throw is logged as `error plugin/<id>`.
- `src/Rtok.Plugins.Measure` holds the measure plugin's manifest (`cli,proxy`, on by default) and its page.
- `eng/RtokPlugins.targets` links the built-in plugins, selected with `RtokPlugins`:
  - unset or `all` links every plugin;
  - `measure`, or a quoted `"measure,cmd"`, links those;
  - `none` links none.
  - `RtokPluginsFile` still adds out-of-tree plugins on top.
- `rtok plugins` prints the columns id, kind, enabled and surfaces. `--json` adds each plugin's page (title, summary, saves_tokens, fields) and writes it with `Utf8JsonWriter`, so it is AOT-clean.
- `tests/Rtok.Core.Tests` has 42 tests:
  - estimator fixtures from rtok;
  - a registry test against the catalogue, plus order and config overrides;
  - every dispatcher merge rule and a throwing plugin on every event;
  - rtok's budget tests;
  - `ArchitectureTests`.
- `ArchitectureTests` gives every production project a layer: `Pyrlyn.*`, then `Rtok.*.Sdk`, then foundation, core, app and surfaces. It fails on a reference to a higher layer, on a project with no layer, and on a `Pyrlyn.*` package that references something other than `Pyrlyn.*`.
- Smoke lane:
  - `rtok+measure` (`-p:RtokPlugins=measure`) is published with AOT and lists only measure.
  - `rtok+sample` now adds `-p:RtokPlugins=none` and lists only `shrink    external`.

Decisions to know:
- Rust rtok's table has no `kind` column, although the task names one. Here `kind` is `builtin` for a catalogue id and `external` for anything else.
- The registry and dispatcher read config only through the `ConfigJson` path seam. Until T7 loads `.rtok.toml`, `rtok plugins` passes no config, so every plugin shows its default. T7 needs to hand its section lookup to `PluginRegistry`.
- The dispatcher returns typed results (decision, context string). The hook wire JSON (`hookSpecificOutput`) belongs to the hooks surface task.
- Injections are always budgeted in core. rtok dropped them in a build without the `inject` feature; here the budget runs whether or not the inject plugin is linked.

Checked:
- `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test` and `dotnet csharpier check .` are green.
- `dotnet msbuild src/Rtok.Cli -getItem:RtokPlugin` links measure for unset, `all`, `measure`, `"cmd,measure"` and `cmd%3Bmeasure`, and links nothing for `cmd` or `none`.
