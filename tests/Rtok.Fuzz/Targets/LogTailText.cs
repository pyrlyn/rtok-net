using Rtok.Log;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// <c>rtok logs</c> and <c>logs watch</c> over any bytes: the first byte picks what happens to the
/// live file between two reads (an append, a rotation, a truncation in place), the rest splits at
/// the first NUL into the file before and the bytes after. Every read returns whole lines only,
/// one per newline, and nothing throws: the file can be non-UTF-8, half-written or cut short.
/// </summary>
internal static class LogTailText
{
    // A directory per run: the tests run samples on several threads at once.
    public static void Run(ReadOnlySpan<byte> data)
    {
        var dir = Directory.CreateTempSubdirectory("rtok-fuzz-log-");
        try
        {
            Run(data, Path.Combine(dir.FullName, "rtok.log"));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    private static void Run(ReadOnlySpan<byte> data, string logPath)
    {
        var pick = data.IsEmpty ? 0 : data[0] % 3;
        var rest = data.IsEmpty ? [] : data[1..];
        var nul = rest.IndexOf((byte)0);
        var before = (nul < 0 ? rest : rest[..nul]).ToArray();
        var after = (nul < 0 ? [] : rest[(nul + 1)..]).ToArray();
        File.WriteAllBytes(logPath, before);

        var tail = LogTail.Read(logPath, int.MaxValue);
        Whole(tail, Newlines(before), "tail");
        _ = LogScreen.Numbered(tail, colour: true);
        var (follow, live) = LogFollow.Primed(logPath);
        using (follow)
        {
            Whole(live, Newlines(before), "primed");
            switch (pick)
            {
                case 0:
                    using (var stream = new FileStream(logPath, FileMode.Append))
                    {
                        stream.Write(after);
                    }
                    Whole(
                        follow.Poll(logPath),
                        Newlines([.. before, .. after]) - live.Count,
                        "append"
                    );
                    break;
                case 1:
                    File.Move(logPath, logPath + ".1");
                    File.WriteAllBytes(logPath, after);
                    Whole(follow.Poll(logPath), Newlines(after), "rotation");
                    break;
                default:
                    File.WriteAllBytes(logPath, after);
                    // Fewer lines than were read means the file started again from its top.
                    var lines = Newlines(after);
                    Whole(
                        follow.Poll(logPath),
                        lines >= live.Count ? lines - live.Count : lines,
                        "truncation"
                    );
                    break;
            }
        }
    }

    private static void Whole(IReadOnlyList<string> lines, int count, string what)
    {
        FuzzInput.Check(lines.Count == count, $"{what}: {lines.Count} lines for {count} newlines");
        FuzzInput.Check(
            lines.All(l => !l.Contains('\n', StringComparison.Ordinal)),
            $"{what}: a line holds a newline"
        );
    }

    private static int Newlines(ReadOnlySpan<byte> bytes) => bytes.Count((byte)'\n');
}
