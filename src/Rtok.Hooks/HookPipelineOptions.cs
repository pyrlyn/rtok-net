using Rtok.Plugin.Sdk;

namespace Rtok.Hooks;

/// <summary>What a <see cref="HookPipeline"/> runs with.</summary>
public sealed record HookPipelineOptions
{
    /// <summary>The plugins, created once per event.</summary>
    public Func<IEnumerable<IPlugin>> Plugins { get; init; } = () => [];

    /// <summary>Where the over-cap, locked-store and slow-event lines go.</summary>
    public TextWriter Errors { get; init; } = TextWriter.Null;

    /// <summary>The process environment, for a hook run in place.</summary>
    public Func<string, string?> Environment { get; init; } =
        System.Environment.GetEnvironmentVariable;

    /// <summary>The environment the configuration reads; null reads the process's.</summary>
    public IReadOnlyDictionary<string, string>? ConfigEnvironment { get; init; }

    /// <summary>The <c>rtok</c> a deferred SessionEnd runs.</summary>
    public string? Executable { get; init; } = System.Environment.ProcessPath;

    /// <summary><c>--config</c>, when given.</summary>
    public string? ConfigFile { get; init; }

    /// <summary>Times the event for the slow note.</summary>
    public TimeProvider Time { get; init; } = TimeProvider.System;
}
