---
status: todo
priority: P2
complexity: 4
readiness: 0%
agent:
---

# T44. Web dashboard: Kestrel, `/ws`, embedded React SPA

The dashboard is the operator's main view; the SPA is reused from rtok (N13).

Done when:
- `rtok web [--port]` serves `/health`, `/ws` and the embedded SPA: hashed, precompressed, SPA fallback, CSP.
- The `/ws` messages from the catalog work, with JSON Schema exported from the STJ context and TypeScript types generated from it.
- Security: Origin and Host checks, plus a per-run random token in the URL, required on `/ws` (rtok S2). A non-loopback `web.host` without the token is refused.
- The brand restyle work from rtok continues on the same SPA (rtok T414).

Tests:
- `Rtok.Web.Tests` integration: `rtok web --port 0` binds `[web] host`, prints its URL with the token to stderr, and `/health` answers 200 while a snapshot build is blocked.
- `Rtok.Web.Tests` integration: hashed assets are served precompressed (`br`, `gzip`) with long-lived cache headers, an unknown route falls back to `index.html`, every HTML response carries the CSP header, and `RTOK_WEB_DIST` replaces the embedded SPA.
- `Rtok.Web.Tests` CsCheck: asset paths are percent-decoded once per segment, malformed escapes, non-UTF-8 bytes and encoded separators get 400, and no path resolves outside the SPA root.
- `Rtok.Web.Tests` integration: a mismatched Origin gets 403 and a Host that is not an IP or `localhost` is refused, on plain HTTP and on `/ws`.
- `Rtok.Web.Tests` integration: `/ws` without the token, or with another run's token, is refused, and two runs print different tokens.
- `Rtok.Web.Tests` integration: a non-loopback `web.host` without the token is refused at startup with a clear message.
- `Rtok.Web.Tests` integration: over `ClientWebSocket` the first frame is a snapshot with all four windows and server totals, and every request id is echoed in its reply.
- `Rtok.Web.Tests` integration: a plugin toggle over `/ws` changes only that plugin's key in `config.toml`, every other byte unchanged, and the next snapshot shows it.
- `Rtok.Web.Tests` integration: expand over `/ws` returns the archived body byte for byte.
- `Rtok.Web.Tests` integration: doctor plan returns a token and a diff, apply with that token writes, and apply after the files changed returns a fresh plan and writes nothing.
- `Rtok.Web.Tests` integration: graph drill-down, calls frames and link/unlink answer over `/ws`, and a path inside a message is never used to open a file.
- `Rtok.Web.Tests` unit: a CallBatch carries every ended call id, so a call past the display cap is never shown as interrupted.
- `Rtok.Web.Tests` Verify: `ws.schema.json` exported from the STJ context and the generated `snapshot.gen.ts` match the checked-in files.
- `Rtok.Web.Tests` integration: the embedded SPA's CSS defines the brand `--pyr-*` tokens, and the bundle holds no copied `brand/` files.
- `Rtok.Smoke` smoke: the published `rtok web --port 0` serves `/health`, the SPA index with its CSP, and a `/ws` snapshot with the token.

Spec: catalog A24, catalog B20 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T19.1, T19.2, T19.4, T21.3, T36.14, T36.15, T60.3, T60.4, T60.5, T60.6, T60.9, T63.1, T193, T206, T226, T227, T228, T229, T231, T232, T260, T310, T310.1, T310.2, T310.3, T310.4, T310.5, T310.6, T310.7, T310.8, T310.9, T310.10, T310.11, T329.12, T329.13, T329.14, T329.15, T329.20, T329.21, T329.22, T329.23, T329.26, T329.27, T329.28, T329.32, T329.33, T329.35, T329.36, T329.43, T329.50, T329.51, T329.52, T329.53, T346, T407, T414.1, T414.2, T414.3, T414.4, T414.5, T414.6, T414.8, T414.9, T414.10, T414.11, T414.12, T414.13, T414.14, T414.15, T414.16, T414.17, T436.1, T483, T484, T573, T574, T619, T623.1, T623.2, T637, T639, T641; open T414, S2 ([open work](../../docs/port/open-work.md)).

Check: Web e2e tests over `ClientWebSocket` cover snapshot, toggle, expand and doctor plan/apply, and `/ws` without the token is refused.
