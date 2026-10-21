namespace Rtok.Agents.Hosts;

/// <summary>Whether <c>rtok agents install</c> can write a module into a host app.</summary>
public enum SupportLevel
{
    /// <summary>Written by a plain install.</summary>
    Yes,

    /// <summary>Written only when a flag is given (<c>--proxy</c>, <c>--yes</c>).</summary>
    Flag,

    /// <summary>
    /// Never written by install: it prints how to install the module behind a flag, and the
    /// host's own installer owns the state.
    /// </summary>
    Offer,

    /// <summary>Cannot be written; the note says why.</summary>
    No,
}
