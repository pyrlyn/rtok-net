using Rtok.Tasks;
using Rtok.Tasks.Disk;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// Any text as a task id or a task file, the first byte picking which: it parses or is refused
/// with a <see cref="TaskException"/>. A parsed id prints back to itself; a parsed file renders
/// to a file that parses to the same task and renders back byte-identical.
/// </summary>
internal static class TaskFileText
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        var (pick, text) = FuzzInput.Picked(data);
        try
        {
            if (pick % 2 == 0)
            {
                Id(text);
            }
            else
            {
                File(text);
            }
        }
        catch (TaskException)
        {
            // Refused: the one way a bad id or file may fail.
        }
    }

    private static void Id(string text)
    {
        var id = TaskId.Parse(text);
        FuzzInput.Check(TaskId.Parse(id.ToString()) == id, $"{id} does not parse back to itself");
    }

    private static void File(string text)
    {
        var rendered = TaskFile.Render(TaskFile.Parse(text));
        var again = TaskFile.Render(TaskFile.Parse(rendered));
        FuzzInput.Check(
            string.Equals(rendered, again, StringComparison.Ordinal),
            $"a rendered file renders back different:\n{rendered}\n---- became ----\n{again}"
        );
    }
}
