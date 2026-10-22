---
status: todo
priority: P2
complexity: 5
readiness: 0%
agent:
---

# T44.2. Dashboard pages in Blazor

The rest of the dashboard moves into `Rtok.Web.Client` (N13), on the shell and `/ws` that T44 lays.

Done when:
- Every catalog A24 page works in Blazor: plugins (toggle), calls (expand), sessions (drill-down), doctor (Fix selected), logs, skills, stats, graph, hosts, config, services, worktrees, memory.
- The UX contracts from catalog A24 hold: ⌘K palette and `g o` shortcuts, filters in the URL query, clickable KPIs, CSV/JSON export with formula-injection escaping, Δtok trend, live status age plus pause, `rtok-theme` in localStorage, passthrough rows in bytes.
- The live graph calls panel matches catalog A24: 1/5/15 min windows, per-tool bars, backend shares, sparklines with 30 slots, tallies, compare colours, `vscode://` links, the narrow strip below 900px, reduced motion.
- Pages are built from Radzen.Blazor components (N13): `RadzenDataGrid` for tables, `RadzenDialog` for confirms and the ⌘K palette, `RadzenNotification` for errors, `RadzenChart` and `RadzenSparkline` for bars, trends and sparklines.
- ECharts (graph view only) and Three.js are vendored static files called through one JS interop module each; the 3D projects overview has its 2D fallback.
- The brand restyle (rtok T414) lands here: `--pyr-*` tokens mapped onto Radzen's `--rz-*` variables, grouped sidebar via `RadzenPanelMenu`.

Tests:
- `Rtok.Web.Client.Tests` bUnit: each page renders from a recorded snapshot and sends the catalog message for its action (toggle, expand, doctor plan/apply, graph drill-down, link/unlink).
- `Rtok.Web.Client.Tests` bUnit: filters round-trip through the URL query, and a reload with that URL shows the same filtered rows.
- `Rtok.Web.Client.Tests` CsCheck: CSV export escapes every cell starting with `=`, `+`, `-` or `@`, and JSON export round-trips.
- `Rtok.Web.Client.Tests` unit: a call past the display cap that ended in a CallBatch is never shown as interrupted.
- `Rtok.Web.Tests` integration: the embedded client's CSS defines the brand `--pyr-*` tokens and sets Radzen's `--rz-*` variables from them, and the bundle holds no copied `brand/` files.
- `Rtok.Web.Tests` integration: ECharts is referenced only by the graph view's interop module.
- `Rtok.Web.Tests` integration: no page needs a CSP source beyond what T44 set.

Ports: the page and UX parts of T44's list; open T414 ([open work](../../docs/port/open-work.md)).

Check: bUnit tests cover every catalog A24 page and its `/ws` action.
