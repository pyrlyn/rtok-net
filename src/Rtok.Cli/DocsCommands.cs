using ConsoleAppFramework;
using Rtok.Config;
using Rtok.Plugins.Docs;
using Rtok.Store;

namespace Rtok.Cli;

/// <summary>Dependency docs for the docs plugin's MCP tools.</summary>
internal sealed class DocsCommands
{
    private readonly TextWriter _stdout = Console.Out;

    /// <summary>
    /// Download a dependency's docs at its lockfile-pinned version and index them for docs_query.
    /// Cargo.lock crates come from docs.rs, packages.lock.json packages from nuget.org.
    /// </summary>
    /// <param name="name">The dependency, as the lockfile names it.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    /// <param name="cancellationToken">Stops the download.</param>
    [Command("fetch")]
    public async Task FetchAsync(
        [Argument] string name,
        string? config = null,
        CancellationToken cancellationToken = default
    )
    {
        var loaded = ConfigLoader.Load(new ConfigLoadOptions { ConfigFile = config });
        if (loaded.Issues.Count > 0)
        {
            throw new ConfigException(string.Join('\n', loaded.Issues));
        }
        var docs = loaded.Config.Plugins.Docs;
        using var store = RtokStore.Open(new StoreOptions(loaded.Config.Core.DbPath));
        var service = new DocsService(
            store,
            new DocsConfig
            {
                QueryLimit = (int)Math.Min(docs.QueryLimit, int.MaxValue),
                SnippetChars = (int)Math.Min(docs.SnippetChars, int.MaxValue),
                MaxTokens = (int)Math.Min(docs.MaxTokens, int.MaxValue),
            },
            loaded.Locations.Home
        );
        using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"rtok/{BuildInfo.Version}");
        var line = await service
            .FetchAsync(
                name,
                Environment.CurrentDirectory,
                http,
                new DocsUpstream(),
                cancellationToken
            )
            .ConfigureAwait(false);
        await _stdout.WriteLineAsync(line).ConfigureAwait(false);
    }
}
