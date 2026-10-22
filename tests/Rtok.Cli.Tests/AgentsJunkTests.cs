using System.Runtime.Versioning;
using Rtok.Testing;

namespace Rtok.Cli.Tests;

/// <summary><c>rtok agents junk list|clear</c> end to end on a fake home with Claude Code installed.</summary>
public sealed class AgentsJunkTests : IDisposable
{
    /// <summary>A fixed past mtime, so <c>last_used</c> is the same on every run.</summary>
    private static readonly DateTime Old = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private readonly TempDir _home = new("rtok-junk-");

    public AgentsJunkTests()
    {
        _home.Write("bin/claude", "");
        Aged(".claude/paste-cache/a");
        Aged(".claude/paste-cache/b");
    }

    public void Dispose() => _home.Dispose();

    private void Aged(string relative) =>
        File.SetLastWriteTimeUtc(_home.Write(relative, new string('x', 5000)), Old);

    [Theory]
    [InlineData("text", "--items", "0")]
    [InlineData("json", "--json")]
    public Task List_shows_the_folders_and_what_clear_frees(string name, params string[] flags)
    {
        var run = Junk(["list", "--bytes", .. flags]);

        Assert.Equal((0, ""), (run.Code, run.Stderr));
        return Verify(run.Stdout.Replace(_home.Path, "{home}", StringComparison.Ordinal))
            .UseParameters(name);
    }

    [Fact]
    public void Clear_without_a_flag_has_nothing_of_rtoks_own()
    {
        var run = Junk("clear");

        Assert.Equal((0, "Nothing to clear\n", ""), run);
    }

    [Fact]
    public void Clear_is_a_dry_run_until_yes()
    {
        var dry = Junk("clear", "--kind", "cache");

        Assert.Equal(0, dry.Code);
        Assert.Contains("rerun with --yes", dry.Stdout, StringComparison.Ordinal);
        Assert.True(File.Exists(_home.Combine(".claude", "paste-cache", "a")));

        var run = Junk("clear", "--kind", "cache", "--yes");

        Assert.Equal(0, run.Code);
        Assert.StartsWith("Freed ", run.Stdout.Split('\n')[^2], StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_home.Combine(".claude", "paste-cache")));
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void An_item_that_cannot_go_fails_the_run()
    {
        Assert.SkipWhen(
            OperatingSystem.IsWindows(),
            "a read-only folder still allows deletes on NTFS"
        );
        var cache = _home.Combine(".claude", "paste-cache");
        File.SetUnixFileMode(cache, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var run = Junk("clear", "--kind", "cache", "--yes", "--json");

            Assert.Equal((1, "error: some junk could not be removed\n"), (run.Code, run.Stderr));
            Assert.Contains("\"failed\": true", run.Stdout, StringComparison.Ordinal);
        }
        finally
        {
            File.SetUnixFileMode(
                cache,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            );
        }
    }

    [Theory]
    [InlineData("not a junk kind: bogus", "clear", "--kind", "bogus")]
    [InlineData("not an agent: nobody", "clear", "--agent", "nobody")]
    [InlineData("--include takes review, not all", "clear", "--include", "all")]
    [InlineData("--older-than:", "clear", "--older-than", "soon")]
    [InlineData("--min-size takes a size like 10M, not big", "clear", "--min-size", "big")]
    [InlineData("--items takes a count or all, not many", "list", "--items", "many")]
    [InlineData("--sort takes size, last-used or path, not age", "list", "--sort", "age")]
    public void A_bad_flag_is_a_usage_error(string error, params string[] args)
    {
        var run = Junk(args);

        Assert.Equal(2, run.Code);
        Assert.Contains("Error: " + error, run.Stderr, StringComparison.Ordinal);
    }

    private (int Code, string Stdout, string Stderr) Junk(params string[] args)
    {
        static string Q(string path) => "'" + path + "'";
        var config = _home.Write(
            "config.toml",
            $"""
            [core]
            db_path = {Q(_home.Combine(".rtok", "rtok.db"))}
            archive_dir = {Q(_home.Combine(".rtok", "archive"))}

            [log]
            path = {Q(_home.Combine(".rtok", "logs", "rtok.log"))}
            """
        );
        return RtokBinary.Exec(
            ["agents", "junk", .. args, "--config", config],
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["HOME"] = _home.Path,
                ["USERPROFILE"] = _home.Path,
                ["RTOK_HOME"] = _home.Combine(".rtok"),
                ["XDG_CACHE_HOME"] = _home.Combine(".cache"),
                ["PATH"] =
                    _home.Combine("bin")
                    + Path.PathSeparator
                    + Environment.GetEnvironmentVariable("PATH"),
            }
        );
    }
}
