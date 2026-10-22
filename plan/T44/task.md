---
status: in progress
priority: P2
complexity: 3
readiness: 75%
agent: Claude Code / claude-opus-5-5
---

# T44. Web dashboard: Kestrel, `/ws`, Blazor WebAssembly shell

The dashboard is the operator's main view; it is a Blazor WebAssembly app served by the AOT `rtok` (N13). This task lays the host, the protocol and the client shell; the pages follow in T44.2.

Done when:
- `rtok web [--port]` serves `/health`, `/ws` and the embedded `Rtok.Web.Client` publish output: fingerprinted, precompressed, fallback to `index.html`, CSP with `'wasm-unsafe-eval'` and `style-src-attr 'unsafe-inline'` (N13) and nothing looser.
- `Rtok.Web.Contracts` holds the `/ws` messages from the catalog and their STJ context; host and client both use it, and the catalog messages work.
- `Rtok.Web.Client` is a standalone Blazor WebAssembly app, trimmed, that references only `Rtok.Web.Contracts` and Radzen.Blazor (N13): `RadzenLayout` shell with sidebar and routing, theme toggle through Radzen's `ThemeService`, a `/ws` client with loading, offline and Reconnect states, and the overview page.
- Radzen: one light and one dark free theme are embedded, the other theme files are left out of the bundle; `Rtok.Web.Contracts` is a trimmer root, since Radzen grids bind columns by property name.
- `Rtok.Web` publishes the client in an MSBuild target before compile and embeds it; the Release `rtok` stays NativeAOT with no new warnings.
- Security: Origin and Host checks, plus a per-run random token in the URL, required on `/ws` (rtok S2). A non-loopback `web.host` without the token is refused.

Tests:
- `Rtok.Web.Tests` integration: `rtok web --port 0` binds `[web] host`, prints its URL with the token to stderr, and `/health` answers 200 while a snapshot build is blocked.
- `Rtok.Web.Tests` integration: fingerprinted `_framework` assets are served precompressed (`br`, `gzip`) with long-lived cache headers, `index.html` is not cached, an unknown route falls back to `index.html`, every HTML response carries the CSP header, and `RTOK_WEB_DIST` replaces the embedded client.
- `Rtok.Web.Tests` CsCheck: asset paths are percent-decoded once per segment, malformed escapes, non-UTF-8 bytes and encoded separators get 400, and no path resolves outside the client root.
- `Rtok.Web.Tests` integration: a mismatched Origin gets 403 and a Host that is not an IP or `localhost` is refused, on plain HTTP and on `/ws`.
- `Rtok.Web.Tests` integration: `/ws` without the token, or with another run's token, is refused, and two runs print different tokens.
- `Rtok.Web.Tests` integration: a non-loopback `web.host` without the token is refused at startup with a clear message.
- `Rtok.Web.Tests` integration: over `ClientWebSocket` the first frame is a snapshot with all four windows and server totals, and every request id is echoed in its reply.
- `Rtok.Web.Tests` integration: a plugin toggle over `/ws` changes only that plugin's key in `config.toml`, every other byte unchanged, and the next snapshot shows it.
- `Rtok.Web.Tests` integration: expand over `/ws` returns the archived body byte for byte.
- `Rtok.Web.Tests` integration: doctor plan returns a token and a diff, apply with that token writes, and apply after the files changed returns a fresh plan and writes nothing.
- `Rtok.Web.Tests` integration: graph drill-down, calls frames and link/unlink answer over `/ws`, and a path inside a message is never used to open a file.
- `Rtok.Web.Tests` unit: a CallBatch carries every ended call id, so a call past the display cap is never shown as interrupted.
- `Rtok.Web.Tests` unit: every `/ws` message type round-trips through the shared STJ context, and the client and host use that one context (no second copy of a message type).
- `Rtok.Web.Client.Tests` bUnit: the shell shows loading, then the overview from a snapshot, then offline with Reconnect when the socket drops.
- `Rtok.Web.Client.Tests` bUnit: the theme toggle switches the Radzen theme and keeps `rtok-theme` in localStorage.
- `Rtok.Web.Tests` integration: the embedded client holds exactly two Radzen theme files, and the CSP has no `style-src` source beyond `'self'` and no `script-src` source beyond `'self' 'wasm-unsafe-eval'`.
- `Rtok.Core.Tests` arch: `Rtok.Web.Client` references only `Rtok.Web.Contracts`, and `Rtok.Web.Contracts` references nothing in `src/`.
- `Rtok.Smoke` smoke: the published `rtok web --port 0` serves `/health`, the client `index.html` with its CSP, `_framework/blazor.webassembly.js`, and a `/ws` snapshot with the token.

Spec: catalog A24, catalog B20 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T19.1, T19.2, T19.4, T21.3, T36.14, T36.15, T60.3, T60.4, T60.5, T60.6, T60.9, T63.1, T193, T206, T226, T227, T228, T229, T231, T232, T260, T310, T310.1, T310.2, T310.3, T310.4, T310.5, T310.6, T310.7, T310.8, T310.9, T310.10, T310.11, T329.12, T329.13, T329.14, T329.15, T329.20, T329.21, T329.22, T329.23, T329.26, T329.27, T329.28, T329.32, T329.33, T329.35, T329.36, T329.43, T329.50, T329.51, T329.52, T329.53, T346, T407, T414.1, T414.2, T414.3, T414.4, T414.5, T414.6, T414.8, T414.9, T414.10, T414.11, T414.12, T414.13, T414.14, T414.15, T414.16, T414.17, T436.1, T483, T484, T573, T574, T619, T623.1, T623.2, T637, T639, T641; open S2 ([open work](../../docs/port/open-work.md)).

Fuzz: `ws-message` in `tests/Rtok.Fuzz`: any `/ws` message is answered or refused, and the Origin and Host checks never throw.

Check: Web e2e tests over `ClientWebSocket` cover snapshot, toggle, expand and doctor plan/apply, and `/ws` without the token is refused.

Plan (same rule as T43: build now; a request whose function is not on `main` yet is answered
"not available in this build yet", and the task stays open until each is wired):
1. `src/Rtok.Web.Contracts`: the snapshot pages move here from `Rtok.Model` (one copy, used by
   the model, the host and the client), plus the `/ws` requests, the frames and one STJ context.
2. `src/Rtok.Web`: Kestrel slim host with `/health`, `/ws` and the client assets (decoded once
   per segment, precompressed, immutable `_framework`, `index.html` fallback, CSP), the
   Origin/Host checks and the per-run token; the snapshot comes from `OperatorModel`.
3. `src/Rtok.Web.Client`: Blazor WebAssembly on Radzen: layout, sidebar, routing, theme toggle,
   the `/ws` client with loading/offline/Reconnect, the overview page. Rtok.Web publishes it
   before compile and embeds the output, with two Radzen themes only.
4. `rtok web [--host] [--port]` in the CLI, `dashboard` as a hidden alias.
5. Tests: `Rtok.Web.Tests`, `Rtok.Web.Client.Tests` (bUnit), architecture rules, smoke, the
   `ws-message` fuzz target.

Pending (answered "not available in this build yet" until their function is on `main`):
- Doctor plan/apply over `/ws` and its test. `DoctorFix.Plan`/`Apply` (T32) are on `main`;
  next: the fix action in `OperatorModel` (T43's open item), a plan frame in
  `Rtok.Web.Contracts`, and the selection mapped onto `FixRequest`.
- Graph drill-down and link/unlink over `/ws`, and their test: need the project registry
  (catalog B15), which is not on `main`; T24 brought only the tags index.
- Live calls frames over `/ws` and the CallBatch unit test.
