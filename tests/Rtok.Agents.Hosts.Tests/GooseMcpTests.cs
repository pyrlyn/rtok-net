using Rtok.Agent.Sdk;
using Rtok.Agents.Hosts.Mcp;
using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts.Tests;

/// <summary>Goose's <c>config.yaml</c>: only the lines of <c>extensions.rtok</c> change (rtok's goose tests).</summary>
public sealed class GooseMcpTests
{
    private const string Foreign =
        "GOOSE_PROVIDER: openai\n"
        + "extensions:\n"
        + "  developer:\n"
        + "    enabled: true\n"
        + "    type: builtin\n"
        + "\n"
        + "GOOSE_MODE: auto\n";

    private readonly GooseMcp _file = new(OwnFiles.Dir + "/config.yaml");
    private readonly OwnFiles _disk = new();

    [Fact]
    public void A_fresh_file_gets_the_block_and_remove_empties_it()
    {
        Assert.Equal("extensions.rtok: rtok mcp --host goose", _disk.Apply(_file, McpMode.Install));
        var text = _disk.Text(_file)!;
        Assert.StartsWith("extensions:\n  rtok:\n    name: rtok\n", text, StringComparison.Ordinal);
        Assert.Contains("args: [mcp, --host, goose]", text, StringComparison.Ordinal);
        Assert.True(_disk.Installed(_file));

        Assert.Equal(Apply.NoChanges, _disk.Apply(_file, McpMode.Install));
        Assert.Equal("- extensions.rtok", _disk.Apply(_file, McpMode.Remove));
        Assert.Equal("", _disk.Text(_file));
    }

    [Fact]
    public void Install_goes_after_the_last_extension_and_remove_restores_every_byte()
    {
        _disk.Put(_file, Foreign);
        _disk.Apply(_file, McpMode.Install);
        Assert.Equal(
            Foreign.Replace(
                "    type: builtin\n",
                "    type: builtin\n  rtok:\n    name: rtok\n    type: stdio\n    cmd: rtok\n"
                    + "    args: [mcp, --host, goose]\n    enabled: true\n",
                StringComparison.Ordinal
            ),
            _disk.Text(_file)
        );
        _disk.Apply(_file, McpMode.Remove);
        Assert.Equal(Foreign, _disk.Text(_file));
    }

    [Fact]
    public void An_empty_extensions_key_goes_with_rtok()
    {
        _disk.Put(_file, "extensions:\nGOOSE_MODE: auto\n");
        _disk.Apply(_file, McpMode.Install);
        _disk.Apply(_file, McpMode.Remove);
        Assert.Equal("GOOSE_MODE: auto\n", _disk.Text(_file));
    }

    [Fact]
    public void A_file_without_a_final_newline_gets_one_before_the_block()
    {
        _disk.Put(_file, "GOOSE_MODE: auto");
        _disk.Apply(_file, McpMode.Install);
        Assert.StartsWith(
            "GOOSE_MODE: auto\nextensions:\n  rtok:\n",
            _disk.Text(_file),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Crlf_stays_crlf()
    {
        var crlf = Foreign.Replace("\n", "\r\n", StringComparison.Ordinal);
        _disk.Put(_file, crlf);
        _disk.Apply(_file, McpMode.Install);
        Assert.DoesNotContain(
            "\n",
            _disk.Text(_file)!.Replace("\r\n", "", StringComparison.Ordinal),
            StringComparison.Ordinal
        );
        _disk.Apply(_file, McpMode.Remove);
        Assert.Equal(crlf, _disk.Text(_file));
    }

    [Fact]
    public void An_entry_the_user_changed_is_left_and_one_of_someone_else_is_not_rtoks()
    {
        _disk.Apply(_file, McpMode.Install);
        var installed = _disk.Text(_file)!;
        _disk.Put(
            _file,
            installed.Replace("enabled: true", "enabled: false", StringComparison.Ordinal)
        );
        Assert.StartsWith(
            "leave extensions.rtok",
            _disk.Apply(_file, McpMode.Remove),
            StringComparison.Ordinal
        );

        _disk.Put(_file, installed.Replace("cmd: rtok", "cmd: other", StringComparison.Ordinal));
        Assert.Contains("not rtok's", _disk.Apply(_file, McpMode.Remove), StringComparison.Ordinal);
        Assert.False(_disk.Installed(_file));
    }

    [Theory]
    [InlineData("extensions: {}\n")]
    [InlineData("extensions: 3\n")]
    [InlineData("extensions: [\n")]
    [InlineData("extensions:\n*OOSE_MODE: auto\n")]
    [InlineData("? [a, b]\n: c\n")]
    public void A_file_it_cannot_edit_is_refused_and_untouched(string text)
    {
        _disk.Put(_file, text);
        Assert.Throws<InvalidDataException>(() => _disk.Apply(_file, McpMode.Install));
        Assert.Equal(text, _disk.Text(_file));
    }

    [Fact]
    public void A_command_with_a_space_is_quoted()
    {
        Assert.Equal("\"/My Apps/rtok\"", YamlText.Scalar("/My Apps/rtok"));
        _disk.Apply(_file, McpMode.Install, "/My Apps/rtok");
        Assert.Contains(
            "    cmd: \"/My Apps/rtok\"\n",
            _disk.Text(_file),
            StringComparison.Ordinal
        );
        Assert.Equal(Apply.NoChanges, _disk.Apply(_file, McpMode.Install, "/My Apps/rtok"));
    }
}
