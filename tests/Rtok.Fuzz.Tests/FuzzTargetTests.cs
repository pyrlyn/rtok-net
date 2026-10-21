using CsCheck;
using Rtok.Testing;

namespace Rtok.Fuzz.Tests;

/// <summary>
/// Every wired target over its seed corpus, random bytes and mutated seeds, so a regression
/// shows on <c>dotnet test</c> without libFuzzer. A crash prints the input as base64.
/// </summary>
public sealed class FuzzTargetTests
{
    public static TheoryData<string> Wired =>
        [.. FuzzTargets.All.Where(t => t.Body is not null).Select(t => t.Name)];

    [Theory]
    [MemberData(nameof(Wired))]
    public void CorpusRuns(string name)
    {
        var (target, seeds) = Load(name);
        Assert.NotEmpty(seeds);
        foreach (var seed in seeds)
        {
            Run(target, seed);
        }
    }

    [Theory]
    [MemberData(nameof(Wired))]
    public void RandomBytesRun(string name)
    {
        var (target, _) = Load(name);
        Gen.Byte.Array[0, 256].Sample(bytes => Run(target, bytes), iter: 1000);
    }

    [Theory]
    [MemberData(nameof(Wired))]
    public void MutatedSeedsRun(string name)
    {
        var (target, seeds) = Load(name);
        Gen.Select(
                Gen.Int[0, seeds.Length - 1],
                Gen.Int[0, 4],
                Gen.Int.Positive,
                Gen.Byte.Array[1, 8]
            )
            .Sample(
                (pick, kind, at, bytes) => Run(target, Mutate(seeds[pick], kind, at, bytes)),
                iter: 2000
            );
    }

    [Fact]
    public void NamesAreUnique() =>
        Assert.Equal(
            FuzzTargets.All.Length,
            FuzzTargets.All.Select(t => t.Name).Distinct(StringComparer.Ordinal).Count()
        );

    /// <summary>A closed task leaves its targets wired, and a wired target has a corpus.</summary>
    [Fact]
    public void ClosedTasksWireTheirTargets()
    {
        foreach (var target in FuzzTargets.All)
        {
            var closed = File.Exists(Path.Combine(RepoPaths.Root, "plan", target.Task, "done.md"));
            Assert.False(
                closed && target.Body is null,
                $"{target.Name}: {target.Task} is closed but the target has no body"
            );
            if (target.Body is not null)
            {
                Assert.True(Directory.Exists(Corpus(target.Name)), $"{target.Name}: no corpus");
            }
        }
    }

    /// <summary>Each target named in an open task's <c>Fuzz:</c> line is registered under that task.</summary>
    [Fact]
    public void FuzzLinesMatchTargets()
    {
        foreach (var dir in Directory.GetDirectories(Path.Combine(RepoPaths.Root, "plan")))
        {
            if (File.Exists(Path.Combine(dir, "done.md")))
            {
                continue;
            }
            var task = Path.GetFileName(dir);
            foreach (var name in FuzzLineNames(File.ReadAllLines(Path.Combine(dir, "task.md"))))
            {
                var target = Array.Find(
                    FuzzTargets.All,
                    t => string.Equals(t.Name, name, StringComparison.Ordinal)
                );
                Assert.True(target is not null, $"{task} names `{name}`, which FuzzTargets lacks");
                Assert.Equal(task, target.Task);
            }
        }
    }

    /// <summary>The backticked names before <c>in `tests/Rtok.Fuzz`</c> on a <c>Fuzz:</c> line.</summary>
    internal static IEnumerable<string> FuzzLineNames(IEnumerable<string> lines)
    {
        const string Marker = " in `tests/Rtok.Fuzz`";
        foreach (var line in lines)
        {
            var end = line.IndexOf(Marker, StringComparison.Ordinal);
            if (!line.StartsWith("Fuzz: ", StringComparison.Ordinal) || end < 0)
            {
                continue;
            }
            var names = line["Fuzz: ".Length..end].Split('`');
            for (var i = 1; i < names.Length; i += 2)
            {
                yield return names[i];
            }
        }
    }

    private static string Corpus(string name) =>
        Path.Combine(RepoPaths.Root, "tests", "Rtok.Fuzz", "corpus", name);

    private static (FuzzTarget Target, byte[][] Seeds) Load(string name)
    {
        var target = Array.Find(
            FuzzTargets.All,
            t => string.Equals(t.Name, name, StringComparison.Ordinal)
        )!;
        var dir = Corpus(name);
        byte[][] seeds = Directory.Exists(dir)
            ? [.. Directory.GetFiles(dir).Order(StringComparer.Ordinal).Select(File.ReadAllBytes)]
            : [];
        return (
            target,
            seeds.Length == 0
                ?
                [
                    [],
                ]
                : seeds
        );
    }

    private static void Run(FuzzTarget target, byte[] input)
    {
        try
        {
            target.Body!(input);
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(
                $"{target.Name} failed on base64 {Convert.ToBase64String(input)}",
                e
            );
        }
    }

    /// <summary>One libFuzzer-style edit: flip, insert, delete, overwrite or truncate.</summary>
    private static byte[] Mutate(byte[] seed, int kind, int at, byte[] bytes)
    {
        if (seed.Length == 0)
        {
            return bytes;
        }
        var i = at % seed.Length;
        var list = seed.ToList();
        switch (kind)
        {
            case 0:
                list[i] ^= bytes[0];
                break;
            case 1:
                list.InsertRange(i, bytes);
                break;
            case 2:
                list.RemoveRange(i, Math.Min(bytes.Length, list.Count - i));
                break;
            case 3:
                for (var k = 0; k < bytes.Length && i + k < list.Count; k++)
                {
                    list[i + k] = bytes[k];
                }
                break;
            default:
                list.RemoveRange(i, list.Count - i);
                break;
        }
        return [.. list];
    }
}
