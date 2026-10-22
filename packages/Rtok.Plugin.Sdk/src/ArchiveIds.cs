namespace Rtok.Plugin.Sdk;

/// <summary>
/// How an archive id is printed. The full id is a 64-hex SHA-256; every trailer, marker and
/// pointer prints its first <see cref="ShortLength"/> digits, which <c>rtok expand</c> resolves
/// back. A <c>Measurement</c>'s <c>RefId</c> keeps the full id.
/// </summary>
public static class ArchiveIds
{
    /// <summary>How many hex digits a printed id has, and the shortest prefix <c>expand</c>
    /// resolves.</summary>
    public const int ShortLength = 8;

    /// <summary>The printed form of <paramref name="id"/>: its first <see cref="ShortLength"/>
    /// characters, or the whole id when it is shorter.</summary>
    public static string Abbreviate(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return id.Length <= ShortLength ? id : id[..ShortLength];
    }
}
