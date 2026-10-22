namespace Rtok.Core;

/// <summary>Command names shared by surfaces and plugins.</summary>
public static class Names
{
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
}
