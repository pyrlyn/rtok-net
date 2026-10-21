---
status: in progress
priority: P0
complexity: 3
readiness: 100%
agent: Claude Code / claude-opus-5-5
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

Plan:
1. `eng/new-package.sh Pyrlyn.Config.Layered`; Tomlyn 2.10.1 in `Directory.Packages.props`.
2. `LayeredConfig`: ordered layers (defaults, TOML files, `.env`, env, flags) merged leaf by leaf into one tree of TOML values; each leaf keeps a `ConfigSource` (layer name, file:line, env var or flag).
3. Env and `.env` keys map through the canonical env name of each known dotted key (`proxy.openai_upstream` → `PREFIX_PROXY_OPENAI_UPSTREAM`), arrays split on `,`.
4. `TomlEditor.Set`: in-place, comment-preserving edit through Tomlyn's syntax tree; a commented `# key = val` line is uncommented in place, string payloads are never touched.
5. Tests: provenance per key across all layers; an edit leaves the rest of a commented file byte-identical.
