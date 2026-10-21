namespace Rtok.Config.Tests;

public sealed class ConfigCliTests
{
    private const string Commented = """
        # my rtok settings
        [proxy]   # the proxy
        # port = 8790      # keep in sync with the IDE
        mode = "passthrough"   # not compress yet

        [plugins.read]
        # allow_paths = []
        max_chars = 30000 # big files

        [stats.prices."my-model"]
        input = 1.0
        """;

    [Fact]
    public async Task Show_with_sources_names_the_layer_of_every_value()
    {
        using var box = new Sandbox();
        box.Write(
            "user/.rtok/config.toml",
            "[proxy]\nport = 1001\n[otel]\nheaders = \"k=secret\"\n"
        );
        box.Write("project/.rtok.toml", "[web]\nport = 2002\n");
        box.Write("project/.env", "RTOK_LOG_LEVEL=debug\n");
        box.Env["RTOK_MCP_TOKEN"] = "secret";
        box.Flags.Add(new ConfigFlag("tui.tick_secs", 9L, "--tick"));

        var (code, stdout, stderr) = Run(box, cli => cli.Show(sources: true, json: false));

        Assert.Equal((0, ""), (code, stderr));
        Assert.DoesNotContain("secret", stdout, StringComparison.Ordinal);
        await Verify(box.Scrub(stdout));
    }

    [Fact]
    public async Task Show_json_lists_key_value_and_source()
    {
        using var box = new Sandbox();
        box.Write("user/.rtok/config.toml", "[proxy]\nport = 1001\n");

        var (_, stdout, _) = Run(box, cli => cli.Show(sources: false, json: true));

        await Verify(box.Scrub(string.Join('\n', stdout.Split('\n').Take(14))));
    }

    [Fact]
    public async Task Set_on_a_commented_file_changes_only_the_lines_of_the_keys_it_sets()
    {
        using var box = new Sandbox();
        box.Write("user/.rtok/config.toml", Commented);

        var outputs = new[]
        {
            Run(box, cli => cli.Set("proxy.port", "9000")),
            Run(box, cli => cli.Set("plugins.read.allow_paths", "[\"~/src\"]")),
            Run(box, cli => cli.Set("plugins.read.max_chars", "40000")),
            Run(box, cli => cli.Set("stats.prices.\"my-model\".output", "2")),
            Run(box, cli => cli.Set("web.host", "0.0.0.0")),
        };

        Assert.All(outputs, o => Assert.Equal((0, ""), (o.Code, o.Stderr)));
        var stdout = box.Scrub(string.Concat(outputs.Select(o => o.Stdout)));
        await Verify(
            $"{stdout}--- {Path.GetFileName(box.UserFile)}\n{File.ReadAllText(box.UserFile)}"
        );
    }

    [Fact]
    public void Set_on_the_reference_file_keeps_every_other_byte()
    {
        using var box = new Sandbox();
        Assert.Equal(0, Run(box, cli => cli.Init(force: false)).Code);
        var before = File.ReadAllLines(box.UserFile);

        Assert.Equal(0, Run(box, cli => cli.Set("proxy.port", "9000")).Code);

        var after = File.ReadAllLines(box.UserFile);
        Assert.Equal(before.Length, after.Length);
        var changed = Assert.Single(
            Enumerable.Range(0, before.Length),
            i => !string.Equals(before[i], after[i], StringComparison.Ordinal)
        );
        Assert.Equal(
            before[changed][2..].Replace("8790", "9000", StringComparison.Ordinal),
            after[changed]
        );
    }

    [Theory]
    [InlineData("proxy.port", "70000", "proxy.port out of range (1–65535)")]
    [InlineData("nope", "1", "unknown key: nope")]
    [InlineData("proxy..port", "1", "empty key")]
    [InlineData("proxy", "1", "proxy is a table; set one of its keys")]
    public void A_rejected_set_leaves_the_file_unchanged(string key, string value, string error)
    {
        using var box = new Sandbox();
        box.Write("user/.rtok/config.toml", Commented);

        var (code, stdout, stderr) = Run(box, cli => cli.Set(key, value));

        Assert.Equal((1, ""), (code, stdout));
        Assert.EndsWith(error + "\n", stderr, StringComparison.Ordinal);
        Assert.Equal(Commented, File.ReadAllText(box.UserFile));
    }

