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

Spec: catalog B17 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: no rtok task; new for .NET.

Check: Tests on each OS show a trashed file is gone from its path and present in the trash.
