using Rtok.Agent.Sdk;
using Rtok.Agents.Hosts.Mcp;
using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts.Tests;

/// <summary>Reasonix's <c>config.toml</c>: rtok is the <c>[[plugins]]</c> table named <c>rtok</c> (rtok's reasonix tests).</summary>
public sealed class ReasonixMcpTests
{
    private const string Foreign =
        "# my reasonix\nmodel = \"deepseek\"\n\n[[plugins]]\nname = \"fs\"\ncommand = \"fs-mcp\"  # keep\n";

    private readonly ReasonixMcp _file = new(OwnFiles.Dir + "/config.toml");
    private readonly OwnFiles _disk = new();

    [Fact]
    public void Install_appends_a_table_once_and_remove_restores_every_byte()
    {
        _disk.Put(_file, Foreign);
        Assert.Equal("plugins.rtok: rtok mcp --host reasonix", _disk.Apply(_file, McpMode.Install));
        var installed = _disk.Text(_file)!;
        Assert.Equal(
            Foreign
                + "\n[[plugins]]\nname = \"rtok\"\ncommand = \"rtok\"\nargs = [\"mcp\", \"--host\", \"reasonix\"]\n",
            installed
        );
        Assert.Equal(Apply.NoChanges, _disk.Apply(_file, McpMode.Install));
        Assert.Equal(installed, _disk.Text(_file));
        Assert.Equal("- plugins.rtok", _disk.Apply(_file, McpMode.Remove));
        Assert.Equal(Foreign, _disk.Text(_file));
    }

    [Fact]
    public void A_table_named_rtok_is_kept_whoever_wrote_it()
    {
        const string theirs = "[[plugins]]\nname = \"rtok\"\ncommand = \"/opt/my/rtok\"\n";
        _disk.Put(_file, theirs);
        Assert.True(_disk.Installed(_file));
        Assert.Equal(Apply.NoChanges, _disk.Apply(_file, McpMode.Install));
        Assert.Equal(theirs, _disk.Text(_file));
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

    [Theory]
    [InlineData("plugins = \"mine\"\n")]
    [InlineData("[[plugins]\n")]
    public void A_file_it_cannot_edit_is_refused_and_untouched(string text)
    {
        _disk.Put(_file, text);
        Assert.Throws<InvalidDataException>(() => _disk.Apply(_file, McpMode.Install));
        Assert.Equal(text, _disk.Text(_file));
    }
}
