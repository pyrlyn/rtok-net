using ConsoleAppFramework;
using Rtok.Archive;
using Rtok.Cli.Ui;
using Rtok.Config;
using Rtok.Store;

namespace Rtok.Cli;

/// <summary><c>rtok expand</c>: the original behind an <c>expand &lt;id&gt;</c> trailer.</summary>
internal static class ExpandCommand
{
    /// <summary>Print an archived original byte for byte, or the lines, grep hits or context you ask for.</summary>
    /// <param name="id">The id an "expand" trailer names: 8 or more hex characters.</param>
    /// <param name="lines">Only lines a-b, 1-based and inclusive.</param>
    /// <param name="grep">Only lines matching this regex, printed N:line.</param>
    /// <param name="context">Lines of context around each --grep hit.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    public static int Run(
        [Argument] string id,
        string? lines = null,
        string? grep = null,
        uint context = 0,
        string? config = null
    )
    {
        var loaded = ConfigLoader.Load(new ConfigLoadOptions { ConfigFile = config });
        if (loaded.Issues.Count > 0)
        {
            throw new ConfigException(string.Join('\n', loaded.Issues));
        }
        var cfg = loaded.Config;
        using var store = RtokStore.Open(
            new StoreOptions(cfg.Core.DbPath) { ArchiveDir = cfg.Core.ArchiveDir }
        );
        var result = new Expander(store, cfg, Expander.CliSession).Run(
            new ExpandRequest(id, lines, grep, (int)Math.Min(context, int.MaxValue))
        );
        if (!result.Ok)
        {
            Lines.Error($"Error: {result.Error}");
            return 1;
        }
        using var stdout = Console.OpenStandardOutput();
        stdout.Write(result.Body);
        return 0;
    }
}
