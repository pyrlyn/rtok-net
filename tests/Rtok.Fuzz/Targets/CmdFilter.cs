using System.Text;
using System.Text.Unicode;
using Rtok.Config;
using Rtok.Plugin.Sdk.Testing;
using Rtok.Plugins.Cmd;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// Any output through the cmd plugin as the output of a command the first byte picks: the
/// <c>run</c> path (<c>filter --archive</c>) prints no more than the input and a newline (the
/// lossy text and a trailer line for invalid UTF-8) and archives the input byte for byte; the
/// stdin filter prints no more than its text.
/// </summary>
internal static class CmdFilter
{
    private static readonly string[] Hints =
    [
        "cat build.log",
        "grep -rn needle src",
        "find . -name *.rs",
        "ls -la",
        "git status",
        "cargo test",
        "pytest -q",
        "kubectl get pods",
        "docker ps",
        "mise exec -- cargo build",
        "cd crates/x && cargo test",
        "jq . data.json",
        "make",
        "just check",
        "npm test",
        "",
    ];

    private static readonly CmdSection Section = new();

    // `\n[rtok 12345678 · N lines]\n` with room for any line count.
    private const int TrailerLine = 48;

    // No user rules: a home that does not exist leaves the built-ins.
    private static readonly string NoHome = Path.Combine(Path.GetTempPath(), "rtok-fuzz-no-home");

    public static void Run(ReadOnlySpan<byte> data)
    {
        var pick = data.IsEmpty ? 0 : data[0];
        var input = data.IsEmpty ? [] : data[1..].ToArray();
        var hint = Hints[pick % Hints.Length];
        var host = new MemoryHost();
        using var output = new MemoryStream();
        if ((pick & 0x10) == 0)
        {
            CmdCommands.Filter(Section, hint, input, true, pick >> 5, host, output, Env);
            // Invalid UTF-8 never prints raw: the lossy text and a pointer to the exact bytes.
            var limit = Utf8.IsValid(input)
                ? input.Length + 1
                : Encoding.UTF8.GetByteCount(Encoding.UTF8.GetString(input)) + TrailerLine;
            FuzzInput.Check(output.Length <= limit, "the run path grew the output");
            foreach (var row in host.Recorded())
            {
                if (row.RefId is { } refId)
                {
                    var archived = host.GetArchive(refId[(refId.IndexOf(':') + 1)..]);
                    FuzzInput.Check(
                        archived is not null && archived.AsSpan().SequenceEqual(input),
                        "the archive is not the input"
                    );
                }
            }
            return;
        }

        CmdCommands.Filter(Section, hint, input, false, 0, host, output, Env);
        var text = Encoding.UTF8.GetString(input);
        FuzzInput.Check(
            output.Length <= Encoding.UTF8.GetByteCount(text),
            "the stdin filter grew the text"
        );
    }

    private static string? Env(string name) =>
        name is "RTOK_HOME" or "HOME" or "USERPROFILE" ? NoHome : null;
}
