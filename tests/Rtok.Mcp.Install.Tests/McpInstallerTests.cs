using Rtok.Agent.Sdk;
using Rtok.Testing;

namespace Rtok.Mcp.Install.Tests;

public sealed class McpInstallerTests
{
    public static TheoryData<McpFormat, string, string> Cases =>
        new()
        {
            { McpFormat.Json, "mcpServers", "" },
            { McpFormat.Json, "mcp.servers", "{\n  \"other\": 1\n}\n" },
            { McpFormat.Jsonc, "mcpServers", "{\n  // keep me\n  \"other\": 1,\n}\n" },
            { McpFormat.Jsonc, "mcp.servers", "" },
            { McpFormat.Toml, "mcp_servers", "" },
            { McpFormat.Toml, "mcp.servers", "# keep me\nmodel = \"x\"\n" },
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Install_then_remove_leaves_the_file_as_it_was(
        McpFormat format,
        string keyPath,
        string existing
    )
    {
        var spec = Specs.Of(format, keyPath);
        var fs = MemFs.Linux();
        var disk = new McpDisk(fs);
        if (existing.Length > 0)
        {
            fs.Directory.CreateDirectory("/home/u/.config/host");
            fs.File.WriteAllText(Specs.File, existing);
        }

        Assert.Equal($"{spec.At}: rtok mcp", Specs.Run(spec, McpMode.Install, disk));
        Assert.Equal(McpEntryState.Present, McpStatus.Of(spec, disk, Specs.IsRtokBin).Entry);
        Assert.Equal(Apply.NoChanges, Specs.Run(spec, McpMode.Install, disk));
        Assert.Equal(Apply.NoChanges, Specs.Run(spec, McpMode.Update, disk));

        Assert.Equal($"- {spec.At}", Specs.Run(spec, McpMode.Remove, disk));
        Assert.Null(McpConfig.Entry(spec, disk.Read(Specs.File)));
        Assert.Equal(Apply.NoChanges, Specs.Run(spec, McpMode.Remove, disk));
        var expected = existing.Length > 0 || format == McpFormat.Toml ? existing : "{}\n";
        Assert.Equal(expected, fs.File.ReadAllText(Specs.File));
    }

    [Theory]
    [InlineData("mcp_servers", "[mcp_servers.rtok]\n")]
    [InlineData("mcp.servers", "[mcp.servers.rtok]\n")]
    public void Toml_entry_is_a_standard_table_under_implicit_parents(string keyPath, string header)
    {
        var spec = Specs.Of(McpFormat.Toml, keyPath);
        var disk = new McpDisk(MemFs.Linux());

        Specs.Run(spec, McpMode.Install, disk);

        Assert.Equal(
            header + "type = \"stdio\"\ncommand = \"rtok\"\nargs = [\"mcp\"]\n",
            disk.Read(Specs.File)
        );
    }

    [Fact]
    public void Toml_update_rewrites_an_inline_entry_as_a_table_and_keeps_the_rest()
    {
        var spec = Specs.Of(McpFormat.Toml, "mcp_servers");
        var fs = MemFs.Linux();
        fs.Directory.CreateDirectory("/home/u/.config/host");
        fs.File.WriteAllText(
            Specs.File,
            "[mcp_servers]\nrtok = { command = \"/old/rtok\", args = [\"serve\"] } # old\nother = { command = \"x\" }\n\n[ui]\ntheme = \"dark\"\n"
        );
        var disk = new McpDisk(fs);

        Assert.Equal("mcp_servers.rtok: rtok mcp", Specs.Run(spec, McpMode.Update, disk));

        Assert.Equal(
            "[mcp_servers]\nother = { command = \"x\" }\n\n[ui]\ntheme = \"dark\"\n\n"
                + "[mcp_servers.rtok]\ntype = \"stdio\"\ncommand = \"rtok\"\nargs = [\"mcp\"]\n",
            disk.Read(Specs.File)
        );
    }

    [Fact]
    public void Toml_update_replaces_the_table_in_place()
    {
        var spec = Specs.Of(McpFormat.Toml, "mcp_servers");
        var fs = MemFs.Linux();
        fs.Directory.CreateDirectory("/home/u/.config/host");
        fs.File.WriteAllText(
            Specs.File,
            "model = \"o3\"\n\n[mcp_servers.rtok]\ncommand = \"rtok\"\nargs = [\"serve\"]\n\n[mcp_servers.rtok.env]\nA = \"1\"\n\n# tail\n[ui]\ntheme = \"dark\"\n"
        );
        var disk = new McpDisk(fs);

        Specs.Run(spec, McpMode.Install, disk);

        Assert.Equal(
            "model = \"o3\"\n\n[mcp_servers.rtok]\ntype = \"stdio\"\ncommand = \"rtok\"\nargs = [\"mcp\"]\n\n\n# tail\n[ui]\ntheme = \"dark\"\n",
            disk.Read(Specs.File)
        );
    }

    [Fact]
    public void Jsonc_keeps_a_table_that_holds_a_comment()
    {
        var spec = Specs.Of(McpFormat.Jsonc);
        const string Text = "{\n  \"mcpServers\": {\n    // mine\n  },\n}\n";
        var disk = Seeded(Text);

        Specs.Run(spec, McpMode.Install, disk);
        Assert.Contains("// mine", disk.Read(Specs.File), StringComparison.Ordinal);
        Specs.Run(spec, McpMode.Remove, disk);

        Assert.Equal(Text, disk.Read(Specs.File));
    }

    [Fact]
    public void Remove_leaves_an_entry_the_user_edited()
    {
        var spec = Specs.Of(McpFormat.Json);
        var disk = Seeded(
            """{"mcpServers":{"rtok":{"type":"stdio","command":"rtok","args":["mcp","--extra"]}}}"""
        );

        Assert.StartsWith("leave mcpServers.rtok in ", Specs.Run(spec, McpMode.Remove, disk));
        Assert.NotNull(McpConfig.Entry(spec, disk.Read(Specs.File)));
    }

    [Fact]
    public void Remove_leaves_an_entry_that_is_not_rtoks()
    {
        var spec = Specs.Of(McpFormat.Json);
        var disk = Seeded("""{"mcpServers":{"rtok":{"command":"other-tool"}}}""");

        Assert.StartsWith("leave mcpServers.rtok in ", Specs.Run(spec, McpMode.Remove, disk));
        Assert.NotNull(McpConfig.Entry(spec, disk.Read(Specs.File)));
    }

    [Fact]
    public void Launch_sets_the_entry_and_a_type_only_difference_is_no_change()
    {
        var spec = Specs.Of(McpFormat.Json) with
        {
            Host = "claude",
            Launch = new McpLaunch("/opt/bin/rtok"),
        };
        var disk = new McpDisk(MemFs.Linux());

        Assert.Equal(
            "mcpServers.rtok: /opt/bin/rtok mcp --host claude",
            Specs.Run(spec, McpMode.Install, disk)
        );
        Assert.Equal(
            """{"type":"stdio","command":"/opt/bin/rtok","args":["mcp","--host","claude"]}""",
            McpConfig.Entry(spec, disk.Read(Specs.File))!.ToJsonString()
        );

        const string NoType =
            """{"mcpServers":{"rtok":{"command":"/opt/bin/rtok","args":["mcp","--host","claude"]}}}""";
        disk.Fs.File.WriteAllText(Specs.File, NoType);
        Assert.Equal(Apply.NoChanges, Specs.Run(spec, McpMode.Install, disk));
        Assert.Equal(Apply.NoChanges, Specs.Run(spec, McpMode.Update, disk));
        Assert.Equal(NoType, disk.Read(Specs.File));
    }

    private const string Ours =
        """{"type":"stdio","command":"/old/bin/rtok","args":["mcp","--host","claude"]}""";
    private const string Edited = """{"command":"rtok","args":["mcp","--extra"]}""";
    private const string Foreign = """{"command":"other-tool"}""";

    [Theory]
    [InlineData(Ours, false, false, "- mcpServers.rtok", false)]
    [InlineData("""{"command":"rtok","args":["mcp"]}""", false, false, "- mcpServers.rtok", false)]
    [InlineData(Edited, false, false, "leave mcpServers.rtok in ", true)]
    [InlineData(Edited, false, true, "? mcpServers.rtok in ", true)]
    [InlineData(Edited, true, false, "- mcpServers.rtok", false)]
    [InlineData(Foreign, true, false, "leave mcpServers.rtok in ", true)]
    public void Remove_follows_ownership_and_the_run_flags(
        string have,
        bool yes,
        bool dryRun,
        string want,
        bool kept
    )
    {
        var spec = Specs.Of(McpFormat.Json);
        var disk = Seeded($$$"""{"keep":1,"mcpServers":{"rtok":{{{have}}}}}""");

        var got = Specs.Run(spec, McpMode.Remove, disk, new Apply { Yes = yes, DryRun = dryRun });

        Assert.StartsWith(want, got, StringComparison.Ordinal);
        Assert.Equal(kept, McpConfig.Entry(spec, disk.Read(Specs.File)) is not null);
    }

    [Theory]
    [InlineData("[1, 2]")]
    [InlineData("null")]
    [InlineData("\"text\"")]
    [InlineData("""{"mcpServers": []}""")]
    [InlineData("""{"mcpServers": null}""")]
    [InlineData("""{"mcpServers": "x"}""")]
    public void Strict_json_refuses_a_non_object_root_or_table_and_keeps_the_file(string raw)
    {
        var spec = Specs.Of(McpFormat.Json);
        var disk = Seeded(raw);

        Assert.Throws<InvalidDataException>(() => Specs.Run(spec, McpMode.Install, disk));
        Assert.Equal(Apply.NoChanges, Specs.Run(spec, McpMode.Remove, disk));
        Assert.Equal(raw, disk.Read(Specs.File));
    }

    [Theory]
    [InlineData(McpFormat.Json, "{\n  // a comment\n}\n")]
    [InlineData(McpFormat.Jsonc, "{ \"mcpServers\": [] }")]
    [InlineData(McpFormat.Toml, "mcp_servers = 3\n")]
    [InlineData(McpFormat.Toml, "not toml [\n")]
    public void A_file_rtok_cannot_edit_is_refused_and_kept(McpFormat format, string raw)
    {
        var spec = Specs.Of(format, format == McpFormat.Toml ? "mcp_servers" : "mcpServers");
        var disk = Seeded(raw);

        Assert.Throws<InvalidDataException>(() => Specs.Run(spec, McpMode.Install, disk));
        Assert.Equal(raw, disk.Read(Specs.File));
    }

    [Fact]
    public void An_unreadable_file_is_refused_not_replaced()
    {
        var spec = Specs.Of(McpFormat.Json);
        var disk = Seeded("""{"keep":1}""");
        using (disk.Fs.File.Open(Specs.File, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Throws<IOException>(() => Specs.Run(spec, McpMode.Install, disk));
            var row = McpStatus.Of(spec, disk, Specs.IsRtokBin);
            Assert.Equal(McpEntryState.Missing, row.Entry);
            Assert.Contains("unreadable", row.Diff, StringComparison.Ordinal);
        }
        Assert.Equal("""{"keep":1}""", disk.Read(Specs.File));
    }

    [Fact]
    public void A_dry_run_reports_and_writes_nothing()
    {
        var spec = Specs.Of(McpFormat.Jsonc);
        var disk = new McpDisk(MemFs.Linux());

        Assert.Equal(
            "mcpServers.rtok: rtok mcp",
            Specs.Run(spec, McpMode.Install, disk, new Apply { DryRun = true })
        );
        Assert.False(disk.Fs.File.Exists(Specs.File));
    }

    private static McpDisk Seeded(string text)
    {
        var fs = MemFs.Linux();
        fs.Directory.CreateDirectory("/home/u/.config/host");
        fs.File.WriteAllText(Specs.File, text);
        return new McpDisk(fs);
    }
}
