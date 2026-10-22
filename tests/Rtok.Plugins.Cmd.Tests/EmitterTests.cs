using System.Text;
using Rtok.Core;
using Rtok.Plugin.Sdk;
using Rtok.Plugin.Sdk.Testing;

namespace Rtok.Plugins.Cmd.Tests;

/// <summary>What <c>rtok run</c> prints for a captured body, and the Measurement it records.</summary>
public sealed class EmitterTests
{
    private static readonly Emitter Emitter = new(
        CmdSettings.Builtin(),
        trailer: true,
        trailerMinLines: 40
    );

    private static string Lines(int n) =>
        string.Join('\n', Enumerable.Range(0, n).Select(i => $"line {i}"));

    private static byte[] Emit(MemoryHost? host, string[] argv, byte[] body, string? agent = null)
    {
        using var output = new MemoryStream();
        Emitter.Emit(host, argv, body, 0, agent, output);
        return output.ToArray();
    }

    [Fact]
    public void Trailer_names_an_eight_char_id()
    {
        var id = string.Concat(Enumerable.Repeat("0123456789abcdef", 4));
        Assert.Equal("[rtok 01234567 · 5 lines]", Emitter.Trailer(id, 5));
        Assert.True(
            Emitter.NamesTheId("… 4 lines omitted (expand 01234567)", ArchiveIds.Abbreviate(id))
        );
    }

    // A formatter that trims a short output still has to name the archive; an in-place cut
    // and destroyed bytes are losses too, however short the output was.
    [Fact]
    public void A_shortened_output_needs_a_pointer_whatever_its_length()
    {
        var log = string.Join('\n', Enumerable.Range(0, 29).Select(i => $"c{i} msg"));
        var cut = string.Join('\n', Enumerable.Range(0, 20).Select(i => $"c{i} msg"));
        var bytes = Encoding.UTF8.GetBytes(log);
        Assert.True(Emitter.NeedsPointer(29, 40, bytes, cut, false), "trimmed under the threshold");
        Assert.False(Emitter.NeedsPointer(29, 40, bytes, log, false), "nothing dropped");
        Assert.True(Emitter.NeedsPointer(400, 40, bytes, log, false), "long enough on its own");
        Assert.True(
            Emitter.NeedsPointer(1, 40, """{"blob":"xxxx"}"""u8, """blob: "xx… " (4)""", false),
            "cut in place"
        );
        Assert.True(
            Emitter.NeedsPointer(1, 40, [0xff, 0xfe, (byte)'o', (byte)'k'], "��ok", false),
            "lost bytes"
        );
        Assert.False(
            Emitter.NeedsPointer(
                10,
                40,
                bytes,
                "c0 msg\n… 27 lines omitted (expand abc)\nc28 msg",
                true
            ),
            "the marker already names the archive"
        );
    }

    // Whitespace and ANSI colour are noise: a reader misses nothing, so no trailer to pay for.
    [Fact]
    public void Padding_and_ansi_only_changes_need_no_pointer()
    {
        var padded = "NAME   STATUS   AGE\nweb-0  Running  3d\napi-1  Pending  1h\n"u8;
        Assert.False(
            Emitter.NeedsPointer(
                3,
                40,
                padded,
                "NAME STATUS AGE\nweb-0 Running 3d\napi-1 Pending 1h",
                false
            )
        );
        Assert.False(Emitter.NeedsPointer(1, 40, "\u001b[1mok\u001b[0m\n"u8, "ok", false));
        Assert.False(Emitter.NeedsPointer(1, 40, "hello-trycmd\n"u8, "hello-trycmd", false));
    }

    // A short `find` the dir grouping would lengthen prints verbatim, with no archive: never worse than raw.
    [Fact]
    public void Short_output_a_filter_lengthens_prints_raw()
    {
        var host = new MemoryHost();
        var body = Encoding.UTF8.GetBytes(
            string.Concat(Enumerable.Range(0, 9).Select(i => $"crates/c{i}/Cargo.toml\n"))
        );
        Assert.Equal(body, Emit(host, ["find", "crates"], body));
        var row = Assert.Single(host.Recorded());
        Assert.Equal("raw", row.Kind);
        Assert.Null(row.RefId);
    }

