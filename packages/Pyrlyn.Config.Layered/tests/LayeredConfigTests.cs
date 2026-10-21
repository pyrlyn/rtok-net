using Tomlyn.Model;

namespace Pyrlyn.Config.Layered.Tests;

public sealed class LayeredConfigTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pyrlyn-layered-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static TomlTable Defaults() =>
        TomlText.Parse(
            """
            [proxy]
            port = 8080
            openai_upstream = "https://api.openai.com"
            [hook]
            enabled = true
            [stats]
            ratio = 0.5
            [log]
            level = "info"
            [core]
            roots = ["a"]
            name = "rtok"
            """
        );

    private string Write(string name, string text)
    {
        var path = Path.Join(_dir, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void EveryKeyReportsTheLayerThatSetIt()
    {
        var user = Write("config.toml", "# mine\n\n[proxy]\nport = 9000\n");
        var project = Write("work/.rtok.toml", "[log]\nlevel = \"debug\"\n");
        var dotenv = Write(
            "work/.env",
            "# vars\nRTOK_PROXY_OPENAI_UPSTREAM=http://local\nOTHER=1\n"
        );

        var config = new LayeredConfigBuilder("RTOK_")
            .AddDefaults(Defaults())
            .AddTomlFile("user", user)
            .AddTomlFile("project", project)
            .AddDotEnv("dotenv", [dotenv])
            .AddEnvironment(
                "env",
                [new("RTOK_HOOK_ENABLED", "false"), new("RTOK_HOME", "/x"), new("PATH", "/bin")]
            )
            .AddFlag("flag", "stats.ratio", 0.9, "--ratio")
            .Build();

        var rows = config.Entries.Select(e => $"{e.Key} = {TomlText.Format(e.Value)} ({e.Source})");
        Assert.Equal(
            [
                "core.name = \"rtok\" (default)",
                "core.roots = [\"a\"] (default)",
                "hook.enabled = false (env RTOK_HOOK_ENABLED)",
                $"log.level = \"debug\" (project {project}:2)",
                $"proxy.openai_upstream = \"http://local\" (dotenv {dotenv}:2 RTOK_PROXY_OPENAI_UPSTREAM)",
                $"proxy.port = 9000 (user {user}:4)",
                "stats.ratio = 0.9 (flag --ratio)",
            ],
            rows,
            StringComparer.Ordinal
        );
        Assert.True(config.TryGet("proxy.port", out var port));
        Assert.Equal(ConfigSourceKind.File, port.Source.Kind);
        Assert.Equal(9000L, port.Value);
        Assert.Empty(config.Warnings);
    }

    [Fact]
    public void LaterLayersWinAndTablesMergeKeyByKey()
    {
        var config = new LayeredConfigBuilder("RTOK_")
            .AddDefaults(Defaults())
            .AddToml("user", "proxy = { port = 1 }\n", "u.toml")
            .AddToml("project", "[proxy]\nport = 2\n", "p.toml")
            .Build();

        Assert.Equal(2L, config.Find("proxy.port"));
        Assert.Equal("https://api.openai.com", config.Find("proxy.openai_upstream"));
        Assert.True(config.TryGet("proxy.port", out var e));
        Assert.Equal("project p.toml:2", e.Source.ToString());
    }

    [Fact]
    public void EnvValuesTakeTheTypeOfTheirDefault()
    {
        var config = new LayeredConfigBuilder("RTOK_")
            .AddDefaults(Defaults())
            .AddEnvAlias("RTOK_UPSTREAM", "proxy.openai_upstream")
            .AddEnvironment(
                "env",
                [
                    new("rtok_proxy_port", "7000"),
                    new("RTOK_STATS_RATIO", "0.25"),
                    new("RTOK_CORE_ROOTS", " x, ,y "),
                    new("RTOK_CORE_NAME", "123"),
                    new("RTOK_UPSTREAM", "http://alias"),
                    new("RTOK_HOOK_ENABLED", "maybe"),
                ]
            )
            .Build();

        Assert.Equal(7000L, config.Find("proxy.port"));
        Assert.Equal(0.25, config.Find("stats.ratio"));
        Assert.Equal(
            ["x", "y"],
            ((TomlArray)config.Find("core.roots")!).Cast<string>(),
            StringComparer.Ordinal
        );
        Assert.Equal("123", config.Find("core.name"));
        Assert.Equal("http://alias", config.Find("proxy.openai_upstream"));
        // Left as a string for the caller's schema to reject, with its source.
        Assert.True(config.TryGet("hook.enabled", out var e));
        Assert.Equal("maybe", e.Value);
        Assert.Equal("env RTOK_HOOK_ENABLED", e.Source.ToString());
    }

    [Fact]
    public void AnEmptyEnvListIsEmptyNotOneBlankItem()
    {
        var config = new LayeredConfigBuilder("RTOK_")
            .AddDefaults(Defaults())
            .AddEnvironment("env", [new("RTOK_CORE_ROOTS", "")])
            .Build();

        Assert.Empty((TomlArray)config.Find("core.roots")!);
    }

    [Fact]
    public void TheFirstDotenvFileWinsAndAMalformedOneIsAWarning()
    {
        var near = Write(
            "a/.env",
            "RTOK_LOG_LEVEL=warn\nRTOK_PROXY_PORT=1\nnot a line\nRTOK_CORE_NAME=late\n"
        );
        var home = Write(".env", "RTOK_LOG_LEVEL=error\nRTOK_CORE_NAME='home # name'\n");

        var config = new LayeredConfigBuilder("RTOK_")
            .AddDefaults(Defaults())
            .AddDotEnv("dotenv", [near, Path.Join(_dir, "missing.env"), home])
            .Build();

        Assert.Equal("warn", config.Find("log.level"));
        Assert.Equal(1L, config.Find("proxy.port"));
        Assert.Equal("home # name", config.Find("core.name"));
        var warning = Assert.Single(config.Warnings);
        Assert.StartsWith($"{near}:3: ", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void ALeafReplacedByATableDropsTheOldSource()
    {
        var config = new LayeredConfigBuilder("RTOK_")
            .AddToml("user", "a = 1\n", "u.toml")
            .AddToml("project", "[a]\nb = 2\n", "p.toml")
            .Build();

        Assert.Equal(["a.b"], config.Entries.Select(e => e.Key), StringComparer.Ordinal);
        Assert.False(config.TryGet("a", out _));
    }

    [Fact]
    public void ATomlErrorNamesTheFileAndLine()
    {
        var bad = Write("bad.toml", "[a]\nx = \n");
        var e = Assert.Throws<ConfigFileException>(() =>
            new LayeredConfigBuilder("RTOK_").AddTomlFile("user", bad)
        );
        Assert.Equal(bad, e.File);
        Assert.Equal(2, e.Line);
    }

    [Fact]
    public void KeysThatNeedQuotesRoundTrip()
    {
        var config = new LayeredConfigBuilder("RTOK_")
            .AddToml("user", "[graph.\"my.proj\"]\nroot = \"/w\"\n")
            .Build();

        Assert.Equal(
            ["graph.\"my.proj\".root"],
            config.Entries.Select(e => e.Key),
            StringComparer.Ordinal
        );
        Assert.Equal("/w", config.Find("graph.\"my.proj\".root"));
        Assert.Equal(["graph", "my.proj", "root"], KeyPath.Split("graph.\"my.proj\".root"));
    }

    [Fact]
    public void ArraysOfTablesAreOneLeaf()
    {
        var config = new LayeredConfigBuilder("RTOK_")
            .AddToml("user", "[[rules]]\nname = \"a\"\n\n[[rules]]\nname = \"b\"\n", "u.toml")
            .Build();

        var e = Assert.Single(config.Entries);
        Assert.Equal("rules", e.Key);
        Assert.Equal(1, e.Source.Line);
        Assert.Equal(2, ((TomlTableArray)e.Value).Count);
    }

    [Fact]
    public void FindUpSkipsADirectoryNamedLikeTheFile()
    {
        Write("x/.env", "A=1\n");
        Directory.CreateDirectory(Path.Join(_dir, "x/y/.env"));
        Directory.CreateDirectory(Path.Join(_dir, "x/y/z"));

        Assert.Equal(Path.Join(_dir, "x", ".env"), DotEnv.FindUp(Path.Join(_dir, "x/y/z")));
    }

    [Theory]
    [InlineData("A=1", "1")]
    [InlineData("export A = two words # note", "two words")]
    [InlineData("A=\"a\\nb # kept\" # note", "a\nb # kept")]
    [InlineData("A='$literal\\n'", "$literal\\n")]
    [InlineData("A=\"line1\nline2\"", "line1\nline2")]
    [InlineData("A=", "")]
    public void DotEnvValues(string text, string expected)
    {
        var (vars, error) = DotEnv.Parse(text, ".env");
        Assert.Null(error);
        Assert.Equal(expected, Assert.Single(vars).Value);
    }

    [Theory]
    [InlineData("A=\"open", 1)]
    [InlineData("A=1\n=2", 2)]
    [InlineData("A='x' y", 1)]
    public void DotEnvErrors(string text, int line)
    {
        var (_, error) = DotEnv.Parse(text, ".env");
        Assert.Equal(line, error!.Line);
    }
}
