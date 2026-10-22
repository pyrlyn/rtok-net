namespace Rtok.Proxy;

/// <summary>What <see cref="SummaryQueue.Admit"/> did with an id.</summary>
internal enum SummaryAdmit
{
    /// <summary>Every slot is busy; the id stays untried.</summary>
    Full,

    /// <summary>The id was tried before; no slot is held.</summary>
    Seen,

    /// <summary>The id is new and holds a slot.</summary>
    Run,
}
