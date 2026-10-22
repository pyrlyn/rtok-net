using Rtok.Agent.Sdk;
using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts.Hooks;

/// <summary>
/// The file a host keeps rtok's hooks in, edited in the host's own shape. Install and update
/// write the same hooks (rtok's <c>Mode::Update</c> differs only in the plugin step), rewriting a
/// stale one and dropping one on a pair no longer listed; remove takes back only rtok's own.
/// Every edit keeps the rest of the file byte for byte.
/// </summary>
/// <param name="path">The file.</param>
internal abstract class HookFile(string path)
{
    /// <summary>The file.</summary>
    public string Path => path;

    /// <summary>Whether <paramref name="text"/> holds an rtok hook.</summary>
    /// <exception cref="InvalidDataException">The text does not parse.</exception>
    public abstract bool Holds(string text);

    /// <summary>The report of putting rtok's hooks in <paramref name="text"/>, and the edited text.</summary>
    /// <exception cref="InvalidDataException">The text does not parse, or is not shaped as the host writes it.</exception>
    public abstract (string Report, string Text) Install(string text);

    /// <summary>The report of taking rtok's hooks out of <paramref name="text"/>, and the edited text.</summary>
    /// <exception cref="InvalidDataException">The text does not parse, or is not shaped as the host writes it.</exception>
    public abstract (string Report, string Text) Remove(string text, Apply apply);

    /// <summary>Installs or removes the hooks; the report, or <see cref="Apply.NoChanges"/>.</summary>
    /// <exception cref="IOException">The file exists but cannot be read.</exception>
    /// <exception cref="InvalidDataException">The file does not parse, or is not shaped as the host writes it.</exception>
    public virtual string Apply(bool remove, Apply apply, McpDisk disk)
    {
        var text = disk.Read(path);
        if (remove && text is null)
        {
            return Agent.Sdk.Apply.NoChanges;
        }
        var (report, edited) = remove ? Remove(text ?? "", apply) : Install(text ?? "");
        if (!string.Equals(edited, text ?? "", StringComparison.Ordinal))
        {
            disk.Write(apply, path, edited, report);
        }
        return report;
    }

    /// <summary>Whether the file holds an rtok hook; an unreadable file holds none.</summary>
    public virtual bool Installed(McpDisk disk)
    {
        try
        {
            return disk.Read(path) is { } text && Holds(text);
        }
        catch (Exception e) when (e is IOException or InvalidDataException)
        {
            return false;
        }
    }
}
