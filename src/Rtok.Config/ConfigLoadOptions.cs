namespace Rtok.Config;

/// <summary>What a load reads besides the files it finds itself.</summary>
public sealed record ConfigLoadOptions
{
    /// <summary>The <c>--config</c> file, or null for <c>RTOK_CONFIG</c> or <c>&lt;home&gt;/config.toml</c>.</summary>
    public string? ConfigFile { get; init; }

    /// <summary>Where the project and <c>.env</c> search start; null is the current directory.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>The environment; null reads the process environment.</summary>
    public IReadOnlyDictionary<string, string>? Environment { get; init; }

    /// <summary>Command-line flags, the last layer.</summary>
    public IReadOnlyList<ConfigFlag> Flags { get; init; } = [];

    /// <summary>When false, only the defaults and the user file are read (<c>rtok config validate</c>).</summary>
    public bool ReadProjectAndEnvironment { get; init; } = true;

    /// <summary>The user file's text to read instead of the file, for checking an edit before it is written.</summary>
    internal string? UserText { get; init; }

    internal Func<string, string?> Lookup =>
        Environment is { } vars
            ? name => vars.TryGetValue(name, out var value) ? value : null
            : System.Environment.GetEnvironmentVariable;
}
