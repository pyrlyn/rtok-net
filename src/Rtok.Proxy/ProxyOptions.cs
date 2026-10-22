using Rtok.Config;
using Rtok.Store;
using Serilog;

namespace Rtok.Proxy;

/// <summary>What <see cref="ProxyServer"/> runs with.</summary>
/// <param name="Config">The loaded config; <c>[proxy]</c> binds and routes.</param>
public sealed record ProxyOptions(RtokConfig Config)
{
    /// <summary>rtok's request and body cap, and how much of a response the tee keeps for
    /// recording: both exist to bound memory, not to turn traffic away.</summary>
    public const int MaxBodyBytes = 256 * 1024 * 1024;

    /// <summary>Where calls, usage and log rows go; null records nothing (plain mode needs
    /// none).</summary>
    public RtokStore? Store { get; init; }

    /// <summary>rtok's log; its events reach the store through the logger's own sink.</summary>
    public ILogger Logger { get; init; } = Serilog.Core.Logger.None;

    /// <summary>The response bytes the tee keeps for recording; the client always gets all.</summary>
    public int TeeBytes { get; init; } = MaxBodyBytes;

    /// <summary>The <c>SSL_CERT_FILE</c> bundle; null or empty trusts the platform store only.</summary>
    public string? CertFile { get; init; } =
        Environment.GetEnvironmentVariable(ProxyTls.CertFileVariable);

    /// <summary>The token a client other than loopback presents for <c>/health</c> and
    /// <c>/live</c> (rtok S5); null refuses such clients.</summary>
    public string? AccessToken { get; init; }

    /// <summary>The clock Flex retries wait on; tests pass a fake one.</summary>
    public TimeProvider Time { get; init; } = TimeProvider.System;

    /// <summary>The upstream handler, for tests that route it elsewhere; null builds the TLS one.</summary>
    public HttpMessageHandler? Upstream { get; init; }

    /// <summary>A kill switch is off (<c>[proxy] enabled</c>, <c>[core] enabled</c> or
    /// <c>[plugins.proxy] enabled</c>): bytes forward untouched and nothing is recorded. The
    /// plugin switch counts because the plugin is the usage capture.</summary>
    public bool Plain =>
        !Config.Proxy.Enabled || !Config.Core.Enabled || !Config.Plugins.Proxy.Enabled;
}
