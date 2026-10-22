using Rtok.Config;
using Rtok.Measure;

namespace Rtok.Cli;

/// <summary><c>rtok stats</c>: what the agents' transcripts and rtok's store say rtok saved.</summary>
internal static class StatsCommands
{
    /// <summary>Report tokens per tool, Bash family and MCP group from the agents' transcripts, with rtok's rows.</summary>
    /// <param name="since">The window: 30d, 12h; [stats] since when omitted.</param>
    /// <param name="json">Print the report as JSON.</param>
    /// <param name="plugin">One plugin: a catalogue plugin's own page, or the report for its rows.</param>
    /// <param name="saveBaseline">Save the report as this baseline instead of printing it.</param>
    /// <param name="compare">Print the change against this saved baseline.</param>
    /// <param name="calibrate">Fit [estimator] chars per token with Anthropic's count_tokens (ANTHROPIC_API_KEY).</param>
    /// <param name="cache">Prompt-cache health per session from the proxy's usage rows.</param>
    /// <param name="price">Add the cost of the proxy's usage rows at [stats.prices].</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    /// <param name="cancellationToken">Ctrl-C.</param>
    public static async Task RunAsync(
        string? since = null,
        bool json = false,
        string? plugin = null,
        string? saveBaseline = null,
        string? compare = null,
        bool calibrate = false,
        bool cache = false,
        bool price = false,
        string? config = null,
        CancellationToken cancellationToken = default
    )
    {
        var flags = new List<ConfigFlag>();
        void Flag(string key, object? value, string name)
        {
            if (value is not null)
            {
                flags.Add(new ConfigFlag($"stats.{key}", value, name));
            }
        }
        Flag("since", since, "--since");
        Flag("format", json ? "json" : null, "--json");
        Flag("plugin", plugin, "--plugin");
        Flag("baseline", compare, "--compare");
        Flag("price", price ? true : null, "--price");
        var loaded = ConfigLoader.Load(
            new ConfigLoadOptions { ConfigFile = config, Flags = flags }
        );
        if (loaded.Issues.Count > 0)
        {
            throw new ConfigException(string.Join('\n', loaded.Issues));
        }
        if (calibrate)
        {
            using var http = new HttpClient();
            Console.Out.Write(
                await Calibrate
                    .RunAsync(
                        loaded.Config,
                        loaded.Locations.UserFile,
                        Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"),
                        http,
                        Calibrate.Endpoint,
                        DateTime.UtcNow,
                        cancellationToken
                    )
                    .ConfigureAwait(false)
            );
            return;
        }
        Console.Out.Write(
            StatsPage.Render(
                loaded.Config,
                loaded.Locations.Home,
                new StatsRequest { SaveBaseline = saveBaseline, Cache = cache },
                StatsSignals.None,
                DateTime.UtcNow
            )
        );
    }
}
