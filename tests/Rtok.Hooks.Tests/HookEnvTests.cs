using System.Runtime.Versioning;
using Rtok.Hook.Contracts;
using Rtok.Testing;

namespace Rtok.Hooks.Tests;

// The fingerprint and the endpoint: what client and resident must agree on (the home is
// RtokHome's, tested in Rtok.Config.Tests).
public sealed class HookEnvTests
{
    private static ulong Fp(params (string, string)[] vars) =>
        HookFingerprint.Of(vars.Select(v => new KeyValuePair<string, string>(v.Item1, v.Item2)));

    [Fact]
    public void The_fingerprint_ignores_order_and_unrelated_variables() =>
        Assert.Equal(
            Fp(("RTOK_LOG", "debug"), ("HOME", "/h")),
            Fp(("PATH", "/bin"), ("HOME", "/h"), ("TERM", "x"), ("RTOK_LOG", "debug"))
        );

    [Theory]
    [InlineData("RTOK_LOG")]
    [InlineData("HOME")]
    [InlineData("USERPROFILE")]
    [InlineData("OTEL_EXPORTER_OTLP_ENDPOINT")]
    [InlineData("OTEL_EXPORTER_OTLP_HEADERS")]
    public void The_fingerprint_follows_config_variables(string name) =>
        Assert.NotEqual(
            Fp(("HOME", "/h")),
            Fp(
                ("HOME", "/h"),
                (string.Equals(name, "HOME", StringComparison.Ordinal) ? "RTOK_X" : name, "1")
            )
        );

    // Host variables travel in the request, so one resident serves every call.
    [Theory]
    [InlineData("GROK_HOOK_EVENT")]
    [InlineData("DEVIN_PROJECT_DIR")]
    public void Host_variables_are_not_in_the_fingerprint(string name) =>
        Assert.Equal(Fp(("HOME", "/h")), Fp(("HOME", "/h"), (name, "1")));

    // "a\0b=" and "a=\0b" must not collide: the separators are part of the hash.
    [Fact]
    public void Names_and_values_do_not_run_together() =>
        Assert.NotEqual(Fp(("RTOK_A", "B")), Fp(("RTOK_AB", "")));

    private static readonly string User = OperatingSystem.IsWindows() ? @"C:\Users\me" : "/home/me";

    [Fact]
    public void Everything_lives_in_the_run_directory()
    {
        var endpoint = HookEndpoint.For(Path.Join(User, ".rtok"))!;

        Assert.Equal(Path.Join(User, ".rtok", "run"), endpoint.RunDir);
        Assert.All(
            [endpoint.LockPath, endpoint.SpawnLockPath, endpoint.QueueDir],
            p => Assert.Equal(endpoint.RunDir, Path.GetDirectoryName(p))
        );
        if (OperatingSystem.IsWindows())
        {
            Assert.StartsWith("rtok-hook-", endpoint.Address, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(Path.Join(endpoint.RunDir, "hook.sock"), endpoint.Address);
        }
    }

    [Fact]
    public void A_socket_path_past_sun_path_has_no_endpoint()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("pipes have no path limit");
        }
        Assert.Null(HookEndpoint.For("/" + new string('h', 90)));
        Assert.NotNull(HookEndpoint.For("/" + new string('h', 70)));
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void The_run_directory_is_forced_to_0700()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix modes");
        }
        using var dir = new TempDir();
        var run = dir.Combine("run");
        Directory.CreateDirectory(run, (UnixFileMode)0b111_101_101);

        HookEndpoint.EnsurePrivateDir(run);

        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(run)
        );
    }

    [Fact]
    public void A_symlinked_run_directory_is_refused()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("symlinks need privileges");
        }
        using var dir = new TempDir();
        Directory.CreateDirectory(dir.Combine("elsewhere"));
        Directory.CreateSymbolicLink(dir.Combine("run"), dir.Combine("elsewhere"));

        Assert.Throws<IOException>(() => HookEndpoint.EnsurePrivateDir(dir.Combine("run")));
    }

    [Fact]
    public void The_backlog_replays_oldest_first_once()
    {
        using var dir = new TempDir();
        var endpoint = HookEndpoint.For(dir.Combine("h"))!;

        Assert.True(HookBacklog.Enqueue(endpoint, [1]));
        Assert.True(HookBacklog.Enqueue(endpoint, [2]));

        Assert.Equal(
            [
                [1],
                [2],
            ],
            HookBacklog.Drain(endpoint).ToList()
        );
        Assert.Empty(HookBacklog.Drain(endpoint));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                File.GetUnixFileMode(endpoint.QueueDir)
            );
        }
    }

    [Fact]
    public void A_full_backlog_takes_no_more()
    {
        using var dir = new TempDir();
        var endpoint = HookEndpoint.For(dir.Combine("h"))!;
        for (var i = 0; i < HookBacklog.MaxQueued; i++)
        {
            Assert.True(HookBacklog.Enqueue(endpoint, [1]));
        }

        Assert.False(HookBacklog.Enqueue(endpoint, [1]));
        Assert.Equal(HookBacklog.MaxQueued, HookBacklog.Drain(endpoint).Count());
    }
}
