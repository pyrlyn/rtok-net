using Rtok.Agents.Hosts;
using Rtok.Config;
using Rtok.Doctor;
using Rtok.Mcp.Install;
using Rtok.Testing;
using Testably.Abstractions.Testing;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// Any text as a host config <c>rtok doctor</c> reads (Claude settings JSONC, <c>~/.claude.json</c>,
/// a project <c>.mcp.json</c>, Codex TOML), next to a fixed file it may duplicate: the report and
/// the fix plan never throw, a file that does not parse is a finding, and every planned edit only
/// deletes bytes; applying the plan writes exactly the planned text.
/// </summary>
internal static class DoctorConfig
{
    private const string Home = "/home/u";

    private static readonly string[] Paths =
    [
        ".claude/settings.json",
        ".claude.json",
        "proj/.mcp.json",
        ".codex/config.toml",
    ];

    /// <summary>The companion the fuzzed file may repeat: one hook and one MCP server.</summary>
    private const string Companion = """
        {
          "hooks": {"PreToolUse": [{"matcher": "Bash", "hooks": [{"type": "command", "command": "/home/u/bin/fmt"}]}]},
          "mcpServers": {"fuzz": {"command": "/home/u/bin/fmt", "args": ["serve"]}}
        }
        """;

    public static void Run(ReadOnlySpan<byte> data)
    {
        var (pick, text) = FuzzInput.Picked(data);
        var fs = MemFs.Linux();
        var path = Home + "/" + Paths[pick % Paths.Length];
        fs.Directory.CreateDirectory(Home + "/proj");
        Write(fs, Home + "/bin/fmt", "");
        Write(fs, Home + "/.claude/settings.local.json", Companion);
        Write(fs, path, text);
        var options = Options(fs);

        var report = DoctorPage.Page(options);
        _ = DoctorText.Render(report);
        var session = new FixSession(new DoctorHome(options), new FixRequest());
        foreach (var file in session.Files)
        {
            FuzzInput.Check(
                file.Error is not null || Deletes(file.Before, file.After),
                "a fix does more than delete"
            );
            FuzzInput.Check(
                file.Error is null || file.Error.Length > 0,
                "a skipped file without a reason"
            );
        }
        var planned = session
            .Files.Where(f => f.Error is null)
            .ToDictionary(f => f.Source, f => f.After, StringComparer.Ordinal);
        var result = DoctorFix.Apply(options, new FixRequest(), session.Token());
        FuzzInput.Check(
            result.Replan is null,
            "the plan changed between two reads of the same disk"
        );
        foreach (var (source, after) in planned)
        {
            FuzzInput.Check(
                result.Code != 0
                    || string.Equals(fs.File.ReadAllText(source), after, StringComparison.Ordinal),
                "apply wrote something else than the plan"
            );
        }
    }

    private static DoctorOptions Options(MockFileSystem fs)
    {
        var env = new HostEnvironment(
            Home,
            McpOs.Linux,
            v =>
                v switch
                {
                    "PATH" => Home + "/bin",
                    HostEnvironment.SandboxVariable => "1",
                    _ => null,
                }
        );
        var hosts = new HostContext(
            env,
            new McpDisk(fs),
            (_, _, _) => ("1.0.0 (fuzz)", ""),
            new RtokBinary("/opt/rtok/bin/rtok"),
            new HostSettings(DryRun: false, Yes: false, Backup: false),
            null
        );
        return new DoctorOptions(hosts, new RtokConfig(), Home + "/proj")
        {
            Managed = [],
            Write = (target, body) =>
            {
                fs.File.WriteAllText(target, body);
                return null;
            },
        };
    }

    private static void Write(MockFileSystem fs, string path, string text)
    {
        fs.Directory.CreateDirectory(fs.Path.GetDirectoryName(path)!);
        fs.File.WriteAllText(path, text);
    }

    /// <summary>Whether <paramref name="after"/> is <paramref name="before"/> with some characters removed.</summary>
    private static bool Deletes(string before, string after)
    {
        var i = 0;
        foreach (var c in before)
        {
            if (i < after.Length && after[i] == c)
            {
                i++;
            }
        }
        return i == after.Length;
    }
}
