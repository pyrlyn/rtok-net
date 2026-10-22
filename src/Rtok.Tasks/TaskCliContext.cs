using Rtok.Config;
using Rtok.Tasks.Remote;

namespace Rtok.Tasks;

/// <summary>What <see cref="TaskCli"/> takes from the surface that runs it.</summary>
/// <param name="Cwd">The working directory, which names the checkout.</param>
/// <param name="Options">What a config load reads; <see cref="ConfigLoadOptions.ConfigFile"/> is <c>--config</c>.</param>
/// <param name="Http">The HTTP client for the remote adapters.</param>
/// <param name="Env">Reads an environment variable: tokens and <c>RTOK_AGENT_ID</c>.</param>
/// <param name="Run">Runs a token helper such as <c>gh auth token</c>, or null where none may run.</param>
/// <param name="Clock">The clock.</param>
public sealed record TaskCliContext(
    string Cwd,
    ConfigLoadOptions Options,
    HttpClient Http,
    Func<string, string?> Env,
    QuietRun? Run,
    TimeProvider Clock
)
{
    /// <summary>Where <c>--body-file -</c> reads from.</summary>
    public TextReader Stdin { get; init; } = TextReader.Null;

    /// <summary>The <c>User-Agent</c> of remote calls.</summary>
    public string UserAgent { get; init; } = "rtok";

    /// <summary>The least time between two remote writes.</summary>
    public TimeSpan WriteGap { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>The service URLs.</summary>
    public TaskEndpoints Endpoints { get; init; } = new();
}
