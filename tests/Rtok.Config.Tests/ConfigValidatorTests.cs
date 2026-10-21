namespace Rtok.Config.Tests;

public sealed class ConfigValidatorTests
{
    [Theory]
    [InlineData("nope = 1", "unknown key: nope")]
    [InlineData("[proxy]\nnope = 1", "unknown key: proxy.nope")]
    [InlineData("proxy = 1", "proxy: expected table")]
    [InlineData("[proxy.port]\nx = 1", "proxy.port: expected number")]
    [InlineData("[proxy]\nport = 0", "proxy.port out of range (1–65535)")]
    [InlineData("[web]\nport = 70000", "web.port out of range (1–65535)")]
    [InlineData("[proxy]\nmode = \"fast\"", "proxy.mode must be passthrough or compress")]
    [InlineData("[proxy.flex]\nretries = 6", "proxy.flex.retries must be ≤ 5")]
    [InlineData(
        "[proxy.flex]\non_429 = \"x\"",
        "proxy.flex.on_429: expected one of none | backoff | default"
    )]
    [InlineData("[log]\nlevel = \"loud\"", "log.level must be error, warn, info, or debug")]
    [InlineData("[log]\nmax_bytes = 10", "log.max_bytes must be ≥ 1024")]
    [InlineData("[log]\nfiles = -1", "log.files must be ≥ 0")]
    [InlineData("[log]\nto_db = \"yes\"", "log.to_db: expected bool")]
    [InlineData("[log]\ntspin = \"never\"", "log.tspin: expected one of auto | always | off")]
    [InlineData("[plugins.read]\nmax_chars = 99", "plugins.read.max_chars must be ≥ 100")]
    [InlineData(
        "[plugins.read]\ndelta_max_ratio = 1.5",
        "plugins.read.delta_max_ratio must be in (0, 1]"
    )]
    [InlineData(
        "[plugins.read]\ndelta_max_ratio = 0",
        "plugins.read.delta_max_ratio must be in (0, 1]"
    )]
    [InlineData(
        "[plugins.read]\ndefault_mode = \"x\"",
        "plugins.read.default_mode must be full, lines, map, or signatures"
    )]
    [InlineData(
        "[plugins.read]\nallow_paths = [1]",
        "plugins.read.allow_paths[0]: expected string"
    )]
    [InlineData(
        "[plugins.proxy.semantic_cache]\nembed_backend = \"openai\"",
        "plugins.proxy.semantic_cache.embed_backend: expected one of hash"
    )]
    [InlineData("[plugins.graph]\nwatch = \"poll\"", "plugins.graph.watch must be off or notify")]
    [InlineData(
        "[plugins.graph.extensions]\nfoo = \"cobol\"",
        "plugins.graph.extensions.foo must be one of rust, ts, tsx, js, mjs, cjs, py, dart, c, h, go (got cobol)"
    )]
    [InlineData(
        "[plugins.graph.backend_by_language]\ngo = \"x\"",
        "plugins.graph.backend_by_language.go must be one of tags, lsp, auto, text (got x)"
    )]
    [InlineData(
        "[plugins.compress.llm]\nurl = \"ftp://x\"",
        "plugins.compress.llm.url must be empty or an http(s) URL"
    )]
    [InlineData("[tasks]\nadapter = \"jira\"", "tasks.adapter must be one of disk, github, gitlab")]
    [InlineData(
        "[tasks]\nprefix = \"T9\"",
        "tasks.prefix: task prefix \"T9\" must be 1 to 8 ASCII letters"
    )]
    [InlineData("[stats]\nsince = \"3w\"", "stats.since: unknown unit \"w\" in \"3w\"; use d or h")]
    [InlineData(
        "[agents.junk]\nkeep_logs_days = 4000",
        "agents.junk.keep_logs_days must be ≤ 3650"
    )]
    [InlineData(
        "[agents.junk]\nexclude = [\"a[b\"]",
        "agents.junk.exclude[0]: unclosed character class; missing ']'"
    )]
    [InlineData(
        "[[agents.junk.extra]]\nhost = \"x\"\nkind = \"cache\"\npath = \"/p\"",
        "agents.junk.extra[0].host must be rtok or a host id (see `rtok agents list`)"
    )]
    [InlineData(
        "[[agents.junk.extra]]\nhost = \"rtok\"\nkind = \"junk\"\npath = \"/p\"",
        "agents.junk.extra[0].kind must be one of cache, temp, logs, crash-dumps"
    )]
    [InlineData(
        "[[agents.junk.extra]]\nhost = \"claude\"\nkind = \"temp\"\npath = \"\"",
        "agents.junk.extra[0].path must be a non-empty path"
    )]
    [InlineData(
        "[[agents.junk.extra]]\nhost = \"claude\"\nkind = \"temp\"\npath = \"/p\"\nx = 1",
        "unknown key: agents.junk.extra[0].x"
    )]
    [InlineData("[stats.prices.m]\ninput = \"cheap\"", "stats.prices.m.input: expected number")]
    [InlineData("[stats.prices.m]\nfree = 1", "unknown key: stats.prices.m.free")]
    public void A_bad_value_is_reported_with_its_file_and_line(string toml, string message)
    {
        using var box = new Sandbox();
        var file = box.Write("c.toml", toml + "\n");

        var issue = Assert.Single(ConfigLoader.LoadFile(file, box.Options).Issues);

        Assert.Equal(message, issue.Message);
        Assert.StartsWith(file + ":", issue.Where, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[plugins.read]\ndelta_max_ratio = 1")]
    [InlineData("[stats]\nsince = \"12h\"")]
    [InlineData("[tasks]\nprefix = \"\"")]
    [InlineData("[log]\nlevel = \"WARN\"")]
    [InlineData("[proxy.lanes.bulk]\nupstream = \"https://x.test\"")]
    [InlineData("[[agents.junk.extra]]\nhost = \"cursor\"\nkind = \"crash-dumps\"\npath = \"~/x\"")]
    [InlineData("[setup]\nsource = \"/src/rtok\"")]
    [InlineData("[bench.configs]\nc = \"bench/configs/c.json\"")]
    public void A_good_value_passes(string toml)
    {
        using var box = new Sandbox();
        var file = box.Write("c.toml", toml + "\n");

        Assert.Empty(ConfigLoader.LoadFile(file, box.Options).Issues);
    }
}
