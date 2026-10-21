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

Tests:
- `Pyrlyn.FsKit.Tests` unit: on `MemFs.Windows()`, `C:\Repo\a.cs` and `c:\repo\A.CS` compare equal for display and read paths.
- `Pyrlyn.FsKit.Tests` CsCheck: path identity of any `\\?\` or `\\?\UNC\` path equals that of its plain form, and no returned path keeps the verbatim prefix.
- `Pyrlyn.FsKit.Tests` CsCheck: a `file://` URI built from a Windows path with spaces, `#`, `%` and non-ASCII characters is percent-encoded and parses back to the same path.
- `Rtok.Plugins.Cmd.Tests` unit: `never_wrap` matches `Sudo.exe` case-insensitively.
- `Rtok.Core.Tests` unit: binary lookup honours PATHEXT to find `tool.cmd` and `tool.exe`, and probes a `.cmd` shim's version through `cmd /C`.
- `Rtok.Plugins.Cmd.Tests` CsCheck: for any argument, `wrap_quote` output parsed by PowerShell quoting rules gives back the same argument.
- `Rtok.Plugins.Cmd.Tests` unit: under a Git Bash host shell, a command containing `'` is not wrapped and keeps its apostrophes (rtok T55.12).
- `Pyrlyn.RotatingLog.Tests` integration: rotation succeeds while another handle holds the log file open, and no line is lost.
- `Rtok.Core.Tests` integration: a Detached spawn inherits no handles, so the parent's redirected stdout reaches EOF while the child still runs.
- `Rtok.Plugins.Cmd.Tests` CsCheck: `rtok run` quoting for cmd.exe keeps any argument with `^ & | < > " %` and spaces byte for byte under raw-arg semantics.
- `Rtok.Smoke` smoke: on the Windows runner the published `rtok run -- cmd /C echo "a b"` and `rtok-hook` on a Windows-path fixture exit 0 with the expected output.

Spec: catalog A32, catalog B24 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T55.1, T55.12, T83.1, T83.3, T83.8, T83.9, T429, T430, T504; open T55.12 ([open work](../../docs/port/open-work.md)).

Fuzz: `win-quote` in `tests/Rtok.Fuzz`: any argument quoted for cmd.exe and PowerShell parses back to itself under `CommandLineToArgvW` rules.

Check: The Windows CI runner passes every test, including the Windows-specific cases from the catalog.
