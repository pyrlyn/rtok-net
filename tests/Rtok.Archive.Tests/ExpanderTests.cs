using System.Text;
using CsCheck;
using Rtok.Store.Model;

namespace Rtok.Archive.Tests;

/// <summary>The one expand handler <c>rtok expand</c> and MCP <c>expand</c> share.</summary>
public sealed class ExpanderTests : IDisposable
{
    private static readonly byte[] TenLines = Encoding.UTF8.GetBytes(
        string.Concat(Enumerable.Range(1, 10).Select(i => $"line {i}\n"))
    );

    private readonly ArchiveFixture _f = new();

    [Fact]
    public void Expand_without_a_view_returns_the_bytes_as_archived() =>
        Gen.OneOf(
                Gen.Byte.Array[0, 300],
                Gen.OneOfConst("a\r\nb\r\n", "\r\n", "\n\n", "x\ry", "")
                    .Select(Encoding.UTF8.GetBytes),
                Gen.OneOfConst<byte[]>([0xff, 0xfe, 0x0a], [0xc3, 0x28, 0x0d, 0x0a], [0xe2, 0x82])
            )
            .Sample(
                body =>
                {
                    using var f = new ArchiveFixture();
                    var id = f.Store.PutArchive("s", body);
                    var result = f.Expand(id);
                    Assert.True(result.Ok, result.Error);
                    Assert.Equal(body, result.Body);
                },
                iter: 60
            );

    [Fact]
    public void An_eight_char_prefix_resolves_a_shorter_one_is_refused()
    {
        var id = _f.Store.PutArchive("s", TenLines);

        Assert.Equal(TenLines, _f.Expand(id[..8]).Body);
        Assert.Equal(TenLines, _f.Expand(id[..12].ToUpperInvariant()).Body);
        Assert.Equal(
            $"unknown archive id: {id[..7]} (expand takes the hex id from an \"expand <id>\" trailer; it does not read stdin)",
            _f.Expand(id[..7]).Error
        );
    }

    [Fact]
    public void An_ambiguous_prefix_lists_the_candidates_by_twelve_chars()
    {
        using (var db = _f.Read())
        {
            foreach (
                var id in (string[])
                    ["abcdef01aaaa" + new string('0', 52), "abcdef01bbbb" + new string('0', 52)]
            )
            {
                db.Archive.Add(
                    new ArchiveEntry
                    {
                        Id = id,
                        Session = "s",
                        Path = "/nowhere",
                        Sha256 = id,
                    }
                );
            }
            db.SaveChanges();
        }

        Assert.Equal(
            "archive id prefix abcdef01 is ambiguous; use one of: abcdef01aaaa, abcdef01bbbb",
            _f.Expand("abcdef01").Error
        );
    }

    [Theory]
    [InlineData("-")]
    [InlineData("/dev/stdin")]
    [InlineData("expand")]
    [InlineData("abc")]
    public void A_non_hex_id_gets_the_stdin_hint(string id) =>
        Assert.Equal(
            $"unknown archive id: {id} (expand takes the hex id from an \"expand <id>\" trailer; it does not read stdin)",
            _f.Expand(id).Error
        );

    [Fact]
    public void A_hex_id_with_no_archive_is_just_unknown() =>
        Assert.Equal(
            $"unknown archive id: {new string('e', 64)}",
            _f.Expand(new string('e', 64)).Error
        );

    [Fact]
    public void Views_render_numbered_lines_with_a_trailing_newline()
    {
        var id = _f.Store.PutArchive("s", TenLines);

        Assert.Equal("line 3\nline 4\nline 5\n", Text(_f.Expand(id, lines: "3-5")));
        Assert.Equal("2:line 2\n", Text(_f.Expand(id, grep: "line 2$")));
        Assert.Equal(
            "1:line 1\n2:line 2\n3:line 3\n",
            Text(_f.Expand(id, grep: "line 2$", context: 1))
        );
        Assert.Equal("", Text(_f.Expand(id, grep: "nothing")));
    }

