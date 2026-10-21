---
status: in progress
priority: P0
complexity: 3
readiness: 90%
agent: Claude Code / claude-opus-5-5
---

# T5. Package Rtok.Plugin.Sdk: the plugin contract

Every token-reduction method is a plugin behind one contract (rtok D1, D25). The contract is the one public package third parties build against.

Done when:
- `packages/Rtok.Plugin.Sdk` holds the contract, with no dependencies beyond the BCL and `System.Text.Json`:
  - an `IPlugin` interface with default no-op event methods;
  - `Manifest`, `Surface`, `Measurement`, `PreToolDecision`, `Injection`, `ToolDef`, `DashboardPage`;
  - event views over the hook JSON;
  - the host capability interfaces (Host, Archive, Notes, ReadCache, Ledger, Symbols);
  - the wire view over a proxied request's tool results;
  - test helpers.
- It is AOT-compatible, documented, and versioned on its own.
- Registration is static, with no runtime assembly loading (N12).
- A sample out-of-tree plugin in `tests/` builds its own `rtok` against the package.

Spec: catalog A1 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T0.4, T0.5, T23.0, T23.1, T23.2, T23.3, T23.5.

Check: The sample plugin builds an AOT `rtok` that lists it in `rtok plugins`, and `dotnet pack` produces the package with zero warnings.

Plan:
1. `eng/new-package.sh Rtok.Plugin.Sdk`; port rtok-plugin-sdk (at `ef091589`): `IPlugin` (required `Manifest`, `DashboardPage`; default no-op `On*` events, `McpTools`, `ProxyFilter`), `Surface` flags, `Manifest`, `Measurement`, `PreToolDecision`, `Injection`, `ToolDef`, `DashboardPage`, event views (`PreToolUse` … `SubagentStart`, JSON as `JsonElement`).
2. Host capabilities: `IHost`, `IArchive`, `INotes`, `IReadCache`, `ILedger`, `ISymbols`, `ICapabilities`, `Ctx` (agent id, `PluginConfig<T>` through a `JsonTypeInfo<T>`), `TextClass`; wire view `WireRequest`, `IToolResults`, `ToolResultRef`, `BlobRef`, `SkillRef` over `JsonNode` slots.
3. Test helpers: `Rtok.Plugin.Sdk.Testing.MemoryHost`; package tests port rtok's.
4. Static registration (N12): `RtokPlugin` MSBuild items (project + `Type`) turn into project references and a generated `LinkedPlugins` factory in `rtok`; out-of-tree plugins pass `-p:RtokPluginsFile=<props>`. A minimal `rtok plugins` lists the linked plugins (T6 adds config, kind, `--json`).
5. `tests/Rtok.SamplePlugin` (rtok's `shrink` example) with its `rtok-plugins.props`; a smoke test publishes an AOT `rtok` with it and checks `rtok plugins`.
6. Verify: `dotnet pack packages/Rtok.Plugin.Sdk -warnaserror`, Release build, tests, CSharpier.
