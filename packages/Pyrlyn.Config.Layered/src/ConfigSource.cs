namespace Pyrlyn.Config.Layered;

/// <summary>Where one value came from.</summary>
/// <param name="Layer">The layer's name, for example <c>default</c>, <c>user</c>, <c>project</c>, <c>env</c>.</param>
/// <param name="Kind">The layer's kind.</param>
/// <param name="File">The file, for <see cref="ConfigSourceKind.File"/> and <see cref="ConfigSourceKind.DotEnv"/>.</param>
/// <param name="Line">The 1-based line in <paramref name="File"/>, or 0.</param>
/// <param name="Name">The variable or flag, for the other kinds.</param>
public sealed record ConfigSource(
    string Layer,
    ConfigSourceKind Kind,
    string? File = null,
    int Line = 0,
    string? Name = null
)
{
    /// <summary>
    /// <c>default</c>, <c>user /home/u/.rtok/config.toml:12</c>,
    /// <c>dotenv /work/.env:3 RTOK_PROXY_PORT</c>, <c>env RTOK_PROXY_PORT</c>, <c>flag --port</c>.
    /// </summary>
    /// <returns>The layer name, then the location.</returns>
    public override string ToString()
    {
        var at =
            File is null ? null
            : Line > 0 ? $"{File}:{Line}"
            : File;
        return string.Join(' ', new[] { Layer, at, Name }.Where(s => !string.IsNullOrEmpty(s)));
    }
}
