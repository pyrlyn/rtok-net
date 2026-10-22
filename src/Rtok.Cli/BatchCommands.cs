using System.ComponentModel.DataAnnotations;
using ConsoleAppFramework;
using Rtok.Cli.Ui;
using Rtok.Config;
using Rtok.Proxy;

namespace Rtok.Cli;

/// <summary>Submit, check and fetch provider Batch jobs through the proxy; the key comes from ANTHROPIC_API_KEY or OPENAI_API_KEY.</summary>
internal sealed class BatchCommands
{
    private readonly TextWriter _stdout = Console.Out;

    /// <summary>Create a batch from a JSONL file of provider-shaped requests; prints the batch object.</summary>
    /// <param name="file">One request per line (Anthropic: custom_id + params; OpenAI: custom_id, method, url, body).</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <param name="provider">anthropic or openai; default: anthropic.</param>
    /// <param name="url">Proxy base URL; default: [proxy] bind and port.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    [Command("submit")]
    public Task<int> SubmitAsync(
        [Argument] string file,
        CancellationToken cancellationToken,
        string? provider = null,
        string? url = null,
        string? config = null
    ) =>
        RunAsync(
            null,
            provider,
            url,
            config,
            async api =>
                _stdout.WriteLine(
                    await api.SubmitAsync(file, cancellationToken).ConfigureAwait(false)
                )
        );

    /// <summary>Print the provider's current state of a batch.</summary>
    /// <param name="id">Batch id (msgbatch_… is Anthropic, batch_… OpenAI).</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <param name="provider">anthropic or openai; default: from the id.</param>
    /// <param name="url">Proxy base URL; default: [proxy] bind and port.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    [Command("status")]
    public Task<int> StatusAsync(
        [Argument] string id,
        CancellationToken cancellationToken,
        string? provider = null,
        string? url = null,
        string? config = null
    ) =>
        RunAsync(
            id,
            provider,
            url,
            config,
            async api =>
                _stdout.WriteLine(
                    await api.StatusAsync(id, cancellationToken).ConfigureAwait(false)
                )
        );

    /// <summary>Write the results of a finished batch to a new file.</summary>
    /// <param name="id">Batch id (msgbatch_… is Anthropic, batch_… OpenAI).</param>
    /// <param name="out">File to create; an existing file is never overwritten.</param>
    /// <param name="cancellationToken">Stops the download.</param>
    /// <param name="provider">anthropic or openai; default: from the id.</param>
    /// <param name="url">Proxy base URL; default: [proxy] bind and port.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    [Command("fetch")]
    public Task<int> FetchAsync(
        [Argument] string id,
        [Argument] string @out,
        CancellationToken cancellationToken,
        string? provider = null,
        string? url = null,
        string? config = null
    ) =>
        RunAsync(
            id,
            provider,
            url,
            config,
            async api =>
            {
                var bytes = await api.FetchAsync(id, @out, cancellationToken).ConfigureAwait(false);
                _stdout.WriteLine($"wrote {bytes} bytes to {@out}");
            }
        );

    /// <summary>The provider a flag names, else the one that issued <paramref name="id"/>,
    /// else Anthropic.</summary>
    internal static BatchProvider ProviderOf(string? flag, string? id) =>
        flag?.Trim().ToLowerInvariant() switch
        {
            null => id is null ? BatchProvider.Anthropic : BatchApi.OfId(id),
            "anthropic" => BatchProvider.Anthropic,
            "openai" => BatchProvider.Openai,
            _ => throw new ValidationException(
                $"--provider must be anthropic or openai, not {flag}"
            ),
        };

    private static async Task<int> RunAsync(
        string? id,
        string? provider,
        string? url,
        string? config,
        Func<BatchApi, Task> call
    )
    {
        var chosen = ProviderOf(provider, id);
        try
        {
            using var api = new BatchApi(
                url ?? ProxyUrls.Anthropic(Load(config).Proxy),
                chosen,
                Environment.GetEnvironmentVariable(BatchApi.KeyEnv(chosen))
            );
            await call(api).ConfigureAwait(false);
            return 0;
        }
        catch (Exception e) when (e is BatchException or IOException or UnauthorizedAccessException)
        {
            Lines.Error($"rtok batch: {e.Message}");
            return 1;
        }
    }

    private static RtokConfig Load(string? config)
    {
        var loaded = ConfigLoader.Load(new ConfigLoadOptions { ConfigFile = config });
        return loaded.Issues.Count > 0
            ? throw new ConfigException(string.Join('\n', loaded.Issues))
            : loaded.Config;
    }
}
