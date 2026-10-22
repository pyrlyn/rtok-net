# Changelog

## 0.3.0

- `FsTree.Ages`: the newest file mtime under a tree and the `CACHEDIR.TAG` caches in it, links never followed and nested checkouts skipped.
- `CacheDirTag.IsCacheDir`: a directory holding a valid [Cache Directory Tagging](https://bford.info/cachedir/) signature.

## 0.2.0

- `FsPath.TryGetFileId`: the device and inode (file index on Windows) of an entry, so a reader can tell a file renamed away from the new one at its path.

## 0.1.0

- First release.
