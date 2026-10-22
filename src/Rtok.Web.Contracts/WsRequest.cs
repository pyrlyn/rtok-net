namespace Rtok.Web.Contracts;

/// <summary>
/// One message a client sends over <c>/ws</c>: an optional request id and exactly one request,
/// named by its property (<c>{"req":7,"toggle":{"plugin":"docs","enabled":false}}</c>).
/// </summary>
public sealed record WsRequest
{
    /// <summary>Chosen by the client and echoed in the reply, so an answer that comes late is
    /// never taken for the answer to a later request.</summary>
    public long? Req { get; init; }

    /// <summary>Turn a linked plugin on or off.</summary>
    public ToggleRequest? Toggle { get; init; }

    /// <summary>The archived payload behind an archive id.</summary>
    public string? Expand { get; init; }

    /// <summary>The <c>doctor --fix</c> checklist: plan it, or apply a confirmed plan.</summary>
    public DoctorRequest? Doctor { get; init; }

    /// <summary>The inside of one project for the graph page; read-only.</summary>
    public GraphRequest? Graph { get; init; }

    /// <summary>A change to the project registry: link or unlink two projects.</summary>
    public ProjectRequest? Project { get; init; }

    /// <summary>Start or stop the live calls frames.</summary>
    public CallsRequest? Calls { get; init; }

    /// <summary>How many requests the message names; a valid message names exactly one.</summary>
    /// <returns>The count.</returns>
    public int Requests() =>
        (Toggle is null ? 0 : 1)
        + (Expand is null ? 0 : 1)
        + (Doctor is null ? 0 : 1)
        + (Graph is null ? 0 : 1)
        + (Project is null ? 0 : 1)
        + (Calls is null ? 0 : 1);
}
