# Pyrlyn.FsKit

Atomic writes through symlink chains, path identity without verbatim prefixes, `Contain`, file
identity, tree disk usage and ages, and `CACHEDIR.TAG` caches. AOT-clean; native calls go through `LibraryImport`.

## Install

```bash
dotnet add package Pyrlyn.FsKit
```

## Use

```csharp
using Pyrlyn.FsKit;

// Sibling temp file + rename. A symlinked target (a dotfile manager's link) is written through
// and the link kept; a cycle or more than 40 links is refused and the links stay as they were.
AtomicFile.Write(settingsPath, json);
AtomicFile.Write(keyPath, key, AtomicWriteOptions.Default with
{
    CreateParent = true,
    UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite, // set at creation
});

// The real path, without the Windows \\?\ prefix; case folded on Windows when compared.
var real = FsPath.Canonicalize(path);
bool inside = FsPath.StartsWith(real, root);

// The one guard for a path from untrusted input: symlinks are followed through the deepest
// existing ancestor, then `..` is applied lexically. Throws UnauthorizedAccessException outside.
var safe = FsPath.Contain(root, requested);

// Device + inode on Unix, volume serial + file index on Windows.
bool sameFile = FileIdentity.Same(a, b);

// Allocated bytes: hard links once, APFS clones once, symlinks never followed.
TreeUsage usage = FsTree.Usage(cacheDir, TimeSpan.FromSeconds(2));

// The newest file mtime and the tagged caches below, each with its own newest; a nested
// checkout (a folder holding .git) is skipped, a link counts its own time.
TreeAges ages = FsTree.Ages(dataDir, TimeSpan.FromSeconds(10));
bool tagged = CacheDirTag.IsCacheDir(dir); // "Signature: 8a477f59…" at the start of CACHEDIR.TAG
```

## Platforms

| | stat / identity | real path |
|---|---|---|
| macOS arm64, x64 | `lstat` (64-bit inode), `getattrlist` for clone families | `realpath` |
| Linux x64, arm64, riscv64, loongarch64 (glibc, musl) | `lstat`, `__lxstat64` before glibc 2.33 | `realpath` |
| Windows | `GetFileInformationByHandle(Ex)` | `GetFinalPathNameByHandleW` |

Elsewhere `FileIdentity` throws `PlatformNotSupportedException` and `FsTree.Usage` counts
apparent sizes.
