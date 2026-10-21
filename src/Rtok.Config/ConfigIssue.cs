using Pyrlyn.Config.Layered;

namespace Rtok.Config;

/// <summary>One problem with a config value: where it was set and what is wrong.</summary>
/// <param name="Key">The dotted key the value was set at; empty for a file that does not parse.</param>
/// <param name="Where">The place: <c>file:line</c>, <c>env RTOK_X</c>, <c>flag --x</c>.</param>
/// <param name="Message">What is wrong, naming the key.</param>
public sealed record ConfigIssue(string Key, string Where, string Message)
{
    /// <summary>The place a source names, as an issue prints it.</summary>
    /// <param name="source">The source of a value.</param>
    /// <returns><c>file:line</c> for files, <c>env NAME</c>, <c>flag --x</c>, or the layer name.</returns>
    public static string Locate(ConfigSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Kind switch
        {
            ConfigSourceKind.File or ConfigSourceKind.DotEnv when source.File is not null =>
                $"{source.File}:{Math.Max(source.Line, 1)}",
            ConfigSourceKind.Environment => $"env {source.Name}",
            ConfigSourceKind.Flag => $"flag {source.Name}",
            _ => source.Layer,
        };
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Where}: {Message}";
}
