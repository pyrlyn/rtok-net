using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ConsoleAppFramework;
using Rtok.Cli.Ui;
using Rtok.Config;
using Rtok.Core;
using Rtok.Process;
using Rtok.Proxy;
using Rtok.Store;

namespace Rtok.Cli;

/// <summary><c>rtok proxy</c> and <c>rtok wrap</c>: the API proxy and a command run behind it.</summary>
internal static class ProxyCommands
{
    /// <summary>How long <c>wrap</c> waits for a running proxy's <c>/health</c>.</summary>
    private static readonly TimeSpan HealthWait = TimeSpan.FromMilliseconds(500);

    /// <summary>Run the API proxy in front of Anthropic, OpenAI and Gemini and record real token usage.</summary>
    /// <param name="cancellationToken">Ctrl+C stops it.</param>
    /// <param name="port">Listen on this port instead of [proxy] port.</param>
    /// <param name="upstream">Forward to this URL instead of [proxy] upstream.</param>
    /// <param name="mode">passthrough or compress, instead of [proxy] mode.</param>
    /// <param name="dryRun">Print the effective [proxy] settings and exit.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    public static async Task<int> ProxyAsync(
        CancellationToken cancellationToken,
        ushort? port = null,
        string? upstream = null,
        string? mode = null,
        bool dryRun = false,
        string? config = null
    )
    {
        var cfg = Load(config, Flags(port, upstream, mode, dryRun));
        if (cfg.Proxy.DryRun)
        {
            Console.Out.Write(DryRun(cfg.Proxy));
            return 0;
        }
        // S5: a proxy other hosts can reach shows /health and /live only to this run's token.
        var token = IsLoopback(cfg.Proxy) ? null : AccessToken.New();
        await using var server = await StartAsync(cfg, token, cancellationToken)
            .ConfigureAwait(false);
        Lines.Info($"rtok proxy: listening on {server.Address}");
        if (token is not null)
        {
            // Once, on stderr, never on disk.
            await Console
                .Error.WriteLineAsync(
                    $"rtok proxy: /health and /live from other hosts need ?token={token}"
                )
                .ConfigureAwait(false);
        }
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        return 0;
    }

    /// <summary>Run a command with its API base URLs on the proxy, starting one when none answers.</summary>
    /// <param name="context">The arguments after <c>--</c> are the command.</param>
    /// <param name="cancellationToken">Stops the command.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    /// <param name="command">The command, when it starts with no option; otherwise after --.</param>
    public static async Task<int> WrapAsync(
        ConsoleAppContext context,
        CancellationToken cancellationToken,
        string? config = null,
        [Argument] params string[] command
    )
    {
        string[] words = [.. command, .. context.EscapedArguments];
        if (words.Length == 0)
        {
            throw new ValidationException("missing command: rtok wrap -- <command>");
        }
        var cfg = Load(config, []);
        // Fail open: a proxy that cannot start leaves the command its own environment.
        ProxyServer? started = null;
        if (!await AnswersAsync(cfg.Proxy, cancellationToken).ConfigureAwait(false))
        {
            try
            {
                started = await StartAsync(cfg, null, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Lines.Warn($"rtok wrap: proxy not started ({e.Message}); running without it");
            }
        }
        await using (started)
        {
            var runner = ProgressRunner.Quiet(words[0], words[1..]).Output(OutputMode.Inherit);
            if (
                started is not null
                || await AnswersAsync(cfg.Proxy, cancellationToken).ConfigureAwait(false)
            )
            {
                runner = runner
                    .Environment("ANTHROPIC_BASE_URL", ProxyUrls.Anthropic(cfg.Proxy))
                    .Environment("OPENAI_BASE_URL", ProxyUrls.OpenAi(cfg.Proxy));
            }
            try
            {
                return (await runner.RunAsync(cancellationToken).ConfigureAwait(false)).ExitCode;
            }
            catch (Win32Exception e)
            {
                Lines.Error($"rtok wrap: {words[0]}: {e.Message}");
                return 127;
            }
        }
    }

    /// <summary>The <c>--dry-run</c> listing, one <c>key = value</c> per line as rtok printed it.</summary>
    internal static string DryRun(ProxySection proxy) =>
        $"""
            bind = {proxy.Bind}
            port = {proxy.Port}
            mode = {proxy.Mode}
            upstream = {proxy.Upstream}
            openai_upstream = {proxy.OpenaiUpstream}
            timeout_s = {proxy.TimeoutS}
            include_usage = {(proxy.IncludeUsage ? "true" : "false")}
            dry_run = {(proxy.DryRun ? "true" : "false")}

            """;

    private static List<ConfigFlag> Flags(ushort? port, string? upstream, string? mode, bool dryRun)
    {
        var flags = new List<ConfigFlag>();
        if (port is { } p)
        {
            flags.Add(new ConfigFlag("proxy.port", (long)p, "--port"));
        }
        if (upstream is not null)
        {
            flags.Add(new ConfigFlag("proxy.upstream", upstream, "--upstream"));
        }
        if (mode is not null)
        {
            flags.Add(new ConfigFlag("proxy.mode", mode, "--mode"));
        }
        if (dryRun)
        {
            flags.Add(new ConfigFlag("proxy.dry_run", true, "--dry-run"));
        }
        return flags;
    }

    private static RtokConfig Load(string? config, IReadOnlyList<ConfigFlag> flags)
    {
        var loaded = ConfigLoader.Load(
            new ConfigLoadOptions { ConfigFile = config, Flags = flags }
        );
        if (loaded.Issues.Count > 0)
        {
            throw new ConfigException(string.Join('\n', loaded.Issues));
        }
        return loaded.Config;
    }

    private static async Task<ProxyServer> StartAsync(
        RtokConfig cfg,
        string? token,
        CancellationToken cancellationToken
    )
    {
        var options = new ProxyOptions(cfg)
        {
            Logger = Serilog.Log.Logger,
            AccessToken = token,
            Store = RtokStore.Open(
                new StoreOptions(cfg.Core.DbPath)
                {
                    ArchiveDir = cfg.Core.ArchiveDir,
                    CallIoInlineBytes = (int)Math.Min(cfg.Core.CallIoInlineBytes, int.MaxValue),
                    StoreRaw = cfg.Core.StoreRaw,
                }
            ),
        };
        try
        {
            return await ProxyServer.StartAsync(options, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            options.Store.Dispose();
            throw;
        }
    }

    /// <summary>Whether an rtok proxy already answers <c>/health</c> where the config points.</summary>
    private static async Task<bool> AnswersAsync(
        ProxySection proxy,
        CancellationToken cancellationToken
    )
    {
        using var client = new HttpClient { Timeout = HealthWait };
        try
        {
            using var response = await client
                .GetAsync(new Uri(ProxyUrls.Root(proxy) + "/health"), cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }
            var body = await response
                .Content.ReadAsByteArrayAsync(cancellationToken)
                .ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("ok", out var ok)
                && ok.ValueKind == JsonValueKind.True;
        }
        catch (Exception e)
            when (e
                    is HttpRequestException
                        or TaskCanceledException
                        or JsonException
                        or UriFormatException
            )
        {
            return false;
        }
    }

    private static bool IsLoopback(ProxySection proxy) =>
        proxy.Bind.Trim().Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || (
            System.Net.IPAddress.TryParse(proxy.Bind.Trim(), out var ip)
            && System.Net.IPAddress.IsLoopback(ip)
        );
}
