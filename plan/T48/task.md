---
status: todo
priority: P2
complexity: 3
readiness: 0%
agent:
---

# T48. Windows portability

rtok runs on Windows; paths, shells and process handles differ there.

Done when:
- Paths compare case-insensitively.
- `file://` URIs are encoded.
- No verbatim prefix leaks.
- Binaries are found through PATHEXT, with the `cmd /C` shim.
- Quoting is PowerShell-safe, and nothing with `'` is wrapped under Git Bash (rtok T55.12).
- Log rotation works while the file is open.
- Detached spawns inherit no handles.
- `rtok run` quotes for cmd.exe.

Spec: catalog A32, catalog B24 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T55.1, T55.12, T83.1, T83.3, T83.8, T83.9, T429, T430, T504; open T55.12 ([open work](../../docs/port/open-work.md)).

Check: The Windows CI runner passes every test, including the Windows-specific cases from the catalog.
