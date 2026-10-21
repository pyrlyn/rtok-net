# Pyrlyn.Trash

Moves files and folders to the OS trash: the macOS Trash, the FreeDesktop trash on Linux and the Windows Recycle Bin. A failure leaves the item in place.

## Install

```bash
dotnet add package Pyrlyn.Trash
```

## Use

```csharp
using Pyrlyn.Trash;

try
{
    var where = OsTrash.Move("build/old-cache");   // file, folder or symlink
    Console.WriteLine($"moved to {where}");          // null when the OS does not say
}
catch (TrashException e)
{
    // The OS refused; the item is still where it was.
}
```

- **macOS**: `NSFileManager trashItemAtURL:resultingItemURL:error:`, the call Finder makes,
  through `objc_msgSend`. Put Back works.
- **Linux**: the [FreeDesktop trash spec](https://specifications.freedesktop.org/trash-spec/1.0/).
  An item on the home trash's device goes to `$XDG_DATA_HOME/Trash` (default
  `~/.local/share/Trash`). An item on another mount goes to that mount's `.Trash/$uid`, used
  only when `.Trash` is a real folder with the sticky bit, or else to `.Trash-$uid`. The
  `.trashinfo` file (escaped `Path`, local `DeletionDate`) is created first with `CreateNew`
  to claim the name; then the item is renamed into `files/`. A taken name becomes
  `a.2.txt`. Items are never copied across devices, and the trash itself cannot be trashed.
- **Windows**: `IFileOperation.DeleteItem` with `FOFX_RECYCLEONDELETE` and no UI, on an STA
  thread, through source-generated COM (AOT-safe). A progress sink cancels any delete the
  shell would not recycle (the item is too big, or the drive has no Recycle Bin), so nothing
  is destroyed for good. The return value is the `$R…` path in the Recycle Bin.
- **Fails closed**: when the OS refuses, `TrashException` (an `IOException`) is thrown and the
  item stays at its path. If nothing is at the path, `FileNotFoundException` is thrown.
- A symlink is trashed itself, not its target.
