# T12.1 done

`packages/Pyrlyn.FsKit`, ported from the Rust fs-kit (`write`, `path`, `tree`):

- `AtomicFile`: sibling temp file (`CreateNew`, pid + time + counter) renamed over the target,
  file and directory fsync, Unix mode kept or set at creation (`UnixCreateMode`), the Windows
  read-only bit cleared. A symlinked target is followed hop by hop to the end of its chain, so a
  dangling link's target is created and every link stays; a cycle or more than 40 hops throws
  before anything is written. `WriteIfChanged`, `ResolveTarget`, `SyncDirectory`.
- `FsPath`: `Canonicalize` (realpath / GetFinalPathNameByHandleW) without `\\?\`, `Simplified`
  (drops `\\?\` and `\\?\UNC\` only where the plain path means the same), `Normalize` (lexical),
  `Resolved`, `Contain`, and Windows-case-folded `SamePath`, `StartsWith`, `EndsWith`,
  `StripPrefix`, `SameFile`, `IsUnwalkableRoot`.
- `FileIdentity`: device + inode on Unix, volume serial + file index on Windows.
- `FsTree.Usage`: allocated blocks, a hard-linked file once, APFS clones once (getattrlist clone
  id; later clones add only their private bytes), symlinks never followed, optional time limit.
- Native calls are `LibraryImport` (AOT-clean): `lstat` per platform layout (macOS 64-bit
  inode, Linux x64 and the generic arm64/riscv64/loongarch64 layout, `__lxstat64` before glibc
  2.33), `realpath`, `getattrlist`, `open`/`fsync`; kernel32 handle queries on Windows.

Checked: `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test` (48 tests, one
Windows-only skip) and `dotnet csharpier check .` green on macOS arm64. Tests cover symlink
chains, linked directories, dangling links, the 40-link cap (40 followed, 41 refused, links
untouched), cycles, `Contain` escapes (`..`, absolute, prefix siblings, links out, `link/..`),
hard-link counting, sparse files, APFS clones, file modes and verbatim prefixes. Not run here:
Linux and Windows (no container network on this machine, no Windows host); the Windows-only and
Linux layout paths are compile-checked only and wait for CI (T2).
