---
status: todo
priority: P2
complexity: 4
readiness: 0%
agent:
---

# T45. TUI on Terminal.Gui v2

`rtok tui` gives the operator model in a terminal (N10).

Done when:
- Terminal.Gui v2 renders the pages, keys, footer, help overlay, filters and plugin toggles from the catalog, reading only the operator snapshot.
- Config keys: `[tui] tab`, `tick_secs`.
- On a non-TTY it prints one stderr line and exits 1.
- Interactive behaviour under AOT is verified first; if it fails, the creator decides the fallback.

Tests:
- `Rtok.Tui.Tests` Verify: each page rendered headless (Terminal.Gui FakeDriver, 100×30) from a fixture operator snapshot, with the footer `q quit · Left/Right or 1..N switch tabs · updated HH:MM:SS UTC` from a fixed `TimeProvider`.
- `Rtok.Core.Tests` arch: `Rtok.Tui` reads only the operator snapshot and never references `Rtok.Store`.
- `Rtok.Tui.Tests` unit: q, Esc and Ctrl-C quit, Left/Right wrap at both ends, 1..9 jump to a tab and `r` refreshes.
- `Rtok.Tui.Tests` Verify: the `?` help overlay, generated from the KEYS table.
- `Rtok.Tui.Tests` unit: every key the TUI handles is in the KEYS table, and every KEYS entry is handled.
- `Rtok.Tui.Tests` unit: an open filter takes keys before the global `?` and `r`, Ctrl-C still quits, and the filter narrows the rows.
- `Rtok.Tui.Tests` unit: Sessions moves its cursor with ↑/↓ and `l` toggles live-only; Calls shows at most 120 rows, Enter and `z` open the detail, and the selected row stays visible after a refresh.
- `Rtok.Tui.Tests` unit: panels scroll by wrapped rows and clamp on resize, and the Logs tab shows at most `[log] lines` rows.
- `Rtok.Tui.Tests` integration: a Plugins tab toggle goes through the config writer and changes only that plugin's key in `config.toml`, every other byte unchanged.
- `Rtok.Tui.Tests` unit: the Doctor tab shows the doctor text report verbatim and applies a fix only after the plan is confirmed.
- `Rtok.Config.Tests` unit: `[tui] tab` picks the start tab, `tick_secs` defaults to 2 and rejects 0 with file:line, and `RTOK_TUI_TAB` and `RTOK_TUI_TICK_SECS` override them.
- `Rtok.Cli.Tests` integration: `rtok tui` with stdin and stdout redirected prints exactly one stderr line and exits 1.
- `Rtok.Smoke` smoke: a Porta.Pty session drives the published `rtok tui` through tab switching, `?` help, a filter and `q`, and the output ends by leaving the alternate screen and showing the cursor.
- `Rtok.Smoke` smoke: Ctrl-C under Porta.Pty quits the published `rtok tui` and restores the terminal the same way.

Spec: catalog A25, catalog B20 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T15.1, T15.2, T15.3, T15.4, T15.5, T15.6, T15.7, T15.8, T15.9, T15.10, T15.11, T15.12, T60.8, T60.10, T113, T478, T480, T487, T500.

Check: A Porta.Pty test drives the AOT `rtok tui` through tab switching, help, a filter and quit, and the terminal is restored.