    [Fact]
    public void Max_lines_caps_with_the_short_id_and_the_measurement_keeps_the_full_one()
    {
        var id = _f.Store.PutArchive("s", TenLines);
        _f.Store.PutArchiveDecision("proxy", "tu1", id, "[archived 10 lines]");
        _f.Config.Expand.MaxLines = 3;

        Assert.Equal(
            $"line 1\nline 2\nline 3\n… 7 lines omitted (expand {id[..8]})\n",
            Text(_f.Expand(id))
        );
        using var db = _f.Read();
        Assert.Equal(id, Assert.Single(db.Measurements).RefId);
    }

    [Fact]
    public void A_range_past_the_end_fails_before_the_freeze()
    {
        var id = _f.Store.PutArchive("s", TenLines);
        _f.Store.PutArchiveDecision("proxy", "tu1", id, "[archived 10 lines]");

        Assert.Equal(
            "invalid line range `11-12`: start exceeds line count 10",
            _f.Expand(id, lines: "11-12").Error
        );
        Assert.Equal(
            "invalid line range `x`: expected a positive line or a-b",
            _f.Expand(id, lines: "x").Error
        );
        using var db = _f.Read();
        Assert.Null(db.ArchiveDecisions.Single().ExpandedTs);
        Assert.Empty(db.Measurements);
    }

    [Fact]
    public void A_malformed_range_is_refused_before_the_id_is_looked_up() =>
        Assert.Equal(
            "invalid line range `0`: lines are 1-based",
            _f.Expand(new string('e', 64), lines: "0").Error
        );

    [Fact]
    public void An_empty_archive_rejects_every_range()
    {
        var id = _f.Store.PutArchive("s", []);

        Assert.Equal(
            "invalid line range `1`: start exceeds line count 0",
            _f.Expand(id, lines: "1").Error
        );
        Assert.Equal("", Text(_f.Expand(id)));
    }

    [Theory]
    [InlineData("[archived 10 lines]", "archive")]
    [InlineData("[toon 10 rows]", "toon")]
    [InlineData("[json-tree 3 keys]", "json_tree")]
    public void Expand_freezes_every_pointer_and_records_one_measurement(
        string pointerText,
        string plugin
    )
    {
        var id = _f.Store.PutArchive("s", TenLines);
        _f.Store.PutArchiveDecision("proxy-a", "tu1", id, pointerText);
        _f.Store.PutArchiveDecision("proxy-b", "tu9", id, pointerText);

        Assert.True(_f.Expand(id[..8]).Ok);
        Assert.True(_f.Expand(id).Ok);

        using var db = _f.Read();
        Assert.All(db.ArchiveDecisions, d => Assert.NotNull(d.ExpandedTs));
        var m = Assert.Single(db.Measurements);
        Assert.Equal(
            (Expander.CliSession, plugin, "expand", 0L, (long)TenLines.Length, 0L, id),
            (m.Session, m.Plugin, m.Kind, m.BeforeBytes, m.AfterBytes, m.EstBefore, m.RefId)
        );
        Assert.True(m.EstAfter > 0);
    }

    [Fact]
    public void Without_a_decision_expand_records_nothing()
    {
        var id = _f.Store.PutArchive("s", TenLines);

        Assert.True(_f.Expand(id).Ok);
        using var db = _f.Read();
        Assert.Empty(db.Measurements);
    }

    [Fact]
    public void A_failed_measurement_rolls_the_freeze_back()
    {
        var id = _f.Store.PutArchive("s", TenLines);
        _f.Store.PutArchiveDecision("proxy", "tu1", id, "[archived]");

        // No call 424242: the measurement's foreign key fails inside the freeze's transaction.
        var expander = new Expander(_f.Store, _f.Config, "mcp-1", callId: 424242);
        Assert.ThrowsAny<Exception>(() => expander.Run(new ExpandRequest(id)));

        using var db = _f.Read();
        Assert.Null(db.ArchiveDecisions.Single().ExpandedTs);
        Assert.Empty(db.Measurements);
    }

    private static string Text(ExpandResult result)
    {
        Assert.True(result.Ok, result.Error);
        return Encoding.UTF8.GetString(result.Body);
    }

    public void Dispose() => _f.Dispose();
}
