using System.Text;
using Rtok.Archive;
using Rtok.Config;
using Rtok.Store;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// <c>rtok expand</c> over any bytes, split at the first two NULs into a line range, an id and
/// the body to archive. Parsing never throws, a range that parses lies inside the body, the 25
/// spellings agents logged (rtok T522) parse, any id is answered rather than thrown, and the
/// archived body expands back byte for byte, by its full id and by its 8-char prefix.
/// </summary>
internal static class ExpandRange
{
    private static readonly HashSet<string> Logged = new(StringComparer.Ordinal)
    {
        "320 - 360",
        "36<br/>-60",
        "10:45",
        "560 650",
        "[1, 50]",
        "[200, 260]",
        "[242, 300]",
        "[317, 390]",
        "12 - 100",
        "200 - 380",
        "580:650",
        "1 40",
        "1860_1960",
        "283 350",
        "136\"-\"215",
        "12 - 70",
        "215\"-\"460",
        "235\"-\"270",
        "410<br>",
        "420 - 640",
        "490<br>-800",
        "52<br>",
        "60<br>",
        "610 614",
        "95<br>",
    };

    // One store for the process: opening one per input would spend each run on migrations.
    // The tests run samples on several threads at once, so its use is serialized.
    private static readonly Lock Gate = new();
    private static readonly Lazy<RtokStore> Shared = new(Open);
    private static readonly RtokConfig Config = new();

    public static void Run(ReadOnlySpan<byte> data)
    {
        var first = data.IndexOf((byte)0);
        var spec = FuzzInput.Text(first < 0 ? data : data[..first]);
        var rest = first < 0 ? [] : data[(first + 1)..];
        var second = rest.IndexOf((byte)0);
        var idText = FuzzInput.Text(second < 0 ? rest : rest[..second]);
        var body = (second < 0 ? [] : rest[(second + 1)..]).ToArray();
        var lines = ArchiveLines.Split(Encoding.UTF8.GetString(body));

        var any = LineRange.TryParse(spec, long.MaxValue, out _, out _);
        FuzzInput.Check(!Logged.Contains(spec) || any, $"logged spelling `{spec}` failed");
        var parsed = LineRange.TryParse(spec, lines.Count, out var range, out _);
        FuzzInput.Check(!parsed || any, "parses for the body but not for any length");
        FuzzInput.Check(
            !parsed || (range.Start >= 1 && range.Start <= range.End && range.End <= lines.Count),
            $"`{spec}` gave {range} for {lines.Count} lines"
        );

        lock (Gate)
        {
            var store = Shared.Value;
            var expander = new Expander(store, Config, Expander.CliSession);
            var id = store.PutArchive("fuzz", body);
            _ = expander.Run(new ExpandRequest(idText, spec));
            Same(expander.Run(new ExpandRequest(id)), body, "full id");
            var shortId = expander.Run(new ExpandRequest(id[..8]));
            // Two bodies of one run sharing 8 hex digits are ambiguous, not wrong.
            if (shortId.Error?.StartsWith("archive id prefix", StringComparison.Ordinal) != true)
            {
                Same(shortId, body, "8-char prefix");
            }
            if (parsed)
            {
                var view = expander.Run(new ExpandRequest(id, spec));
                FuzzInput.Check(view.Ok, $"`{spec}` parsed but expand refused it: {view.Error}");
                var count = range.End - range.Start + 1;
                var newlines = view.Body.AsSpan().Count((byte)'\n');
                FuzzInput.Check(
                    newlines == count || (count == 1 && view.Body is []),
                    $"`{spec}` printed {newlines} lines for {count}"
                );
            }
        }
    }

    private static void Same(ExpandResult result, byte[] body, string how) =>
        FuzzInput.Check(
            result.Ok && result.Body.AsSpan().SequenceEqual(body),
            $"{how}: the body did not come back byte for byte ({result.Error})"
        );

    private static RtokStore Open()
    {
        var dir = Directory.CreateTempSubdirectory("rtok-fuzz-expand-");
        var store = RtokStore.Open(
            new StoreOptions(Path.Combine(dir.FullName, "rtok.db"))
            {
                ArchiveDir = Path.Combine(dir.FullName, "archive"),
            }
        );
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            store.Dispose();
            dir.Delete(recursive: true);
        };
        return store;
    }
}
