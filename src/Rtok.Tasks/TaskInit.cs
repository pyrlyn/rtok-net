using Pyrlyn.Config.Layered;
using Pyrlyn.FsKit;
using Rtok.Config;
using Tomlyn.Model;

namespace Rtok.Tasks;

/// <summary><c>rtok task init</c>: writes <c>[tasks]</c> into the checkout's <c>.rtok.toml</c>, keeping every other byte.</summary>
public static class TaskInit
{
    /// <summary>
    /// Sets <c>tasks.adapter</c> (to <c>disk</c> when not given and not set yet) and
    /// <c>tasks.prefix</c> (upper-cased) in the checkout's project file.
    /// </summary>
    /// <param name="cwd">A directory inside the checkout.</param>
    /// <param name="adapter"><c>disk</c>, <c>github</c> or <c>gitlab</c>; null keeps the file's.</param>
    /// <param name="prefix">The id prefix; null keeps the file's.</param>
    /// <returns>The project file.</returns>
    /// <exception cref="TaskException">A flag is bad, there is no checkout, or the file does not parse.</exception>
    public static string Write(string cwd, string? adapter, string? prefix)
    {
        if (
            adapter is not null
            && !ConfigValidator.TaskAdapters.Contains(adapter, StringComparer.Ordinal)
        )
        {
            throw new TaskException(
                $"rtok task init: --adapter must be one of {string.Join(", ", ConfigValidator.TaskAdapters)}"
            );
        }
        var upper = prefix is null ? null : TaskId.CheckPrefix(prefix);
        var root =
            ProjectRoot.Find(cwd)
            ?? throw new TaskException("rtok task init: not inside a git checkout");
        var file = Path.Combine(root.Checkout, ConfigLoader.ProjectFileName);
        var text = File.Exists(file) ? File.ReadAllText(file) : "";
        try
        {
            if (adapter is not null || !HasAdapter(text, file))
            {
                text = ConfigFile.Edit(text, file, "tasks.adapter", adapter ?? "disk");
            }
            if (upper is not null)
            {
                text = ConfigFile.Edit(text, file, "tasks.prefix", upper);
            }
        }
        catch (ConfigException e)
        {
            throw new TaskException($"rtok task init: {e.Message}", e);
        }
        AtomicFile.Write(file, text);
        return file;
    }

    private static bool HasAdapter(string text, string file)
    {
        TomlTable table;
        try
        {
            table = TomlText.Parse(text, file);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw new TaskException($"rtok task init: {e.Message}", e);
        }
        return table.TryGetValue("tasks", out var tasks) switch
        {
            false => false,
            true when tasks is TomlTable t => t.ContainsKey("adapter"),
            _ => throw new TaskException($"rtok task init: {file}: tasks is not a table"),
        };
    }
}
