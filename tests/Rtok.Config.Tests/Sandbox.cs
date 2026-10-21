using Rtok.Testing;

namespace Rtok.Config.Tests;

/// <summary>A user home, an rtok home and a git project in one temp directory, with an environment pointing at them.</summary>
internal sealed class Sandbox : IDisposable
{
    private readonly TempDir _dir = new();

    public Sandbox()
    {
        Directory.CreateDirectory(Path.Join(Project, ".git"));
        Directory.CreateDirectory(Home);
        Env["HOME"] = User;
        Env["RTOK_HOME"] = Home;
    }

    public string Root => _dir.Path;

    public string User => Path.Join(_dir.Path, "user");

    public string Home => Path.Join(User, ".rtok");

    public string Project => Path.Join(_dir.Path, "project");

    public string UserFile => Path.Join(Home, "config.toml");

    public Dictionary<string, string> Env { get; } = new(StringComparer.Ordinal);

    public List<ConfigFlag> Flags { get; } = [];

    public ConfigLoadOptions Options =>
        new()
        {
            Environment = Env,
            WorkingDirectory = Project,
            Flags = Flags,
        };

    public string Write(string relative, string text) => _dir.Write(relative, text);

    public LoadedConfig Load() => ConfigLoader.Load(Options);

    /// <summary>The text with this sandbox's paths replaced by placeholders, for goldens.</summary>
    public string Scrub(string text) => text.Replace(_dir.Path, "{root}", StringComparison.Ordinal);

    public void Dispose() => _dir.Dispose();
}
