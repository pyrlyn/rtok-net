namespace Rtok.Plugin.Sdk;

/// <summary>
/// The host, as every plugin sees it: estimate what text will cost, record what was saved, say
/// what happened, and read configuration. Stored state is behind the narrow capability
/// interfaces (<see cref="IArchive"/>, <see cref="INotes"/>, <see cref="IReadCache"/>,
/// <see cref="ILedger"/>, <see cref="ISymbols"/>); a method exists there because a plugin calls
/// it, not as a general database API.
/// </summary>
/// <remarks>
/// Every member may be called from any thread. Failures are thrown; the host isolates each
/// plugin call, so a plugin that cannot reach the store still fails open.
/// </remarks>
public interface IHost
{
    /// <summary>The host session this dispatch belongs to. Every row a plugin writes is
    /// attributed to it.</summary>
    string Session { get; }

    /// <summary>Working directory of this dispatch, when the surface knows it. Hook events carry
    /// one; MCP and the CLI typically do not.</summary>
    string? Cwd => null;

    /// <summary>The <c>calls</c> row this dispatch runs under, when the surface has one.</summary>
    long? CallId => null;

    /// <summary>Estimated token count for <paramref name="text"/>. No tokenizer, no network:
    /// safe on the hot path.</summary>
    int Estimate(string text, TextClass textClass);

    /// <summary>Persist a <see cref="Measurement"/>. The only path a saving has into the host's
    /// numbers: what is not recorded here did not happen.</summary>
    void Record(Measurement measurement);

    /// <summary>Keep one small value about the plugin's own work under <paramref name="key"/>
    /// in <paramref name="plugin"/>'s namespace; a repeat key replaces it. For signals that are
    /// not savings and so must never reach <see cref="Record"/>. A host without a store drops
    /// the value.</summary>
    void PluginStateSet(string plugin, string key, string value) { }

    /// <summary>The value <see cref="PluginStateSet"/> kept under <paramref name="key"/>, if
    /// any.</summary>
    string? PluginStateGet(string plugin, string key) => null;

    /// <summary>Open a <c>calls</c> row for one unit of work on <paramref name="surface"/> and
    /// return its id.</summary>
    long RecordCall(string surface, string kind, string? name);

    /// <summary>Open a <c>plugin_run</c> row for <paramref name="plugin"/>, nested under the
    /// current call when there is one.</summary>
    long RecordPluginRun(string surface, string plugin);

    /// <summary>Attribute <paramref name="tokens"/> to a call: the before and after phases of a
    /// plugin's own work.</summary>
    void RecordTokens(long callId, string? plugin, string phase, string source, long tokens);

    /// <summary>Append a log line. Never throws: a plugin cannot be made to care where logs
    /// go.</summary>
    void Log(string level, string source, string name, string message);

    /// <summary>One section of the host's configuration as JSON, by dotted path
    /// (<c>plugins.read</c>, <c>proxy</c>, <c>estimator</c>), or <see langword="null"/> when the
    /// host has no such section. A plugin reads its own settings through
    /// <see cref="Ctx.PluginConfig{T}"/>; this is the way out for a host-wide value.</summary>
    System.Text.Json.JsonElement? ConfigJson(string path);

    /// <summary>Watcher debounce window of <paramref name="root"/>: relative paths not yet
    /// re-indexed.</summary>
    IReadOnlyList<string> GraphWatchPending(string root) => [];

    /// <summary>Publish <paramref name="root"/>'s in-flight pending set for staleness
    /// banners.</summary>
    void PublishGraphWatchPending(string root, IReadOnlyList<string> paths) { }
}
