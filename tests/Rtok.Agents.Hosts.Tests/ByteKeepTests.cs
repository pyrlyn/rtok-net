using CsCheck;
using Rtok.Agents.Hosts.Mcp;
using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts.Tests;

/// <summary>Install then uninstall gives back every byte of a host's own file, whatever else is in it.</summary>
public sealed class ByteKeepTests
{
    private static readonly Gen<string> Word = Gen.Char["abcxyz019 _-"]
        .Array[1, 10]
        .Select(c => new string(c));

    /// <summary>Top-level lines <c>k&lt;i&gt;: 'v'</c>, comments and blank lines, newline-terminated.</summary>
    private static readonly Gen<List<string>> YamlLines = Gen.Select(Word, Gen.Int[0, 2])
        .Array[0, 6]
        .Select(items =>
            items
                .Select(
                    (x, i) =>
                        x.Item2 switch
                        {
                            0 => $"k{i}: '{x.Item1}'",
                            1 => $"# {x.Item1}",
                            _ => "",
                        }
                )
                .ToList()
        );

    private static readonly Gen<bool> Crlf = Gen.Bool;

    [Fact]
    public void Goose_keeps_every_byte() =>
        Gen.Select(YamlLines, Gen.Int[0, 6], Gen.Bool, Crlf)
            .Sample(t =>
            {
                var (lines, at, block, crlf) = t;
                if (block)
                {
                    lines.Insert(
                        Math.Min(at, lines.Count),
                        "extensions:\n  dev:\n    type: builtin"
                    );
                }
                RoundTrips(new GooseMcp(OwnFiles.Dir + "/config.yaml"), Join(lines, crlf));
            });

    [Fact]
    public void Continue_keeps_every_byte() =>
        Gen.Select(YamlLines, Gen.Int[0, 6], Gen.Bool, Crlf)
            .Sample(t =>
            {
                var (lines, at, block, crlf) = t;
                if (block)
                {
                    lines.Insert(
                        Math.Min(at, lines.Count),
                        "mcpServers:\n  - name: other\n    command: x"
                    );
                }
                // A blank file is a fresh one: it gets Continue's "Local Config" header, which stays.
                if (lines.TrueForAll(string.IsNullOrWhiteSpace))
                {
                    return;
                }
                RoundTrips(new ContinueMcp(OwnFiles.Dir + "/config.yaml"), Join(lines, crlf));
            });

    [Fact]
    public void Reasonix_keeps_every_byte() =>
        Gen.Select(Word.Array[0, 5], Gen.Bool, Crlf)
            .Sample(t =>
            {
                var (words, table, crlf) = t;
                var lines = words.Select((w, i) => $"k{i} = '{w}'").ToList();
                if (table)
                {
                    lines.Add("[[plugins]]\nname = \"fs\" # mine");
                }
                RoundTrips(new ReasonixMcp(OwnFiles.Dir + "/config.toml"), Join(lines, crlf));
            });

    /// <summary>A JSONC file of the MCP core (Cursor's <c>mcp.json</c>) with comments and foreign servers around rtok's.</summary>
    [Fact]
    public void Jsonc_keeps_every_byte() =>
        Gen.Select(Word.Array[0, 4], Gen.Bool, Gen.Bool)
            .Sample(t =>
            {
                var (words, servers, crlf) = t;
                var keys = words.Select((w, i) => $"  // {w}\n  \"k{i}\": \"{w}\",\n");
                var tail = servers
                    ? "  \"context_servers\": {\n    \"other\": { \"command\": \"x\" } // theirs\n  }\n"
                    : "  \"z\": 1\n";
                var text = "{\n" + string.Concat(keys) + tail + "}\n";
                if (crlf)
                {
                    text = text.Replace("\n", "\r\n", StringComparison.Ordinal);
                }
                var home = new FakeHome();
                home.Install(AgentHosts.Find("zed")!.Variants[0]);
                var path = home.At(".config/zed/settings.json");
                home.Write(path, text);
                var run = home.Run();
                run.Run(new HostRequest(["zed"], McpMode.Install, false, false, false));
                Assert.Contains("rtok", home.Read(path), StringComparison.Ordinal);
                run.Run(new HostRequest(["zed"], McpMode.Remove, false, false, false));
                Assert.Equal(text, home.Read(path));
            });

    private static string Join(List<string> lines, bool crlf)
    {
        var text = string.Concat(lines.Select(l => l + "\n"));
        return crlf ? text.Replace("\n", "\r\n", StringComparison.Ordinal) : text;
    }

    private static void RoundTrips(McpFile file, string text)
    {
        var disk = new OwnFiles();
        disk.Put(file, text);
        disk.Apply(file, McpMode.Install);
        Assert.True(disk.Installed(file));
        disk.Apply(file, McpMode.Remove);
        Assert.Equal(text, disk.Text(file));
    }
}
