namespace Rtok.Core;

/// <summary>Command and archive names shared by surfaces and plugins.</summary>
public static class Names
{
    /// <summary>Hex characters of an archive id that name it in markers and trailers;
    /// <c>rtok expand</c> resolves a unique prefix.</summary>
    public const int MinPrefix = 8;

    /// <summary>
    /// Basename of a command path: split on <c>/</c> and <c>\</c>, a trailing <c>.exe</c>
    /// (any case) dropped. One definition for agents, measure and the cmd formatters.
    /// </summary>
    public static string CmdStem(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var slash = path.LastIndexOfAny(['/', '\\']);
        var name = slash < 0 ? path : path[(slash + 1)..];
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }

    /// <summary>The first <see cref="MinPrefix"/> characters of an archive id.</summary>
    public static string ShortId(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return id.Length <= MinPrefix ? id : id[..MinPrefix];
    }
}
