---
status: todo
priority: P0
complexity: 3
readiness: 0%
agent:
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