    [Fact]
    public void Emit_archives_the_body_and_records()
    {
        var host = new MemoryHost();
        var body = Encoding.UTF8.GetBytes(Lines(400));
        var shown = Encoding.UTF8.GetString(Emit(host, ["cat"], body));
        var row = Assert.Single(host.Recorded());
        Assert.Equal("cmd", row.Plugin);
        Assert.StartsWith("cat:", row.RefId, StringComparison.Ordinal);
        var id = row.RefId!["cat:".Length..];
        Assert.Equal(body, host.GetArchive(id));
        // The trailer names the id the host gave, not the sha256 the filter guessed.
        Assert.Contains(
            $"[rtok {ArchiveIds.Abbreviate(id)} · 400 lines]",
            shown,
            StringComparison.Ordinal
        );
        Assert.True(row.AfterBytes < row.BeforeBytes);
    }

    [Fact]
    public void Emit_skill_records_its_kind()
    {
        var host = new MemoryHost();
        Emit(host, ["skill", "demo"], Encoding.UTF8.GetBytes(Lines(80)));
        Assert.Equal("skill", Assert.Single(host.Recorded()).Kind);
    }

    // `expand` must return what the command wrote, invalid UTF-8 included.
    [Fact]
    public void Archive_keeps_bytes_that_are_not_utf8()
    {
        var host = new MemoryHost();
        byte[] body =
        [
            0xff,
            0xfe,
            (byte)'o',
            (byte)'k',
            (byte)'\n',
            .. Enumerable.Repeat((byte)'x', 300),
            (byte)'\n',
        ];
        Emit(host, ["cat"], body);
        var row = Assert.Single(host.Recorded());
        Assert.Equal(body, host.GetArchive(row.RefId!.Split(':')[1]));
    }

    // The text view replaces a broken byte, never a whole character next to it.
    [Fact]
    public void Non_utf8_is_shown_lossily_without_cutting_a_character()
    {
        var host = new MemoryHost();
        var body = new List<byte>();
        for (var i = 0; i < 400; i++)
        {
            body.AddRange(Encoding.UTF8.GetBytes($"line {i} é"));
            body.AddRange([0xff, (byte)'\n']);
        }

        var shown = Encoding.UTF8.GetString(Emit(host, ["cat"], [.. body]));
        Assert.Contains("line 0 é\ufffd\n", shown, StringComparison.Ordinal);
        Assert.Contains("[rtok ", shown, StringComparison.Ordinal);
    }

    // A store that cannot take the archive leaves nothing to expand: the raw bytes go out.
    [Fact]
    public void A_failing_store_prints_the_raw_bytes_and_a_newline()
    {
        var host = new MemoryHost { ArchiveFails = true };
        var body = Encoding.UTF8.GetBytes(Lines(400));
        Assert.Equal([.. body, (byte)'\n'], Emit(host, ["cat"], body));
    }

    // A body no longer than the trailer prints raw with a zero-saving row: nothing was saved,
    // and nothing claims it was.
    [Theory]
    [InlineData("\u001b[1mok\u001b[0m\n")]
    [InlineData("ok\n")]
    public void Tiny_output_prints_raw_with_a_zero_saving_row(string text)
    {
        var host = new MemoryHost();
        var body = Encoding.UTF8.GetBytes(text);
        var shown = Emit(host, ["kubectl", "get", "pods"], body);
        Assert.Equal(body, shown);
        var row = Assert.Single(host.Recorded());
        Assert.Equal("raw", row.Kind);
        Assert.Equal(row.BeforeBytes, row.AfterBytes);
        Assert.Equal(row.EstBefore, row.EstAfter);
        Assert.Null(row.RefId);
    }

    // A whitespace-only cut has nothing to expand: no trailer, so no archive either.
    [Fact]
    public void A_whitespace_only_cut_has_no_trailer_and_no_archive()
    {
        var host = new MemoryHost();
        var body = Encoding.UTF8.GetBytes(
            "NAME   STATUS   AGE\nweb-0  Running  3d\napi-1  Pending  1h\n"
        );
        var shown = Encoding.UTF8.GetString(Emit(host, ["kubectl", "get", "pods"], body));
        Assert.DoesNotContain("[rtok ", shown, StringComparison.Ordinal);
        Assert.Null(Assert.Single(host.Recorded()).RefId);
    }

