using System.Security.Cryptography;

namespace Rtok.Store;

/// <summary>Owner-only files (rtok S3): the database, its WAL and SHM siblings and the archived
/// bodies hold prompts and tool output.</summary>
internal static class StoreFiles
{
    private const UnixFileMode PrivateDir =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode PrivateFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private static long _nextTemp;

    /// <summary>Creates <paramref name="dir"/> and its missing parents with mode 0700. An
    /// existing directory keeps its mode: the operator may have chosen a shared path.</summary>
    public static void CreatePrivateDirectory(string dir)
    {
        if (dir.Length == 0 || Directory.Exists(dir))
        {
            return;
        }
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(dir);
            return;
        }
        Directory.CreateDirectory(dir, PrivateDir);
    }

    /// <summary>Creates an empty file with mode 0600, or leaves a zero-length one. SQLite gives
    /// the WAL and SHM files the database file's mode.</summary>
    public static void CreatePrivateFile(string path)
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.OpenOrCreate,
            Access = FileAccess.Write,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = PrivateFile;
        }
        using var _ = new FileStream(path, options);
    }

    /// <summary>Mode 0600 on the database file and its WAL and SHM siblings. Best effort: a file
    /// owned by someone else keeps its mode.</summary>
    public static void RestrictDatabase(string dbPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        foreach (var path in (string[])[dbPath, dbPath + "-wal", dbPath + "-shm"])
        {
            try
            {
                if (File.Exists(path) && File.GetUnixFileMode(path) != PrivateFile)
                {
                    File.SetUnixFileMode(path, PrivateFile);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Not ours to change.
            }
        }
    }

    /// <summary>Where the body with id <paramref name="sha"/> lives: <c>dir/&lt;first two
    /// digits&gt;/&lt;sha256&gt;</c>, sharded so no directory holds tens of thousands of files
    /// (rtok I-S3).</summary>
    public static string ArchivePath(string dir, string sha) =>
        System.IO.Path.Combine(dir, sha.Length > 2 ? sha[..2] : "_", sha);

    /// <summary>Writes <paramref name="body"/> to <see cref="ArchivePath"/>, content-addressed
    /// so the same body twice is one file. The write goes to a sibling temp file renamed over the
    /// target, because <c>expand</c> reads the file without a lock. Returns the path and whether
    /// this call created it: only such a file is safe to remove when the caller's insert fails.</summary>
    public static (string Path, bool Created) WriteArchive(
        string dir,
        string sha,
        ReadOnlySpan<byte> body
    )
    {
        var path = ArchivePath(dir, sha);
        var shard = System.IO.Path.GetDirectoryName(path)!;
        CreatePrivateDirectory(dir);
        CreatePrivateDirectory(shard);
        var created = !File.Exists(path);
        var n = Interlocked.Increment(ref _nextTemp);
        var temp = System.IO.Path.Combine(shard, $".{sha}.tmp-{Environment.ProcessId}-{n}");
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
            };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = PrivateFile;
            }
            using (var stream = new FileStream(temp, options))
            {
                stream.Write(body);
            }
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            File.Delete(temp);
            throw;
        }
        return (path, created);
    }

    /// <summary>Lowercase hex SHA-256 of <paramref name="bytes"/>: an archive's id and file name.</summary>
    public static string Sha256Hex(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));
}
