using System.Globalization;
using System.Text;
using CliWrap;
using Rtok.Archive;
using Rtok.Config;
using Rtok.Store;
using Rtok.Testing;

namespace Rtok.Cli.Tests;

/// <summary><c>rtok expand</c> against the binary: the views, the errors, and the same bytes
/// as the shared handler the MCP tool calls.</summary>
public sealed class ExpandCommandTests : IDisposable
{
    private static readonly byte[] Body = Encoding.UTF8.GetBytes(
        string.Concat(Enumerable.Range(1, 8).Select(i => $"line {i}{(i == 5 ? " needle" : "")}\n"))
    );

    private readonly TempDir _home = new("rtok-expand-");
    private readonly string _id;

    public ExpandCommandTests()
    {
        _home.Write(
            "config.toml",
            $"[core]\ndb_path = '{_home.Combine("rtok.db")}'\narchive_dir = '{_home.Combine("archive")}'\n"
        );
        using var store = Open();
        _id = store.PutArchive("seed", Body);
    }

    private string Short => _id[..8];

    [Fact]
    public async Task Expand_prints_the_original_and_its_views()
    {
        var text = new StringBuilder();
        foreach (
            var args in (string[][])
                [
                    [],
                    ["--lines", "3-5"],
                    ["--grep", "needle"],
                    ["--grep", "needle", "--context", "1"],
                ]
        )
        {
            var (code, stdout, _) = await RunAsync([Short, .. args]);
            text.Append(
                CultureInfo.InvariantCulture,
                $"$ rtok expand <id>{string.Concat(args.Select(a => $" {a}"))} (exit {code})\n{Encoding.UTF8.GetString(stdout)}"
            );
        }

        await Verify(text.ToString());
    }

    [Fact]
    public async Task A_non_hex_id_gets_the_hint_and_stdin_is_never_read()
    {
        // A stdin that never ends: reading it would hang the command past the test's timeout.
        var (code, stdout, stderr) = await RunAsync(
            ["-"],
            PipeSource.Create(async (_, ct) => await Task.Delay(Timeout.Infinite, ct))
        );

        Assert.Equal(1, code);
        Assert.Empty(stdout);
        Assert.Contains(
            "unknown archive id: - (expand takes the hex id from an \"expand <id>\" trailer; it does not read stdin)",
            stderr,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task The_cli_prints_what_the_shared_handler_returns()
    {
        var config = ConfigLoader
            .Load(new ConfigLoadOptions { ConfigFile = _home.Combine("config.toml") })
            .Config;
        foreach (
            var request in (ExpandRequest[])
                [
                    new(Short),
                    new(_id, Lines: "3-5"),
                    new(Short, Grep: "needle"),
                    new(Short, Grep: "needle", Context: 1),
                    new(Short, Lines: "7-20", Grep: "line"),
                    new(Short, Lines: "9-10"),
                    new("zz"),
                ]
        )
        {
            ExpandResult expected;
            using (var store = Open())
            {
                expected = new Expander(store, config, Expander.CliSession).Run(request);
            }
            List<string> args = [request.Id];
            if (request.Lines is { } lines)
            {
                args.AddRange(["--lines", lines]);
            }
            if (request.Grep is { } grep)
            {
                args.AddRange(["--grep", grep]);
            }
            if (request.Context > 0)
            {
                args.AddRange(["--context", $"{request.Context}"]);
            }

            var (code, stdout, stderr) = await RunAsync([.. args]);

            if (expected.Ok)
            {
                Assert.Equal(0, code);
                Assert.Equal(expected.Body, stdout);
            }
            else
            {
                Assert.Equal(1, code);
                Assert.Contains(expected.Error, stderr, StringComparison.Ordinal);
            }
        }
    }

    private RtokStore Open() =>
        RtokStore.Open(
            new StoreOptions(_home.Combine("rtok.db")) { ArchiveDir = _home.Combine("archive") }
        );

    private async Task<(int Code, byte[] Stdout, string Stderr)> RunAsync(
        string[] args,
        PipeSource? stdin = null
    )
    {
        using var stdout = new MemoryStream();
        var stderr = new StringBuilder();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken
        );
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        // CliWrap keeps only the last environment given, so it is built here in one go.
        var result = await CliWrap
            .Cli.Wrap(RtokBinary.Path)
            .WithArguments(["expand", .. args, "--config", _home.Combine("config.toml")])
            .WithWorkingDirectory(_home.Path)
            .WithEnvironmentVariables(e =>
                e.Set("RTOK_HOME", _home.Path)
                    .Set("RTOK_CONFIG", null)
                    .Set("RTOK_LOG", null)
                    .Set("NO_COLOR", "1")
            )
            .WithStandardInputPipe(stdin ?? PipeSource.Null)
            .WithStandardOutputPipe(PipeTarget.ToStream(stdout))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stderr))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(timeout.Token);
        return (result.ExitCode, stdout.ToArray(), stderr.ToString());
    }

    public void Dispose() => _home.Dispose();
}
