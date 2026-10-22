using System.Text.Json;
using Pyrlyn.FsKit;
using Rtok.Agents.Usage;

namespace Rtok.Measure;

/// <summary>The small readings of a tool call every fold shares.</summary>
internal static class Calls
{
    /// <summary>The call's <c>file_path</c> (or <c>path</c>), trimmed; null when blank or absent.</summary>
    public static string? ToolPath(JsonElement input) =>
        (Json.Text(input, "file_path") ?? Json.Text(input, "path"))?.Trim() is { Length: > 0 } p
            ? p
            : null;

    /// <summary>
    /// Two spellings of one tool-call path: equal, or one is the other's trailing components
    /// (<c>src/main.rs</c> vs <c>/repo/src/main.rs</c>). Blank paths match nothing.
    /// </summary>
    public static bool SamePath(string a, string b)
    {
        (a, b) = (a.Trim(), b.Trim());
        return a.Length > 0
            && b.Length > 0
            && (FsPath.SamePath(a, b) || FsPath.EndsWith(a, b) || FsPath.EndsWith(b, a));
    }

    /// <summary><paramref name="part"/> as a percentage of <paramref name="whole"/>; 0 for an empty whole.</summary>
    public static double Pct(long part, long whole) => whole == 0 ? 0 : 100.0 * part / whole;

    /// <summary>The chars/4 estimate rtok uses wherever no tokenizer runs: ⌈bytes / 4⌉.</summary>
    public static long EstTokens(long bytes) => (bytes + 3) / 4;
}
