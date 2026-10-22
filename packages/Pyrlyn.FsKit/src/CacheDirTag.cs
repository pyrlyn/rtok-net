namespace Pyrlyn.FsKit;

/// <summary>
/// The Cache Directory Tagging Specification (bford.info/cachedir): a directory holding a
/// <c>CACHEDIR.TAG</c> that starts with the signature is a cache its tool regenerates.
/// </summary>
public static class CacheDirTag
{
    /// <summary>The tag's file name.</summary>
    public const string FileName = "CACHEDIR.TAG";

    /// <summary>The first bytes of a valid tag.</summary>
    public const string Signature = "Signature: 8a477f597d28d172789f06886806bc55";

    /// <summary>Whether <paramref name="dir"/> holds a valid tag. A tag that is a symlink, or cannot be read, is not one.</summary>
    /// <param name="dir">A directory.</param>
    public static bool IsCacheDir(string dir)
    {
        ArgumentException.ThrowIfNullOrEmpty(dir);
        var tag = Path.Combine(dir, FileName);
        try
        {
            if (new FileInfo(tag) is not { Exists: true, LinkTarget: null })
            {
                return false;
            }
            Span<byte> head = stackalloc byte[Signature.Length];
            using var stream = File.OpenRead(tag);
            stream.ReadExactly(head);
            for (var i = 0; i < head.Length; i++)
            {
                if (head[i] != Signature[i])
                {
                    return false;
                }
            }
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
