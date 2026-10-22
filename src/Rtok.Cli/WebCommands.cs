using Rtok.Agents.Hosts;
using Rtok.Archive;
using Rtok.Cli.Ui;
using Rtok.Config;
using Rtok.Model;
using Rtok.Store;
using Rtok.Web;

namespace Rtok.Cli;

/// <summary><c>rtok web</c>: the dashboard, served until Ctrl-C.</summary>
internal static class WebCommands
{
    /// <summary>The session an expand from the dashboard is measured under.</summary>
    private const string WebSession = "web";

    /// <summary>Spells the hidden <c>dashboard</c> alias as <c>web</c>, with a notice on stderr.</summary>
    /// <param name="args">The command line.</param>
    /// <returns>The command line with <c>web</c> in place of <c>dashboard</c>.</returns>
    public static string[] Aliases(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args is not ["dashboard", ..])
        {
            return args;
        }
        Console.Error.WriteLine("rtok dashboard is now rtok web");
        return ["web", .. args[1..]];
    }

    /// <summary>Serve the web dashboard until Ctrl-C; prints its address with this run's token to stderr.</summary>
    /// <param name="cancellationToken">Ctrl-C.</param>
    /// <param name="host">The address to bind, an IP or localhost; [web] host when omitted.</param>
    /// <param name="port">The port, 0 for any free one; [web] port when omitted.</param>
    /// <param name="noToken">Serve /ws without the token; refused unless the host is loopback.</param>
    /// <param name="config">The config file to load instead of the user's.</param>
    public static async Task WebAsync(
        CancellationToken cancellationToken,
        string? host = null,
        ushort? port = null,
        bool noToken = false,
        string? config = null
    )
    {
        var load = new ConfigLoadOptions { ConfigFile = config };
        var settings = ConfigLoader.Load(load).Config;
        var (files, notice) = ClientFiles.Resolve(
            Environment.GetEnvironmentVariable(ClientFiles.DistVariable)
        );
        if (notice is not null)
        {
            Lines.Warn(notice);
        }
        using var store = RtokStore.Open(
            new StoreOptions(settings.Core.DbPath) { ArchiveDir = settings.Core.ArchiveDir }
        );
        var sources = ModelSources.Create(
            store,
            LinkedPlugins.Create,
            c => HostContexts.Of(c, null)
        );
        var options = new WebOptions(host ?? settings.Web.Host, port ?? settings.Web.Port, files)
        {
            NoToken = noToken,
            Expander = new Expander(store, settings, WebSession),
        };
        var server = await WebServer
            .StartAsync(
                options,
                sources,
                LinkedPlugins.Create().Select(p => p.Manifest.Id),
                load,
                cancellationToken
            )
            .ConfigureAwait(false);
        await using (server.ConfigureAwait(false))
        {
            await Console
                .Error.WriteLineAsync($"rtok web {server.OpenAddress}")
                .ConfigureAwait(false);
            await server.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
