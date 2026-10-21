namespace Rtok.Config;

/// <summary>The places a load read from.</summary>
/// <param name="Home">The rtok home.</param>
/// <param name="UserHome">The user home, or null when none is known.</param>
/// <param name="UserFile">The user config file, whether or not it exists.</param>
/// <param name="ProjectFile">The project's <c>.rtok.toml</c>, or null outside a git checkout.</param>
public sealed record ConfigLocations(
    string Home,
    string? UserHome,
    string UserFile,
    string? ProjectFile
);
