namespace Pyrlyn.Ignore;

/// <summary>What an ignore rule says about a path.</summary>
public enum IgnoreMatch
{
    /// <summary>No rule names the path; a less specific source decides.</summary>
    None,

    /// <summary>The last rule naming the path ignores it.</summary>
    Ignore,

    /// <summary>The last rule naming the path is a <c>!</c> negation: it is kept.</summary>
    Whitelist,
}