    [Fact]
    public void Set_notes_a_layer_that_overrides_the_file()
    {
        using var box = new Sandbox();
        box.Env["RTOK_PROXY_PORT"] = "9100";

        var (code, stdout, stderr) = Run(box, cli => cli.Set("proxy.port", "9000"));

        Assert.Equal(0, code);
        Assert.Equal("proxy.port = 9100\n", stdout);
        Assert.Equal("note: env RTOK_PROXY_PORT overrides the file\n", stderr);
        Assert.Contains(
            "\nport      = 9000 ",
            File.ReadAllText(box.UserFile),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Get_prints_a_value_or_a_table_with_secrets_redacted()
    {
        using var box = new Sandbox();
        box.Env["RTOK_OTEL_HEADERS"] = "k=v";

        Assert.Equal((0, "8790\n", ""), Run(box, cli => cli.Get("proxy.port", json: false)));
        Assert.Equal(
            (0, "[read, search, tree]\n", ""),
            Run(box, cli => cli.Get("mcp.http_tools", json: false))
        );
        Assert.Equal(
            (0, "<redacted>\n", ""),
            Run(box, cli => cli.Get("otel.headers", json: false))
        );
        Assert.Contains(
            "headers = <redacted>",
            Run(box, cli => cli.Get("otel", json: false)).Stdout,
            StringComparison.Ordinal
        );
        Assert.Equal(
            (1, "", "unknown key: proxy.nope\n"),
            Run(box, cli => cli.Get("proxy.nope", json: false))
        );
    }

    [Fact]
    public void Init_refuses_to_overwrite_without_force()
    {
        using var box = new Sandbox();

        Assert.Equal((0, box.UserFile + "\n", ""), Run(box, cli => cli.Init(force: false)));
        Assert.Equal(
            (1, "", $"{box.UserFile} exists; pass --force to overwrite\n"),
            Run(box, cli => cli.Init(force: false))
        );
        Assert.Equal(0, Run(box, cli => cli.Init(force: true)).Code);
        Assert.Equal(ConfigFile.Reference, File.ReadAllText(box.UserFile));
    }

    [Fact]
    public void Validate_creates_the_missing_user_file_and_lists_pinned_keys()
    {
        using var box = new Sandbox();

        Assert.Equal((0, $"ok {box.UserFile}\n", ""), Run(box, cli => cli.Validate(null)));

        box.Write("user/.rtok/config.toml", "[proxy]\nport = 8790\nmode = \"compress\"\n");
        Assert.Equal(
            (
                0,
                $"note {box.UserFile}:3: proxy.mode = compress (default passthrough)\nok {box.UserFile}\n",
                ""
            ),
            Run(box, cli => cli.Validate(null))
        );
    }

    [Fact]
    public void Validate_reports_every_problem_and_exits_one()
    {
        using var box = new Sandbox();
        var file = box.Write("bad.toml", "[proxy]\nport = 0\n[nope]\n");

        Assert.Equal(
            (1, "", $"{file}:2: proxy.port out of range (1–65535)\n{file}:3: unknown key: nope\n"),
            Run(box, cli => cli.Validate(file))
        );
        Assert.Equal(
            (1, "", $"{box.Root}/missing.toml: no such file\n"),
            Run(box, cli => cli.Validate(Path.Join(box.Root, "missing.toml")))
        );
    }

    private static (int Code, string Stdout, string Stderr) Run(
        Sandbox box,
        Func<ConfigCli, int> command
    )
    {
        using var stdout = new StringWriter { NewLine = "\n" };
        using var stderr = new StringWriter { NewLine = "\n" };
        var code = command(new ConfigCli(stdout, stderr, box.Options));
        return (code, stdout.ToString(), stderr.ToString());
    }
}
