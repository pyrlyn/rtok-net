using Rtok.Config;
using Rtok.Testing;

namespace Rtok.Agents.Tests;

/// <summary>
/// The config rtok's own goldens run with: three priced models, and every host's folder pointed
/// at <c>tests/fixtures/usage-logs</c>, so a run never reaches this machine's agent folders.
/// </summary>
internal static class UsageFixture
{
    public static string Dir(string host) => RepoPaths.Fixture("usage-logs", host);

    public static RtokConfig Config(Action<AgentsUsageSection>? usage = null)
    {
        var config = new RtokConfig();
        config.Stats.TranscriptsDir = Dir("claude");
        config.Stats.CodexDir = Dir("codex");
        // A [stats.prices] table in a file adds to the default prices, as the loader merges it.
        config.Stats.Prices["claude-test"] = Price(1, 1.25, 0.1, 5);
        config.Stats.Prices["gpt-x"] = Price(2, 2, 0, 10);
        config.Stats.Prices["gemini-test"] = Price(1, 1, 0.25, 4);
        var dirs = config.Agents.Usage.Dirs;
        dirs.Opencode = [Dir("opencode")];
        dirs.Kilo = [Dir("kilo")];
        dirs.Copilot = [Dir("copilot")];
        dirs.Gemini = [Dir("gemini")];
        dirs.Droid = [Dir("droid")];
        dirs.Pi = [Dir("pi")];
        dirs.Kimi = [Dir("kimi")];
        dirs.Grok = [Dir("grok")];
        dirs.Zcode = [Dir("zcode")];
        dirs.Antigravity = [Dir("antigravity")];
        usage?.Invoke(config.Agents.Usage);
        return config;
    }

    public static ModelPrice Price(
        double input,
        double cacheWrite,
        double cacheRead,
        double output
    ) =>
        new()
        {
            Input = input,
            CacheWrite = cacheWrite,
            CacheRead = cacheRead,
            Output = output,
        };

    /// <summary>A zone by IANA id, or null where the platform cannot resolve one (the test then skips).</summary>
    public static TimeZoneInfo? Zone(string id) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) ? zone : null;
}
