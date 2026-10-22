namespace Rtok.Tasks.Remote;

/// <summary>What the remote adapters take from the surface that opens them.</summary>
/// <param name="Http">The HTTP client; the surface sets its 30 s timeout.</param>
/// <param name="Env">Reads an environment variable.</param>
/// <param name="Run">Runs a token helper, or null where none may run.</param>
/// <param name="Clock">The clock for pacing, rate limits and timestamps.</param>
/// <param name="Warn">Takes a warning line: a write that half failed, never an error.</param>
public sealed record RemoteContext(
    HttpClient Http,
    Func<string, string?> Env,
    QuietRun? Run,
    TimeProvider Clock,
    Action<string> Warn
)
{
    /// <summary>The least time between two writes; the services ask for a second.</summary>
    public TimeSpan WriteGap { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>The <c>User-Agent</c>: <c>rtok/&lt;version&gt;</c>.</summary>
    public string UserAgent { get; init; } = "rtok";

    /// <summary>The first non-blank of <paramref name="names"/>, else the trimmed output of
    /// <paramref name="command"/>.</summary>
    /// <param name="adapter">The adapter, for the message.</param>
    /// <param name="names">The variables, in order.</param>
    /// <param name="command">The helper that prints a token.</param>
    /// <param name="ct">Cancels the helper.</param>
    /// <returns>The token.</returns>
    /// <exception cref="TaskException">No token anywhere.</exception>
    public async Task<string> TokenAsync(
        string adapter,
        IReadOnlyList<string> names,
        IReadOnlyList<string> command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(command);
        foreach (var name in names)
        {
            if (Env(name)?.Trim() is { Length: > 0 } value)
            {
                return value;
            }
        }
        if (
            Run is not null
            && (await Run(command, ct).ConfigureAwait(false))?.Trim() is { Length: > 0 } output
        )
        {
            return output;
        }
        var run = command.Count > 0 ? $" or run `{string.Join(' ', command)}`" : "";
        throw new TaskException($"{adapter}: no token; set {string.Join(" or ", names)}{run}");
    }
}
