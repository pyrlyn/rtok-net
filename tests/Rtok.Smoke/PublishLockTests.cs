using CliWrap;
using CliWrap.Buffered;
using Rtok.Testing;

namespace Rtok.Smoke;

/// <summary>Test processes that publish at once take turns on
/// <see cref="PublishedApps.LockAsync"/>, and each gets a working executable.</summary>
public sealed class PublishLockTests
{
    [Fact]
    public async Task Two_processes_publishing_at_once_both_get_a_working_path()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDir();
        var probe = await BuildProbeAsync(dir, ct);

        // RTOK_SMOKE_BIN off: the probes must publish, not take CI's binaries.
        Task<BufferedCommandResult> ProbeAsync(string marker) =>
            PublishedApps
                .Dotnet(
                    ["run", "--no-build", "--file", probe, "--", marker],
                    e => e.Set(PublishedApps.BinDirVariable, null)
                )
                .ExecuteBufferedAsync(ct);

        string[] markers = [dir.Combine("a"), dir.Combine("b")];
        Task<BufferedCommandResult>[] probes;
        using (await PublishedApps.LockAsync(ct))
        {
            probes = Array.ConvertAll(markers, ProbeAsync);
            // Both reach their publish while this test holds the lock; neither gets past it.
            while (!Array.TrueForAll(markers, File.Exists))
            {
                Assert.DoesNotContain(probes, p => p.IsCompleted);
                await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
            }

            await Task.Delay(TimeSpan.FromSeconds(2), ct);
            Assert.DoesNotContain(probes, p => p.IsCompleted);
        }

        // Released at once, they race for the lock and publish one after the other.
        var results = await Task.WhenAll(probes);
        foreach (var r in results)
        {
            Assert.True(r.ExitCode == 0, r.StandardOutput + r.StandardError);
        }

        var hook = Assert.Single(
            results.Select(r => r.StandardOutput.Trim()).Distinct(StringComparer.Ordinal)
        );
        await AssertRunsAsync(hook, ct);
    }

    // A file-based app standing for a test process: it marks that it is about to publish, then
    // prints the path. Built once up front: two builds of one project at once collide by
    // themselves.
    private static async Task<string> BuildProbeAsync(TempDir dir, CancellationToken ct)
    {
        var testing = Path.Join(RepoPaths.Root, "tests", "Rtok.Testing", "Rtok.Testing.csproj");
        var probe = dir.Write(
            "probe.cs",
            $"""
            #:project {testing}
            File.WriteAllText(args[0], "");
            Console.WriteLine(await Rtok.Testing.PublishedApps.PathAsync("rtok-hook"));
            """
        );
        var build = await PublishedApps
            .Dotnet(["build", probe, "-nologo"])
            .ExecuteBufferedAsync(ct);
        Assert.True(build.ExitCode == 0, build.StandardOutput + build.StandardError);
        return probe;
    }

    private static async Task AssertRunsAsync(string hook, CancellationToken ct)
    {
        using var home = new TempDir();
        var reply = await Cli.Wrap(hook)
            .WithArguments("PreToolUse")
            .WithEnvironmentVariables(e => e.Set("RTOK_HOME", home.Path))
            .WithStandardInputPipe(
                PipeSource.FromFile(RepoPaths.Fixture("hooks", "pre_tool_use.json"))
            )
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(ct);
        Assert.Equal(0, reply.ExitCode);
    }
}
