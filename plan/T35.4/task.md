---
status: in progress
priority: P2
complexity: 3
readiness: 0%
agent: Claude Code / claude-opus-5-5
---

# T35.4. Agent hosts: proxy base-URL module

`rtok agents install --proxy` points a host at rtok's proxy (T27); uninstall clears the base URL only while it still holds rtok's URL.

Done when:
- The proxy module writes and removes the base URL for every host that has one: Claude (`ANTHROPIC_BASE_URL`), Codex (`model_provider` base_url, keeping the replaced provider as `# rtok: was "openai"` and restoring it), OpenCode, Aider (`openai-api-base` line edit).
- Ownership of a URL comes from parsing it: only an http(s) loopback host counts.

Tests:
- `Rtok.Agents.Hosts.Tests` unit: uninstall clears a base URL only when it parses as rtok's http(s) loopback URL, and Codex gets its provider back from `# rtok: was "openai"`.

Spec: catalog A19, catalog B3 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: the proxy part of T35's list.

Check: The T35 install-matrix test covers `--proxy` install and uninstall, and a foreign base URL survives uninstall.

Plan:
1. Spec from rtok's proxy module per host (Claude `env.ANTHROPIC_BASE_URL`, Codex `model_provider`, OpenCode, Aider), its loopback ownership and its report lines.
2. Move `ProxyUrls` from `Rtok.Proxy` to `Rtok.Config`, so the hosts library reads the one URL rule without the Kestrel proxy.
3. A proxy module in `Rtok.Agents.Hosts` beside hooks and MCP: per-host writers through Pyrlyn.Jsonc, the TOML splice editor and an Aider line edit. Only an http(s) loopback URL counts as rtok's. Wire `--proxy` (`setup.proxy`) into install, update and uninstall, `agents list|info`, and `--replace` retargeting a legacy proxy URL.
4. Tests: loopback ownership units, the Codex `# rtok: was "openai"` restore, and install-matrix `--proxy` install and uninstall with a foreign URL kept.
5. Gates, `done.md`, merge.
