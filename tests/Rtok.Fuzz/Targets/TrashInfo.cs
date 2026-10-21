using Pyrlyn.Trash;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// Any path through the <c>.trashinfo</c> <c>Path=</c> encoding: the value is printable ASCII
/// with no space, so it stays one line of the key file, and decoding each segment gives the
/// path back.
/// </summary>
internal static class TrashInfo
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        var path = FuzzInput.Text(data);
        // Escape is plain string code; only the rest of the class needs Linux.
#pragma warning disable CA1416
        var escaped = FreeDesktopTrash.Escape(path);
#pragma warning restore CA1416
        FuzzInput.Check(
            escaped.All(c => c is > ' ' and < '\x7f'),
            $"{escaped} is not one printable line"
        );
        FuzzInput.Check(
            string.Equals(
                string.Join('/', escaped.Split('/').Select(Uri.UnescapeDataString)),
                path,
                StringComparison.Ordinal
            ),
            $"{escaped} does not decode to the path"
        );
    }
}
