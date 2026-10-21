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

Spec: catalog A25, catalog B20 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T15.1, T15.2, T15.3, T15.4, T15.5, T15.6, T15.7, T15.8, T15.9, T15.10, T15.11, T15.12, T60.8, T60.10, T113, T478, T480, T487, T500.

Check: A Porta.Pty test drives the AOT `rtok tui` through tab switching, help, a filter and quit, and the terminal is restored.
