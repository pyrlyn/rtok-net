# T12.7 done

New package `packages/Pyrlyn.Trash` (0.1.0):

- `OsTrash.Move(path)` moves a file, folder or symlink to the OS trash and returns its new path, or null when the OS does not say. A symlink is trashed itself, not its target. When the OS refuses, it throws `TrashException` (an `IOException`) and the item stays at its path. When nothing is at the path, it throws `FileNotFoundException`. It never copies and then deletes.
- macOS: `[[NSFileManager defaultManager] trashItemAtURL:resultingItemURL:error:]` through `objc_msgSend` function pointers (`LibraryImport`, inside an autorelease pool). The error is `localizedDescription`.
- Linux, the FreeDesktop trash spec 1.0:
  - An item on the home trash's device goes to `$XDG_DATA_HOME/Trash` (default `~/.local/share/Trash`).
  - Otherwise it goes to the top of its mount: `.Trash/$uid` when `.Trash` is a real folder with the sticky bit, else `.Trash-$uid`. Device ids come from Pyrlyn.FsKit's `FileIdentity`, and the uid from `/proc/self/status`, so no libc call is needed.
  - The `.trashinfo` file is created first with `CreateNew` to claim the name. It holds the escaped `Path` (absolute in the home trash, relative to the mount top elsewhere) and the local `DeletionDate`.
  - Then the item is renamed into `files/` with `Directory.Move`, which is `rename(2)` for every kind of item. `File.Move` links and then unlinks, and the Linux run showed it leaves a second link in the trash when the unlink is refused.
  - A failed rename removes the info file. A taken name becomes `a.2.txt` (`.env.2` for a dotfile). The trash itself cannot be trashed. The optional `directorysizes` cache is not written.
- Windows: `IFileOperation.DeleteItem` with `FOF_ALLOWUNDO`, `FOFX_RECYCLEONDELETE`, `FOFX_EARLYFAILURE` and no UI, on an STA thread, through `[GeneratedComInterface]` and `[GeneratedComClass]` (AOT-safe).
  - A progress sink returns `E_ABORT` from `PreDeleteItem` when `TSF_DELETE_RECYCLE_IF_POSSIBLE` is missing. So an item the shell would not recycle (too big, or no Recycle Bin on the drive) is kept, never destroyed for good.
  - `PostDeleteItem` gives the `$R…` path that `Move` returns.

No new NuGet dependency. The package references Pyrlyn.FsKit for device ids.

Checked:
- macOS (arm64, this machine): 5 of 5 OS tests pass against the real `~/.Trash`. A file and a folder leave their path and are readable at the returned path. A symlink is trashed and its target kept. A missing path throws. A move out of a read-only folder throws and leaves the file. The 8 Linux-only tests skip.
- Linux (`mcr.microsoft.com/dotnet/sdk:10.0`, arm64, in colima):
  - As a normal user with a tmpfs at `/mnt/t` and `PYRLYN_TRASH_OTHER_DEVICE=/mnt/t`: 13 of 13 pass. The tmpfs item went to `/mnt/t/.Trash-<uid>/files/far.txt`, with a relative `Path` in its info file.
  - As root: 11 pass and 2 skip (root ignores folder modes; no second mount given).
  - The home trash is empty after the runs.
  - The Linux tests cover the info file text (escaping, date format), numbered names, a failed move (no info file left, item in place), refusing the trash itself, and the choice between `.Trash/$uid` and `.Trash-$uid` (sticky, not sticky, symlinked).
- Windows: not run here; there is no Windows machine. The same `OsTrashTests` run on the `windows-2025` CI leg (T3). The failure case there is a file held open with `FileShare.None`, and the symlink test skips because it needs developer mode. With no Windows machine, the interop was checked mechanically against Microsoft's `Windows.Win32.winmd` (Microsoft.Windows.SDK.Win32Metadata 71.0.37-preview), read with System.Reflection.Metadata next to the compiled `Pyrlyn.Trash.dll`. `IFileOperation` (20 slots), `IFileOperationProgressSink` (16) and `IShellItem` (5) match slot for slot in order and arity. So do their IIDs, `CLSID_FileOperation`, `CoCreateInstance`, `SHCreateItemFromParsingName`, and every flag value (`FOF_*`, `FOFX_RECYCLEONDELETE`, `FOFX_EARLYFAILURE`, `TSF_DELETE_RECYCLE_IF_POSSIBLE`, `SIGDN_FILESYSPATH`, `E_ABORT`). What still needs a Windows run: the shell's behaviour (the recycle flag in `PreDeleteItem`, the `$R…` path) and the STA thread under NativeAOT.
- `dotnet build rtok.slnx -c Release -warnaserror`, `dotnet test` (915 tests) and `dotnet csharpier check .` are green.
