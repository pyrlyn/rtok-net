---
status: todo
priority: P3
complexity: 2
readiness: 0%
agent:
---

# T12.7. Package Pyrlyn.Trash: move to the OS trash

`agents junk clear --trash` moves files to the OS trash instead of deleting them.

Done when:
- macOS moves files with `NSFileManager trashItemAtURL`.
- Linux follows the FreeDesktop trash spec.
- Windows sends files to the Recycle Bin with `IFileOperation`.
- A failure leaves the file in place.

Tests:
- `Pyrlyn.Trash.Tests` integration: on macOS a temp file moved through `NSFileManager trashItemAtURL` is gone from its path and present at the returned trash URL.
- `Pyrlyn.Trash.Tests` integration: on Linux, with `XDG_DATA_HOME` in a temp dir, the file lands in `Trash/files` beside a `Trash/info/<name>.trashinfo` that names its original `Path`.
- `Pyrlyn.Trash.Tests` integration: on Linux a second file with the same name gets a unique trash name, and both keep their own `.trashinfo`.
- `Pyrlyn.Trash.Tests` CsCheck: the percent-encoded `Path=` of a `.trashinfo` decodes back to the original path for any file name, spaces and non-ASCII included.
- `Pyrlyn.Trash.Tests` integration: on Windows a temp file sent through `IFileOperation` is gone from its path and listed in the Recycle Bin.
- `Pyrlyn.Trash.Tests` integration: a directory is trashed as one item on each OS.
- `Pyrlyn.Trash.Tests` integration: a missing path, a file under a read-only parent and, on Windows, a file held open each return a failure and leave the file in place.
- `Pyrlyn.Trash.Tests` integration: on Linux an unwritable trash directory fails the move, leaves the file in place and leaves no `.trashinfo` behind.
- `Rtok.AotTests` AOT: the native trash call (Objective-C on macOS, COM on Windows, file moves on Linux) trashes a temp file from the published binary.

Spec: catalog B17 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: no rtok task; new for .NET.

Check: Tests on each OS show a trashed file is gone from its path and present in the trash.
