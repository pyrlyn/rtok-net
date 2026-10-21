using Rtok.Plugin.Sdk.Testing;

namespace Rtok.Plugins.Cmd.Tests;

/// <summary><c>rtok filter --stdin --cmd</c>: text compressed as if a command printed it.</summary>
public sealed class StdinFilterTests
{
    private static readonly StdinFilter Filter = new(
        CmdSettings.Builtin(),
        trailer: true,
        trailerMinLines: 40
    );

    private static readonly string Eighty = string.Join(
        '\n',
        Enumerable.Range(0, 80).Select(i => $"line {i}")
    );

    [Fact]
    public void Git_status_from_stdin_drops_boilerplate()
    {
        Assert.Skip(DispatchTests.PendingT18);
        const string raw = """
            On branch main
            Changes not staged for commit:
              (use "git add <file>..." to update what will be committed)
            	modified:   src/lib.rs

            """;
        var output = Filter.Run("git status", raw, null);
        Assert.Contains("On branch main", output, StringComparison.Ordinal);
        Assert.Contains(" M src/lib.rs", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Changes not staged", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_family_records_a_rule_row_and_expand_trailer()
    {
        var host = new MemoryHost();
        var output = Filter.Run("read src/lib.rs", Eighty, host);
        Assert.Contains("[rtok ", output, StringComparison.Ordinal);
        Assert.Contains(" lines]", output, StringComparison.Ordinal);
        Assert.True(output.Length < Eighty.Length, output);
        var row = Assert.Single(host.Recorded());
        Assert.Equal("rule", row.Kind);
        Assert.StartsWith("read:", row.RefId, StringComparison.Ordinal);
        Assert.Equal(
            Eighty,
            System.Text.Encoding.UTF8.GetString(host.GetArchive(row.RefId!["read:".Length..])!)
        );
    }

    [Fact]
    public void Grep_find_ls_each_record_a_family_row()
    {
        var host = new MemoryHost();
        foreach (var cmd in (string[])["grep foo", "find *.rs", "ls src"])
        {
            Assert.Contains("expand", Filter.Run(cmd, Eighty, host), StringComparison.Ordinal);
        }

        var rows = host.Recorded();
        Assert.Equal(
            ["grep", "find", "ls"],
            rows.Select(r => r.RefId!.Split(':')[0]),
            StringComparer.Ordinal
        );
        Assert.All(rows, r => Assert.Equal("rule", r.Kind));
    }

    // Without a store the text is still compressed, with nothing to name for `expand`.
    [Fact]
    public void No_host_only_compresses()
    {
        var output = Filter.Run("read src/lib.rs", Eighty, null);
        Assert.True(output.Length < Eighty.Length, output);
        Assert.DoesNotContain("[rtok ", output, StringComparison.Ordinal);
    }

    // Text the filter cannot shorten comes back as it was, with no row.
    [Fact]
    public void Short_text_passes_through()
    {
        var host = new MemoryHost();
        Assert.Equal("a\nb\n", Filter.Run("cat f", "a\nb\n", host));
        Assert.Empty(host.Recorded());
    }

    // The same without a store: the cut must not even eat the final newline.
    [Fact]
    public void Short_text_passes_through_without_a_host() =>
        Assert.Equal("a\nb\n", Filter.Run("cat f", "a\nb\n", null));
}
