namespace Rtok.Config;

/// <summary>Which session a hook event belongs to.</summary>
public static class SessionId
{
    /// <summary>What a session is called when nothing names it.</summary>
    public const string Unknown = "unknown";

    /// <summary>The id the event's stdin carries, else <c>$&lt;core.session_env&gt;</c>, else <see cref="Unknown"/>.</summary>
    /// <param name="fromStdin">The id in the event payload, or null.</param>
    /// <param name="core">The <c>[core]</c> settings.</param>
    /// <param name="env">Reads an environment variable; null reads the process environment.</param>
    /// <returns>The session id.</returns>
    public static string Resolve(
        string? fromStdin,
        CoreSection core,
        Func<string, string?>? env = null
    )
    {
        ArgumentNullException.ThrowIfNull(core);
        env ??= Environment.GetEnvironmentVariable;
        return NonEmpty(fromStdin)
            ?? (core.SessionEnv.Length > 0 ? NonEmpty(env(core.SessionEnv)) : null)
            ?? Unknown;
    }

    private static string? NonEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
