using System.Text;
using CsCheck;

namespace Rtok.Doctor.Tests;

/// <summary><c>--fix</c> on generated host files only deletes: the fixed entries go and every other byte stays, in order.</summary>
public sealed class ByteKeepTests
{
    private static readonly IReadOnlyList<string> Broken = [ProblemKinds.BrokenHook];
    private static readonly IReadOnlyList<string> DupMcp = [ProblemKinds.DuplicateMcp];

    private static readonly Gen<bool[][][]> Settings = Gen.Bool.Array[1, 3].Array[1, 3].Array[1, 3];

    private static readonly Gen<int[]> Servers = Gen.Int[0, 2].Array[1, 6];

    [Fact]
    public void Fixing_broken_hooks_in_jsonc_keeps_every_other_byte() =>
        Gen.Select(Settings, Gen.Bool, Gen.Bool)
            .Sample(t =>
            {
                var (events, comments, trailing) = t;
                var f = new DoctorFake();
                f.Exe("fmt");
                var (text, ok, broken) = Jsonc(events, comments, trailing);
                var path = f.At(".claude/settings.json");
                f.Write(path, text);

                DoctorFix.Run(f.Options(), Broken, apply: true, stat: false);

                var after = f.Read(path)!;
                Assert.True(Deletes(text, after), $"not only deletions:\n{text}\n---\n{after}");
                Assert.DoesNotContain("gone-", after, StringComparison.Ordinal);
                Assert.Equal(ok, f.Page().HooksTotal);
                if (comments)
                {
                    Assert.All(
                        Comments(text),
                        c => Assert.Contains(c, after, StringComparison.Ordinal)
                    );
                }
                Assert.Contains("\"model\": \"opus\"", after, StringComparison.Ordinal);
                Assert.True(broken == 0 || !string.Equals(text, after, StringComparison.Ordinal));
            });

    [Fact]
    public void Removing_duplicate_toml_servers_keeps_every_other_byte() =>
        Gen.Select(Servers, Gen.Bool)
            .Sample(t =>
            {
                var (commands, comments) = t;
                var f = new DoctorFake();
                foreach (var c in commands.Distinct())
                {
                    f.Exe($"tool{c}");
                }
                var text = Toml(commands, comments);
                var path = f.At(".codex/config.toml");
                f.Write(path, text);

                DoctorFix.Run(f.Options(), DupMcp, apply: true, stat: false);

                var after = f.Read(path)!;
                Assert.True(Deletes(text, after), $"not only deletions:\n{text}\n---\n{after}");
                for (var c = 0; c <= 2; c++)
                {
                    Assert.Equal(
                        commands.Contains(c) ? 1 : 0,
                        Count(after, $"command = \"tool{c}\"")
                    );
                }
                if (comments)
                {
                    Assert.All(
                        Comments(text),
                        c => Assert.Contains(c, after, StringComparison.Ordinal)
                    );
                }
                Assert.Contains(
                    "[profiles.fast]\nmodel = \"mini\"\n",
                    after,
                    StringComparison.Ordinal
                );
            });

    private static (string Text, int Ok, int Broken) Jsonc(
        bool[][][] events,
        bool comments,
        bool trailing
    )
    {
        string[] names = ["Stop", "PreToolUse", "SessionStart"];
        var sb = new StringBuilder("{\n");
        if (comments)
        {
            sb.Append("  // top note\n");
        }
        sb.Append("  \"model\": \"opus\",\n  \"hooks\": {\n");
        int ok = 0,
            broken = 0,
            n = 0;
        for (var e = 0; e < events.Length; e++)
        {
            sb.Append("    \"").Append(names[e]).Append("\": [\n");
            for (var g = 0; g < events[e].Length; g++)
            {
                if (comments)
                {
                    sb.Append("      // group ").Append(e).Append('.').Append(g).Append('\n');
                }
                sb.Append("      {\"hooks\": [");
                var hooks = events[e][g];
                for (var h = 0; h < hooks.Length; h++)
                {
                    var command = hooks[h] ? $"gone-{n++}" : $"fmt --n {n++}";
                    _ = hooks[h] ? broken++ : ok++;
                    sb.Append(h == 0 ? "" : ", ")
                        .Append("{\"type\": \"command\", \"command\": \"")
                        .Append(command)
                        .Append("\"}");
                }
                sb.Append(trailing ? ",]}" : "]}")
                    .Append(g < events[e].Length - 1 || trailing ? ",\n" : "\n");
            }
            sb.Append("    ]").Append(e < events.Length - 1 ? ",\n" : "\n");
        }
        sb.Append("  }\n}\n");
        return (sb.ToString(), ok, broken);
    }

    private static string Toml(int[] commands, bool comments)
    {
        var sb = new StringBuilder();
        if (comments)
        {
            sb.Append("# my codex config\n");
        }
        sb.Append("model = \"gpt\"\n\n");
        for (var i = 0; i < commands.Length; i++)
        {
            if (comments)
            {
                sb.Append("# server ").Append(i).Append('\n');
            }
            sb.Append("[mcp_servers.s")
                .Append(i)
                .Append("]\ncommand = \"tool")
                .Append(commands[i])
                .Append("\"\n\n");
            if (i == 0)
            {
                sb.Append("[profiles.fast]\nmodel = \"mini\"\n\n");
            }
        }
        return sb.ToString();
    }

    private static IEnumerable<string> Comments(string text) =>
        text.Split('\n')
            .Select(l => l.Trim())
            .Where(l =>
                l.StartsWith("//", StringComparison.Ordinal)
                    && !l.Contains("group", StringComparison.Ordinal)
                || l.StartsWith("# my", StringComparison.Ordinal)
            );

    /// <summary><paramref name="after"/> is <paramref name="before"/> with some characters taken out.</summary>
    private static bool Deletes(string before, string after)
    {
        var i = 0;
        foreach (var c in after)
        {
            while (i < before.Length && before[i] != c)
            {
                i++;
            }
            if (i == before.Length)
            {
                return false;
            }
            i++;
        }
        return true;
    }

    private static int Count(string text, string part) =>
        (text.Length - text.Replace(part, "", StringComparison.Ordinal).Length) / part.Length;
}
