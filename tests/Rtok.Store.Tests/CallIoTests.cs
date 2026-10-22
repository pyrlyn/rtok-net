using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Rtok.Store.Tests;

public sealed class CallIoTests
{
    private static StoreHarness Harness(Func<StoreOptions, StoreOptions>? configure = null)
    {
        var h = new StoreHarness(o =>
        {
            o = o with { CallIoInlineBytes = 32 };
            return configure?.Invoke(o) ?? o;
        });
        h.Store.UpsertSession("s", null, null, null, null);
        return h;
    }

    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    [Fact]
    public void A_small_body_is_inline_and_its_request_cleaned()
    {
        using var h = Harness();
        var call = h.Store.InsertCall("s", "proxy", "api");

        h.Store.InsertCallIo(
            call,
            "{\"a\":\"x  \\r\\n\"}"u8.ToArray(),
            "{\"b\":\"y  \"}"u8.ToArray()
        );

        using var db = h.Read();
        var io = db.CallIo.Single();
        Assert.Equal("""{"a":"x\n"}""", io.RequestJson);
        Assert.Equal("""{"b":"y  "}""", io.ResponseJson);
        Assert.Equal(
            (11L, Sha("""{"a":"x\n"}"""u8.ToArray())),
            (io.RequestBytes, io.RequestSha256)
        );
        Assert.Equal(Sha("""{"b":"y  "}"""u8.ToArray()), io.ResponseSha256);
        Assert.Null(io.RequestArchive);
        Assert.Null(io.RequestRaw);
    }

    [Fact]
    public void Store_raw_saves_the_request_verbatim()
    {
        using var h = Harness(o => o with { StoreRaw = true });
        var call = h.Store.InsertCall("s", "proxy", "api");

        h.Store.InsertCallIo(call, "{\"a\":\"x  \"}"u8.ToArray(), null);

        using var db = h.Read();
        var io = db.CallIo.Single();
        Assert.Equal("{\"a\":\"x  \"}", io.RequestJson);
        Assert.Equal((0L, null), (io.ResponseBytes, io.ResponseSha256));
    }

    [Fact]
    public void Bytes_that_are_not_utf8_are_kept_exactly()
    {
        using var h = Harness();
        var call = h.Store.InsertCall("s", "proxy", "api");
        byte[] response = [0xff, 0x00, 0x41];

        h.Store.InsertCallIo(call, null, response);

        using var db = h.Read();
        var io = db.CallIo.Single();
        Assert.Equal(response, io.ResponseRaw);
        Assert.Equal(Sha(response), io.ResponseSha256);
        Assert.NotNull(io.ResponseJson);
    }

    [Fact]
    public void A_large_body_is_archived_once_by_content()
    {
        using var h = Harness();
        var first = h.Store.InsertCall("s", "proxy", "api");
        var second = h.Store.InsertCall("s", "proxy", "api");
        var big = Encoding.UTF8.GetBytes(new string('x', 100));

        h.Store.InsertCallIo(first, big, big);
        h.Store.InsertCallIo(second, big, null);

        using var db = h.Read();
        var archive = db.Archive.Single();
        Assert.Equal((Sha(big), 100L, "s"), (archive.Id, archive.Bytes, archive.Session));
        Assert.Equal(big, File.ReadAllBytes(archive.Path));
        Assert.Equal(
            Path.Combine(h.Options.ArchiveDir!, archive.Id[..2], archive.Id),
            archive.Path
        );
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(archive.Path)
            );
        }
        var io = db.CallIo.Single(x => x.CallId == first);
        Assert.Equal(
            (archive.Id, archive.Id, null, 100L),
            (io.RequestArchive, io.ResponseArchive, io.RequestJson, io.RequestBytes)
        );
        Assert.Single(Directory.GetFiles(h.Options.ArchiveDir!, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void Without_an_archive_dir_a_large_body_keeps_only_its_size()
    {
        using var h = Harness(o => o with { ArchiveDir = null });
        var call = h.Store.InsertCall("s", "proxy", "api");

        h.Store.InsertCallIo(call, Encoding.UTF8.GetBytes(new string('x', 100)), null);

        using var db = h.Read();
        var io = db.CallIo.Single();
        Assert.Equal(
            (100L, null, null, null),
            (io.RequestBytes, io.RequestSha256, io.RequestJson, io.RequestArchive)
        );
        Assert.Empty(db.Archive);
    }

    [Fact]
    public void A_failed_insert_removes_the_archive_file_it_made()
    {
        using var h = Harness();
        var call = h.Store.InsertCall("s", "proxy", "api");
        h.Store.InsertCallIo(call, null, null);

        Assert.Throws<DbUpdateException>(() =>
            h.Store.InsertCallIo(call, Encoding.UTF8.GetBytes(new string('y', 100)), null)
        );

        Assert.Empty(Directory.GetFiles(h.Options.ArchiveDir!, "*", SearchOption.AllDirectories));
        using var db = h.Read();
        Assert.Empty(db.Archive);
    }

    [Fact]
    public void A_hook_saves_its_session_fields_once()
    {
        using var h = Harness(o => o with { CallIoInlineBytes = 4096 });
        var first = h.Store.InsertCall("s", "hook", "hook");
        var second = h.Store.InsertCall("s", "hook", "hook");
        var stdin =
            """{"session_id":"s","cwd":"/w","tool_name":"Bash","tool_input":{"command":"ls  "}}"""u8.ToArray();

        h.Store.InsertHookCallIo(first, stdin, "{}"u8.ToArray());
        h.Store.InsertHookCallIo(
            second,
            """{"session_id":"s","cwd":"/w","prompt":"hi"}"""u8.ToArray(),
            null
        );

        using var db = h.Read();
        var session = db.HookSessions.Single();
        Assert.Equal("""{"session_id":"s","cwd":"/w"}""", session.Fields);
        var io = db.CallIo.Single(x => x.CallId == first);
        Assert.Equal(session.Id, io.HookSessionId);
        Assert.Equal("""{"tool_name":"Bash","tool_input":{"command":"ls"}}""", io.RequestJson);
        Assert.Equal(
            """{"session_id":"s","cwd":"/w","tool_name":"Bash","tool_input":{"command":"ls"}}""",
            HookFields.Rebuild(io.RequestJson!, session.Fields)
        );
        Assert.Equal("{}", io.ResponseJson);
    }

    [Theory]
    [InlineData(false, 32, """{"tool_name":"x"}""")]
    [InlineData(true, 4096, """{"session_id":"s","x":"  "}""")]
    [InlineData(false, 8, """{"session_id":"s"}""")]
    public void A_hook_body_that_is_not_split_is_saved_whole(bool raw, int inline, string stdin)
    {
        using var h = Harness(o => o with { StoreRaw = raw, CallIoInlineBytes = inline });
        var call = h.Store.InsertCall("s", "hook", "hook");

        h.Store.InsertHookCallIo(call, Encoding.UTF8.GetBytes(stdin), null);

        using var db = h.Read();
        var io = db.CallIo.Single();
        Assert.Null(io.HookSessionId);
        Assert.Empty(db.HookSessions);
        Assert.Null(io.RequestArchive);
        Assert.Equal(stdin.Length, io.RequestBytes);
        Assert.Equal(stdin.Length <= inline ? stdin : null, io.RequestJson);
    }
}
