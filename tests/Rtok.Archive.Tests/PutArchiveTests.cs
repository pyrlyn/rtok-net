using System.Security.Cryptography;
using Rtok.Store.Model;

namespace Rtok.Archive.Tests;

/// <summary>Archive bodies: content-addressed files in hash-sharded directories (rtok I-S3),
/// and the row that describes them.</summary>
public sealed class PutArchiveTests : IDisposable
{
    private readonly ArchiveFixture _f = new();

    [Fact]
    public void A_body_lands_at_its_sharded_sha_path_with_a_row_that_describes_it()
    {
        byte[] body = [1, 2, 3, 0xff];
        var id = _f.Store.PutArchive("s1", body, agentId: "agent-7");

        var sha = Convert.ToHexStringLower(SHA256.HashData(body));
        Assert.Equal(sha, id);
        var path = Path.Combine(_f.ArchiveDir, sha[..2], sha);
        Assert.Equal(body, File.ReadAllBytes(path));
        Assert.Equal([path], Directory.GetFiles(_f.ArchiveDir, "*", SearchOption.AllDirectories));
        using var db = _f.Read();
        var row = Assert.Single(db.Archive);
        Assert.Equal(
            (sha, "s1", 4L, path, sha, "agent-7"),
            (row.Id, row.Session, row.Bytes, row.Path, row.Sha256, row.AgentId)
        );
    }

    [Fact]
    public void The_same_bytes_twice_keep_one_file_and_the_first_row()
    {
        byte[] body = [.. "same body\n"u8];
        var first = _f.Store.PutArchive("s1", body);
        var second = _f.Store.PutArchive("s2", body, agentId: "sub");

        Assert.Equal(first, second);
        Assert.Single(Directory.GetFiles(_f.ArchiveDir, "*", SearchOption.AllDirectories));
        using var db = _f.Read();
        var row = Assert.Single(db.Archive);
        Assert.Equal(("s1", (string?)null), (row.Session, row.AgentId));
    }

    [Fact]
    public void Get_reads_the_live_directory_then_the_stored_path()
    {
        byte[] body = [.. "moved\n"u8];
        var id = _f.Store.PutArchive("s1", body);
        var live = Path.Combine(_f.ArchiveDir, id[..2], id);
        var old = Path.Combine(Path.GetDirectoryName(_f.ArchiveDir)!, "old-" + id);
        File.Move(live, old);
        using (var db = _f.Read())
        {
            db.Archive.Single().Path = old;
            db.SaveChanges();
        }

        Assert.Equal(body, _f.Store.GetArchive(id));
        File.Delete(old);
        Assert.Null(_f.Store.GetArchive(id));
        Assert.Null(_f.Store.GetArchive(new string('0', 64)));
    }

    [Fact]
    public void Prefix_lookup_is_a_byte_exact_range()
    {
        using (var db = _f.Read())
        {
            foreach (
                var id in (string[])
                    [
                        "abcdef01" + new string('0', 56),
                        "abcdef01" + new string('1', 56),
                        "abcdef02" + new string('0', 56),
                    ]
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

        Assert.Equal(2, _f.Store.ArchiveIdsWithPrefix("abcdef01", 5).Count);
        Assert.Single(_f.Store.ArchiveIdsWithPrefix("abcdef02", 5));
        Assert.Single(_f.Store.ArchiveIdsWithPrefix("abcdef01", 1));
        Assert.Empty(_f.Store.ArchiveIdsWithPrefix("abcdef0%", 5));
        Assert.Empty(_f.Store.ArchiveIdsWithPrefix("ABCDEF01", 5));
    }

    [Fact]
    public void The_first_decision_for_a_tool_result_stays()
    {
        var id = _f.Store.PutArchive("s1", [.. "x\n"u8]);
        _f.Store.PutArchiveDecision("s1", "tu1", id, "[archived first]");
        _f.Store.PutArchiveDecision("s1", "tu1", id, "[archived second]");

        Assert.Equal("[archived first]", _f.Store.LivePointer(id));
        using var db = _f.Read();
        Assert.Single(db.ArchiveDecisions);
    }

    [Fact]
    public void The_first_summary_of_an_archive_version_stays()
    {
        var id = _f.Store.PutArchive("s1", [.. "x\n"u8]);
        Assert.Null(_f.Store.GetArchiveSummary(id, "v1"));

        Assert.True(_f.Store.PutArchiveSummary(id, "v1", "first"));
        Assert.False(_f.Store.PutArchiveSummary(id, "v1", "second"));
        Assert.True(_f.Store.PutArchiveSummary(id, "v2", "other version"));

        Assert.Equal("first", _f.Store.GetArchiveSummary(id, "v1"));
        Assert.Equal("other version", _f.Store.GetArchiveSummary(id, "v2"));
    }

    public void Dispose() => _f.Dispose();
}
