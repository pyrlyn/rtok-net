using System.Text.Json;
using System.Text.Json.Nodes;
using Rtok.Agent.Sdk;
using Rtok.Testing;
using Testably.Abstractions.Testing;

namespace Rtok.Mcp.Install.Tests;

/// <summary>The T34 check: install, update, status and remove for every host's spec, on a fake home.</summary>
public sealed class McpHostsTests
{
    public static TheoryData<string, string, McpOs> Surfaces()
    {
        var data = new TheoryData<string, string, McpOs>();
        foreach (var os in Enum.GetValues<McpOs>())
        {
            var (fs, env) = Home(os);
            foreach (var spec in McpHosts.All(env, fs))
            {
                data.Add(spec.Host, spec.Surface, os);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void Install_update_status_and_remove_work_for_every_host(
        string host,
        string surface,
        McpOs os
    )
    {
        var (fs, env) = Home(os);
        var spec = McpHosts
            .For(host, env, fs)
            .Single(s => string.Equals(s.Surface, surface, StringComparison.Ordinal));
        var disk = new McpDisk(fs);
        Assert.StartsWith(Root(os), spec.Path, StringComparison.Ordinal);

        Assert.Equal(McpEntryState.Missing, Status(spec, disk).Entry);
        Assert.Equal($"{spec.At}: {spec.Summary()}", Specs.Run(spec, McpMode.Install, disk));
        Assert.Equal(McpEntryState.Present, Status(spec, disk).Entry);
        Assert.Equal(Apply.NoChanges, Specs.Run(spec, McpMode.Install, disk));
        Assert.Equal(Apply.NoChanges, Specs.Run(spec, McpMode.Update, disk));
        Assert.True(
            JsonNode.DeepEquals(spec.Entry(), McpConfig.Entry(spec, disk.Read(spec.Path))),
            disk.Read(spec.Path)
        );

        Assert.Equal($"- {spec.At}", Specs.Run(spec, McpMode.Remove, disk));
        Assert.Equal(McpEntryState.Missing, Status(spec, disk).Entry);
        Assert.Equal(Apply.NoChanges, Specs.Run(spec, McpMode.Remove, disk));
    }

    [Fact]
    public void Every_entry_names_its_host()
    {
        var (fs, env) = Home(McpOs.Linux);
        foreach (var spec in McpHosts.All(env, fs))
        {
            Assert.EndsWith($"mcp --host {spec.Host}", spec.Summary(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Desktop_apps_without_a_shell_path_get_the_absolute_binary()
    {
        var (fs, env) = Home(McpOs.MacOS);
        env = env with { Exe = "/opt/rtok/bin/rtok" };

        var absolute = McpHosts
            .All(env, fs)
            .Where(s => s.Launch.Command is not null)
            .Select(s => $"{s.Host}.{s.Surface}")
            .ToList();

        Assert.Equal(["claude.desktop", "zcode.desktop"], absolute);
    }

    [Fact]
    public void A_path_the_user_set_replaces_the_default()
    {
        var (fs, env) = Home(McpOs.Linux);
        env = env with
        {
            Paths = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["codex.cli"] = "/work/codex.toml",
            },
        };

        var codex = McpHosts.For("codex", env, fs).Single();

        Assert.Equal("/work/codex.toml", codex.Path);
        Assert.Equal(McpFormat.Toml, codex.Format);
    }

    [Fact]
    public void Amp_and_mimo_edit_the_jsonc_file_when_only_it_exists()
    {
        var (fs, env) = Home(McpOs.Linux);
        fs.Directory.CreateDirectory("/home/u/.config/amp");
        fs.File.WriteAllText("/home/u/.config/amp/settings.jsonc", "{}");

        var amp = McpHosts.For("amp", env, fs).Single();

        Assert.Equal("/home/u/.config/amp/settings.jsonc", amp.Path);
        Assert.Equal(["amp.mcpServers"], amp.KeyPath);
        Assert.Equal(McpFormat.Jsonc, amp.Format);
        Assert.Equal(McpFormat.Json, McpHosts.For("mimo", env, fs)[0].Format);
    }

    [Theory]
    [InlineData(McpOs.Linux, "/home/u/.config/Claude/claude_desktop_config.json")]
    [InlineData(
        McpOs.MacOS,
        "/Users/u/Library/Application Support/Claude/claude_desktop_config.json"
    )]
    [InlineData(McpOs.Windows, @"C:\Users\u\AppData\Roaming\Claude\claude_desktop_config.json")]
    public void Claude_desktop_follows_the_os_layout(McpOs os, string path)
    {
        var (fs, env) = Home(os);

        Assert.Equal(
            path,
            McpHosts
                .For("claude", env, fs)
                .Single(s => string.Equals(s.Surface, "desktop", StringComparison.Ordinal))
                .Path
        );
    }

    [Fact]
    public void The_status_row_serializes_for_json_and_the_web_model()
    {
        var row = new McpRow("cli", "~/.codex/config.toml", McpEntryState.Present, null, null);

        Assert.Equal(
            """{"surface":"cli","file":"~/.codex/config.toml","entry":"present","plugin":null}""",
            JsonSerializer.Serialize(row, McpJson.Default.McpRow)
        );
    }

    private static McpRow Status(McpSpec spec, McpDisk disk) =>
        McpStatus.Of(spec, disk, Specs.IsRtokBin);

    private static string Root(McpOs os) =>
        os switch
        {
            McpOs.Windows => @"C:\Users\u",
            McpOs.MacOS => "/Users/u",
            _ => "/home/u",
        };

    private static (MockFileSystem Fs, McpHostEnv Env) Home(McpOs os)
    {
        var fs = os switch
        {
            McpOs.Windows => MemFs.Windows(),
            McpOs.MacOS => MemFs.MacOS(),
            _ => MemFs.Linux(),
        };
        var root = Root(os);
        fs.Directory.CreateDirectory(root);
        var appData = os == McpOs.Windows ? @"C:\Users\u\AppData\Roaming" : null;
        return (fs, new McpHostEnv(root, os, appData));
    }
}
