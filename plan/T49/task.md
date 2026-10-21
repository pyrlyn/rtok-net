---
status: todo
priority: P1
complexity: 3
readiness: 0%
agent:
---

# T49. Security: threat model and audit findings

rtok's 2026-10-14 audit found eight issues (S1-S8). Each one goes into the matching task, and this task makes sure none is lost and writes the threat model (rtok I-S5).

Done when:
- `docs/security.md` describes the threat model per surface: hooks, stdio MCP, HTTP MCP, proxy, dashboard.
- Each finding has a test in its task:
  - S1, S8 in T15;
  - S2 in T44;
  - S3 in T8;
  - S4 in T19;
  - S5, S6 in T27 and T44;
  - S7 in T14.

Tests:
- `Rtok.Core.Tests` arch: `docs/security.md` has a threat-model section for each surface: hooks, stdio MCP, HTTP MCP, proxy and dashboard.
- `Rtok.Core.Tests` arch: each of S1-S8 in `docs/security.md` names a test, and that test method exists under `tests/` or `packages/*/tests`.
- `Rtok.Mcp.Tests` integration: S1, `rtok mcp --http` in `/` or `$HOME` refuses to start unless `allow_paths` names a project.
- `Rtok.Mcp.Tests` integration: S8, `rtok mcp --http` refuses a non-loopback bind without TLS in front.
- `Rtok.Web.Tests` integration: S2, `/ws` without the per-run token is refused, and a non-loopback `web.host` without it refuses to start.
- `Rtok.Store.Tests` integration: S3, on Unix the `~/.rtok` store, archive and log directories are created 0700 and their files 0600 under a permissive umask.
- `Rtok.Plugins.Read.Tests` integration: S4, `read` and `search` refuse `.env`, `.git/config`, `*.pem` and `id_*` by the default `plugins.read.deny_globs`, and over HTTP even when config clears them.
- `Rtok.Proxy.Tests` integration: S5, `/live` and `/health` refuse a non-loopback client without the token.
- `Rtok.Proxy.Tests` integration: S6, a `Host` header containing `,` answers 400.
- `Rtok.Web.Tests` integration: S5 and S6 on the dashboard, an unauthenticated `/health` off loopback is refused and a `Host` containing `,` answers 400.
- `Rtok.Hooks.Tests` integration: S7, the resident socket is created inside a 0700 directory, and on Windows the pipe's ACL is owner-only.

Ports: rtok open S1, S2, S3, S4, S5, S6, S7, S8, I-S5 ([open work](../../docs/port/open-work.md)).

Check: `docs/security.md` exists and each of S1-S8 maps to a named passing test.
