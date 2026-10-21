---
status: todo
priority: P0
complexity: 3
readiness: 0%
agent:
---

# T12.1. Package Pyrlyn.FsKit: atomic writes, path identity, tree usage

Installers, the archive and worktrees all need the same safe file operations.

Done when:
- Atomic write follows symlink chains, writing through to the target. A cycle or more than 40 links is refused and the links are left as they were.
- Displayed and compared paths carry no Windows verbatim prefix.
- `contain(root, path)` follows symlinks through the deepest existing ancestor, then applies `..` lexically.
- Tree usage counts allocated blocks, with hard links and clones counted once.
- File identity is device + inode on Unix and the file index on Windows.
- Unix file modes are set at creation.

Spec: catalog B3, catalog B5 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: no rtok task; new for .NET.

Check: Tests on macOS, Linux and Windows cover symlink chains, the 40-link cap, `contain` escapes and hard-link counting.
