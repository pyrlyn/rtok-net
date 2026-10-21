namespace Rtok.Agents.Hosts;

/// <summary>A request <c>rtok agents</c> refuses: no host, an unknown one, or none of them installed.</summary>
public sealed class AgentsException : Exception
{
    /// <summary>Creates the exception.</summary>
    public AgentsException() { }

    /// <summary>Creates the exception with the message the CLI prints after <c>error: </c>.</summary>
    public AgentsException(string message)
        : base(message) { }

    /// <summary>Creates the exception with its cause.</summary>
    public AgentsException(string message, Exception inner)
        : base(message, inner) { }
}
