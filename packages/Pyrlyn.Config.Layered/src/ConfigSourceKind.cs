namespace Pyrlyn.Config.Layered;

/// <summary>What kind of layer set a value.</summary>
public enum ConfigSourceKind
{
    /// <summary>Built-in defaults.</summary>
    Defaults,

    /// <summary>A TOML file; the source has its path and line.</summary>
    File,

    /// <summary>A <c>.env</c> file; the source has its path, line and variable name.</summary>
    DotEnv,

    /// <summary>A process environment variable; the source has its name.</summary>
    Environment,

    /// <summary>A command-line flag; the source has its name.</summary>
    Flag,
}
