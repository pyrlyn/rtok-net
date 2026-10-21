using Rtok.Agent.Sdk;
using Rtok.Agents.Hosts.Mcp;
using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts.Tests;

/// <summary>Continue's <c>config.yaml</c>: only rtok's <c>mcpServers</c> item changes (rtok's continue tests).</summary>
public sealed class ContinueMcpTests
{
    private const string Item =
        "- name: rtok\n  type: stdio\n  command: rtok\n  args: [mcp, --host, continue]\n";

    private readonly ContinueMcp _file = new(OwnFiles.Dir + "/config.yaml");
    private readonly OwnFiles _disk = new();

    [Fact]
    public void A_fresh_file_gets_the_local_config_and_remove_keeps_it()
    {
        Assert.Equal(
            "mcpServers.rtok: rtok mcp --host continue",
            _disk.Apply(_file, McpMode.Install)
        );
        var text = _disk.Text(_file)!;
        Assert.StartsWith(
            "name: Local Config\nversion: 1.0.0\nschema: v1\nmcpServers:\n  - name: rtok\n",
            text,
            StringComparison.Ordinal
        );
        Assert.Contains("args: [mcp, --host, continue]", text, StringComparison.Ordinal);
        Assert.Equal(Apply.NoChanges, _disk.Apply(_file, McpMode.Install));
        Assert.Equal("- mcpServers.rtok", _disk.Apply(_file, McpMode.Remove));
        Assert.Equal("name: Local Config\nversion: 1.0.0\nschema: v1\n", _disk.Text(_file));
    }

    [Fact]
    public void A_list_at_its_keys_indent_gets_the_item_at_that_indent()
    {
        const string foreign = "mcpServers:\n- name: other\n  command: x\nrules: []\n";
        _disk.Put(_file, foreign);
        _disk.Apply(_file, McpMode.Install);
        Assert.Equal(
            "mcpServers:\n- name: other\n  command: x\n" + Item + "rules: []\n",
            _disk.Text(_file)
        );
        _disk.Apply(_file, McpMode.Remove);
        Assert.Equal(foreign, _disk.Text(_file));
    }

    [Theory]
    [InlineData("mcpServers: []\nname: n\n")]
    [InlineData("mcpServers:\nname: n\n")]
    public void An_empty_list_gets_a_block_list_and_loses_it_again(string text)
    {
        _disk.Put(_file, text);
        _disk.Apply(_file, McpMode.Install);
        Assert.StartsWith(
            "mcpServers:\n  - name: rtok\n",
            _disk.Text(_file),
            StringComparison.Ordinal
        );
        _disk.Apply(_file, McpMode.Remove);
        Assert.Equal("name: n\n", _disk.Text(_file));
    }

    [Fact]
    public void Crlf_stays_crlf()
    {
        const string crlf = "name: n\r\nmcpServers:\r\n  - name: other\r\n    command: x\r\n";
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

    [Theory]
    [InlineData("mcpServers: [\n")]
    [InlineData("mcpServers:\n  a: b\n")]
    [InlineData("mcpServers: [{name: o}]\n")]
    public void A_file_it_cannot_edit_is_refused_and_untouched(string text)
    {
        _disk.Put(_file, text);
        Assert.Throws<InvalidDataException>(() => _disk.Apply(_file, McpMode.Install));
        Assert.Equal(text, _disk.Text(_file));
    }
}
