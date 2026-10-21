# T5. Done

`packages/Rtok.Plugin.Sdk` 0.1.0 holds the plugin contract, ported from rtok-plugin-sdk. It depends on the BCL and `System.Text.Json` only and is `IsAotCompatible`:

- `IPlugin`. `Manifest` and `DashboardPage` are required. `OnPreTool`, `OnPostTool`, `OnSessionStart`, `OnPromptSubmit`, `OnPreCompact`, `OnSubagentStart`, `McpTools` and `ProxyFilter` default to no-ops.
- Contract types: `Surface` flags with `SurfaceNames`, `Manifest`, `Measurement`, `PreToolDecision.Deny`/`Rewrite`, `Injection`, `ToolDef`, `DashboardPage`.
- Event views `PreToolUse` through `SubagentStart`, carrying the hook JSON as `JsonElement`.
- Host capabilities:
  - `IHost`, `IArchive`, `INotes`, `IReadCache`, `ILedger` and `ISymbols`, combined in `ICapabilities`. Failures are thrown and the host isolates each plugin.
  - `Ctx` carries the agent id and offers `PluginConfig<T>`/`Config<T>` through a `JsonTypeInfo<T>`. A missing or broken section gives `new T()`.
  - Optional members answer empty by default.
- The wire view: `WireRequest` over `IToolResults`, with `ToolResultRef`, `BlobRef` and `SkillRef` exposing `JsonSlot`s that write back into the request body.
- `Rtok.Plugin.Sdk.Testing.MemoryHost`:
  - keeps measurements, logs, content-addressed archives (`mem` + 16 hex digits of SHA-256) and first-writer-wins archive decisions;
  - estimates with fixed per-class rates.
- Not ported: rtok's `task_prefix` helper. Its only user is the tasks plugin (T41).

Static registration (N12), in `eng/RtokPlugins.targets`:
- `RtokPlugin` items (project + `Type`) become project references of `rtok`.
- A generated `LinkedPlugins.Create()` returns the plugins. No assembly is loaded at run time.
- An out-of-tree plugin builds rtok with `-p:RtokPluginsFile=<props>`.

`rtok plugins` prints rtok's table: id, enabled (from `DefaultOn` until T6 adds config), surfaces. `tests/Rtok.SamplePlugin` is rtok's `shrink` example as an out-of-tree plugin, with its `rtok-plugins.props`.

Checked:
- `dotnet pack packages/Rtok.Plugin.Sdk/src -c Release -warnaserror` produces `Rtok.Plugin.Sdk.0.1.0.nupkg` with zero warnings.
- `dotnet publish src/Rtok.Cli -c Release -r osx-arm64 -p:RtokPluginsFile=tests/Rtok.SamplePlugin/rtok-plugins.props` gives a native Mach-O `rtok`; `rtok plugins` prints `shrink    on        hook`.
- The package tests port rtok's MemoryHost tests: dotted config with default fallback, stable archive ids, distinct bodies get distinct ids, frozen decisions. They also cover default events, surface names, the ledger event filter and `JsonSlot` write-back. 11 pass.
- Smoke lane: `PublishedApps` gained `rtok+sample` (published into `artifacts/smoke-plugins`), and `PluginTests` asserts its `rtok plugins` output byte for byte.
- After rebasing on `main`: Release `-warnaserror` build green, `dotnet test` 328 passed and 1 skipped (Rtok.Smoke included), CSharpier clean.