    [Fact]
    public void Identical_output_from_different_commands_dedups()
    {
        var host = new MemoryHost();
        var payload = Encoding.UTF8.GetBytes(Lines(400) + "\n");
        Emit(host, ["cat", "a.txt"], payload);
        var pointer = Encoding.UTF8.GetString(Emit(host, ["cat", "b.txt"], payload));
        var dedup = Assert.Single(
            host.Recorded(),
            r => string.Equals(r.Kind, "dedup", StringComparison.Ordinal)
        );
        Assert.True(dedup.BeforeBytes > dedup.AfterBytes);
        Assert.Contains("identical to a result", pointer, StringComparison.Ordinal);
    }

    // The `--agent` the rewrite embeds scopes the pointer to that sub-agent's own window.
    [Fact]
    public void Dedup_is_scoped_to_the_dispatching_agent()
    {
        var host = new MemoryHost();
        var payload = Encoding.UTF8.GetBytes(Lines(80) + "\n");
        int Dedups() =>
            host.Recorded().Count(r => string.Equals(r.Kind, "dedup", StringComparison.Ordinal));
        Emit(host, ["printf"], payload, "agent-a");
        Assert.Equal(0, Dedups());
        Emit(host, ["printf"], payload, "agent-b");
        Assert.Equal(0, Dedups());
        Emit(host, ["printf"], payload, "agent-a");
        Assert.Equal(1, Dedups());
    }

    // A tiny body never prints longer than raw, and never records a negative saving.
    [Fact]
    public void Tiny_body_never_grows()
    {
        var host = new MemoryHost();
        const string line = "root         1  0.0  00:00:01 /sbin/init worker-7 extra-flag";
        var rows = new List<string> { "USER         PID  %CPU TIME     COMMAND" };
        while (string.Join('\n', rows).Length + 1 + line.Length < 150)
        {
            rows.Add(line);
        }

        var body = Encoding.UTF8.GetBytes(string.Join('\n', rows));
        Assert.True(Emit(host, ["ps", "aux"], body).Length <= body.Length + 1);
        var row = Assert.Single(host.Recorded());
        Assert.True(row.AfterBytes <= row.BeforeBytes);
    }

    // Past the host cap, bytes the model never sees do not count as a saving.
    [Fact]
    public void Emit_ignores_bytes_past_the_host_cap()
    {
        var host = new MemoryHost();
        var lines = new List<string> { new('x', Bounded.HostVisibleChars) };
        lines.AddRange(
            Enumerable.Range(0, 50).Select(i => $"tail line {i} that the host already dropped")
        );
        Emit(host, ["echo"], Encoding.UTF8.GetBytes(string.Join('\n', lines)));
        var row = Assert.Single(host.Recorded());
        Assert.True(row.BeforeBytes > row.AfterBytes, "the tail was cut");
        Assert.Equal(row.EstBefore, row.EstAfter);
    }

    [Fact]
    public void Emit_under_the_cap_still_counts_a_shrink()
    {
        Assert.Skip(DispatchTests.PendingT18);
        var host = new MemoryHost();
        var lines = new List<string> { "running 50 tests" };
        lines.AddRange(Enumerable.Range(0, 50).Select(i => $"test a::ok{i} ... ok"));
        lines.Add("test a::bad ... FAILED");
        lines.Add("test result: FAILED. 1 failed; 50 passed");
        var shown = Encoding.UTF8.GetString(
            Emit(host, ["cargo", "test"], Encoding.UTF8.GetBytes(string.Join('\n', lines)))
        );
        Assert.Contains("[rtok ", shown, StringComparison.Ordinal);
        var row = Assert.Single(host.Recorded());
        Assert.True(row.EstBefore > row.EstAfter);
    }

    // With no store there is no archive to restore from: the command's own bytes go out.
    [Fact]
    public void Raw_bytes_survive_a_missing_store()
    {
        var body = new List<byte>();
        for (var i = 0; i < 400; i++)
        {
            body.AddRange(Encoding.UTF8.GetBytes($"line {i} é"));
            body.AddRange([0xff, (byte)'\n']);
        }

        body.AddRange("no newline at the end"u8.ToArray());
        body.Add(0xff);
        byte[] expect = [.. body, (byte)'\n'];
        Assert.Equal(expect, Emit(null, ["echo"], [.. body]));
    }
}
