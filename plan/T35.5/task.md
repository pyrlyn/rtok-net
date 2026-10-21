---
status: todo
priority: P1
complexity: 4
readiness: 0%
agent:
---

# T35.5. Agent hosts: plugin link, skills, plugin versions, `agents outdated`

Hosts load rtok's plugin trees (T36); install links or installs them, update keeps them current, and one version scheme says what is outdated (rtok T279).

Done when:
- The plugin module links or installs T36's trees per host (Claude and Codex through their marketplaces, the rest by link), with up-to-date, stale (relinked) and foreign (never touched) states.
- Skills from `skills/` are installed into host skill roots with the `rtok-` prefix; uninstall removes only rtok's directories.
- One plugin version scheme for every install source: a version file and a receipt, a SemVer compare ignoring build metadata, and `agents outdated [--json] --exit-code 10`.
- `agents update` updates or reinstalls plugins and skips up-to-date ones.

Tests:
- `Rtok.Agents.Hosts.Tests` unit: a linked plugin is up-to-date, stale (relinked) or foreign (never touched), and a skill copy newer than its `.rtok-owned` marker is left on uninstall.
- `Rtok.Agents.Hosts.Tests` unit: `agents update` runs the Claude and Codex marketplace commands through a fake ProgressRunner, reports "already current" when the cache bytes do not change, and falls back to uninstall + install on failure.
- `Rtok.Agents.Hosts.Tests` CsCheck: the plugin version file and receipt round-trip, and the compare follows SemVer precedence ignoring build metadata: equal skips, newer updates, lower warns.
- `Rtok.Cli.Tests` unit: `rtok agents outdated --exit-code 10` exits 10 when a plugin is behind and 0 when all are current.

Spec: catalog A19, catalog B3 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: the plugin and skill part of T35's list; open T279 ([open work](../../docs/port/open-work.md)).

Check: The T35 install-matrix test covers the plugin module, and `agents outdated` tests cover equal, newer, older and legacy versions with exit code 10.
