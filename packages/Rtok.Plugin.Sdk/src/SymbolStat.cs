namespace Rtok.Plugin.Sdk;

/// <summary>What the index recorded about one file, to tell whether it changed.</summary>
/// <param name="Sha">SHA-256 of the file at index time.</param>
/// <param name="Mtime">Modification time, nanoseconds since the Unix epoch (<see cref="Stamp"/>).</param>
/// <param name="Size">Size in bytes.</param>
public sealed record SymbolStat(string Sha, long Mtime, long Size)
{
    /// <summary>
    /// The freshness key of a file on disk: modification time in nanoseconds since the Unix
    /// epoch, and size. <c>(0, 0)</c> when the file cannot be read, which no stat gate treats as
    /// a match. The index and its pending check use this one function, so they never disagree.
    /// </summary>
    public static (long Mtime, long Size) Stamp(string absPath)
    {
        try
        {
            var info = new FileInfo(absPath);
            if (!info.Exists)
            {
                return (0, 0);
            }

            var ticks = info.LastWriteTimeUtc.Ticks - DateTime.UnixEpoch.Ticks;
            return (ticks < 0 ? 0 : ticks * 100, info.Length);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return (0, 0);
        }
    }
}
