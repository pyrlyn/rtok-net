namespace Rtok.Agents.Junk;

/// <summary>What a host folder holds.</summary>
public enum JunkRole
{
    /// <summary>The host's config or data home: never junk as a whole (§22).</summary>
    Data,

    /// <summary>Temp files.</summary>
    Temp,

    /// <summary>Logs.</summary>
    Logs,

    /// <summary>Caches.</summary>
    Cache,
}
