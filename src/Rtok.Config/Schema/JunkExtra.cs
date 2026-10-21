namespace Rtok.Config;

/// <summary>One `[agents.junk] extra` entry: `path` is junk of `kind` for `host` (a host id or `rtok`)</summary>
/// <remarks>Read from [agents.junk.extra[]].</remarks>
public sealed class JunkExtra
{
    /// <summary>a host id, or rtok</summary>
    public string Host { get; set; } = "";

    /// <summary>cache | temp | logs | crash-dumps</summary>
    public string Kind { get; set; } = "";

    /// <summary>the folder that is junk; ~ is the user's home</summary>
    public string Path { get; set; } = "";
}
