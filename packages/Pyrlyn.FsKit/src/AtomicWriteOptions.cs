namespace Pyrlyn.FsKit;

/// <summary>How <see cref="AtomicFile"/> replaces a file. The defaults are the safe ones.</summary>
public sealed record AtomicWriteOptions
{
    /// <summary>The safe defaults: durable, permissions kept, symlinks followed, no parent created.</summary>
    public static AtomicWriteOptions Default { get; } = new();

    /// <summary>
    /// Fsync the file before the rename and its directory after it (default on). Without the file
    /// fsync the kernel may record the rename and lose the bytes, leaving an empty file after a crash.
    /// </summary>
    public bool Durable { get; init; } = true;

    /// <summary>
    /// Copy an existing target's Unix mode onto the new file (default on), so a 0600 file stays
    /// 0600. On Windows this clears the target's read-only bit instead, because the rename cannot
    /// replace a read-only file.
    /// </summary>
    public bool PreservePermissions { get; init; } = true;

    /// <summary>
    /// When the target is a symlink, write the file at the end of its chain and keep every link
    /// (default on). Off replaces the link itself with a plain file.
    /// </summary>
    public bool FollowSymlinks { get; init; } = true;

    /// <summary>Create the target's missing parent directories (default off).</summary>
    public bool CreateParent { get; init; }

    /// <summary>
    /// Unix mode the new file is created with (the umask still applies). An existing target's mode
    /// wins while <see cref="PreservePermissions"/> is on. Ignored on Windows.
    /// </summary>
    public UnixFileMode? UnixCreateMode { get; init; }
}
