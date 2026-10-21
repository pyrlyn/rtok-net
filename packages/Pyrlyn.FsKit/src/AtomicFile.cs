using System.Text;
using Pyrlyn.FsKit.Native;

namespace Pyrlyn.FsKit;

/// <summary>
/// Replace a file atomically: the new bytes go to a sibling temp file that is renamed over the
/// target, so a reader, a crash or a full disk never sees a half-written file.
/// </summary>
public static class AtomicFile
{
    /// <summary>The most symlinks a write follows, the Linux kernel's own ELOOP limit.</summary>
    public const int MaxSymlinkHops = 40;

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private static long counter;

    /// <summary>Replace <paramref name="path"/> with <paramref name="bytes"/>.</summary>
    public static void Write(
        string path,
        ReadOnlySpan<byte> bytes,
        AtomicWriteOptions? options = null
    )
    {
        options ??= AtomicWriteOptions.Default;
        var target = Prepare(path, options);
        var (tmp, stream) = CreateTemp(target, options);
        try
        {
            using (stream)
            {
                stream.Write(bytes);
                Seal(stream, target, options);
            }
            Swap(tmp, target, options);
        }
        catch
        {
            TryDelete(tmp);
            throw;
        }
    }

    /// <summary>Replace <paramref name="path"/> with <paramref name="text"/> as UTF-8 without a BOM.</summary>
    public static void Write(string path, string text, AtomicWriteOptions? options = null) =>
        Write(path, Utf8.GetBytes(text), options);

    /// <summary>Replace <paramref name="path"/> with whatever <paramref name="fill"/> writes into the temp file.</summary>
    public static void WriteWith(
        string path,
        Action<Stream> fill,
        AtomicWriteOptions? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(fill);
        options ??= AtomicWriteOptions.Default;
        var target = Prepare(path, options);
        var (tmp, stream) = CreateTemp(target, options);
        try
        {
            using (stream)
            {
                fill(stream);
                Seal(stream, target, options);
            }
            Swap(tmp, target, options);
        }
        catch
        {
            TryDelete(tmp);
            throw;
        }
    }

    /// <summary>
    /// Write <paramref name="bytes"/> to <paramref name="path"/> unless it already holds exactly
    /// them, creating the parent directory. True when it wrote, so a caller can report
    /// <c>wrote</c> or <c>unchanged</c>.
    /// </summary>
    public static bool WriteIfChanged(string path, ReadOnlySpan<byte> bytes)
    {
        if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
        {
            return false;
        }
        Write(path, bytes, AtomicWriteOptions.Default with { CreateParent = true });
        return true;
    }

    /// <summary>
    /// The file a write to <paramref name="path"/> lands on: the end of its symlink chain, followed
    /// by hand so a dangling link's target is created rather than the link replaced. A cycle or a
    /// chain longer than <see cref="MaxSymlinkHops"/> is refused, because replacing one of its links
    /// with a file would silently break the others.
    /// </summary>
    /// <exception cref="IOException">A cycle or too many links.</exception>
    public static string ResolveTarget(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var current = path;
        for (var hops = 0; ; hops++)
        {
            var link = new FileInfo(current).LinkTarget;
            if (link is null)
            {
                break;
            }
            if (hops == MaxSymlinkHops)
            {
                throw new IOException($"{path}: too many levels of symbolic links");
            }
            current = Path.Combine(Path.GetDirectoryName(current) ?? "", link);
        }
        var dir = Path.GetDirectoryName(current);
        var name = Path.GetFileName(current);
        return string.IsNullOrEmpty(dir) ? current : Path.Combine(FsPath.Canon(dir), name);
    }

    /// <summary>Fsync a directory so a rename or create inside it survives a crash; best effort, a no-op on Windows.</summary>
    public static void SyncDirectory(string dir)
    {
        if (!OperatingSystem.IsWindows())
        {
            Unix.SyncDirectory(dir);
        }
    }

    private static string Prepare(string path, AtomicWriteOptions options)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var name = Path.GetFileName(path);
        if (name.Length == 0 || name is "." or "..")
        {
            throw new ArgumentException($"{path} names no file", nameof(path));
        }
        if (options.CreateParent && Path.GetDirectoryName(path) is { Length: > 0 } parent)
        {
            Directory.CreateDirectory(parent);
        }
        return options.FollowSymlinks ? ResolveTarget(path) : path;
    }

    /// <summary>
    /// A fresh temp file beside <paramref name="target"/>, created new with pid, time and a
    /// process-wide counter in its name, so two writers never share one and a crashed run's
    /// leftover is never reused.
    /// </summary>
    private static (string Path, FileStream Stream) CreateTemp(
        string target,
        AtomicWriteOptions options
    )
    {
        var dir = Path.GetDirectoryName(target);
        var name = Path.GetFileName(target);
        var stream = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
        };
        if (options.UnixCreateMode is { } mode && !OperatingSystem.IsWindows())
        {
            stream.UnixCreateMode = mode;
        }
        IOException? last = null;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var n = Interlocked.Increment(ref counter);
            var tmpName = $".{name}.tmp-{Environment.ProcessId}-{DateTime.UtcNow.Ticks}-{n}";
            var tmp = string.IsNullOrEmpty(dir) ? tmpName : Path.Combine(dir, tmpName);
            try
            {
                return (tmp, new FileStream(tmp, stream));
            }
            catch (IOException e) when (File.Exists(tmp))
            {
                last = e;
            }
        }
        throw last ?? new IOException($"{target}: no free temp file name");
    }

    private static void Seal(FileStream stream, string target, AtomicWriteOptions options)
    {
        if (options.PreservePermissions)
        {
            KeepPermissions(target, stream);
        }
        stream.Flush(flushToDisk: options.Durable);
    }

    private static void Swap(string tmp, string target, AtomicWriteOptions options)
    {
        File.Move(tmp, target, overwrite: true);
        if (options.Durable && Path.GetDirectoryName(Path.GetFullPath(target)) is { } dir)
        {
            // Best effort: some filesystems refuse to fsync a directory, and the bytes are safe by then.
            SyncDirectory(dir);
        }
    }

    private static void KeepPermissions(string target, FileStream tmp)
    {
        if (OperatingSystem.IsWindows())
        {
            // MOVEFILE_REPLACE_EXISTING cannot replace a read-only file; drop the bit on the target
            // rather than copying it onto the temp file.
            var info = new FileInfo(target);
            if (info.Exists && info.IsReadOnly)
            {
                info.IsReadOnly = false;
            }
            return;
        }
        UnixFileMode mode;
        try
        {
            mode = File.GetUnixFileMode(target);
        }
        catch (FileNotFoundException)
        {
            return;
        }
        catch (DirectoryNotFoundException)
        {
            return;
        }
        File.SetUnixFileMode(tmp.SafeFileHandle, mode);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
