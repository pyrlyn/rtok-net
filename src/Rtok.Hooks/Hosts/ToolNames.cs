namespace Rtok.Hooks.Hosts;

/// <summary>Host tool names to the Claude names the plugins match on (rtok
/// <c>canonical_tool_name</c>).</summary>
public static class ToolNames
{
    /// <summary><c>Bash</c>, <c>Read</c>, <c>Edit</c> or <c>Write</c> for a host's shell, read,
    /// edit or write tool; any other name unchanged.</summary>
    public static string Canonical(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var l = name.ToLowerInvariant();
        if (
            l is "exec" or "run_commands"
            || l.Contains("bash", StringComparison.Ordinal)
            || l.Contains("shell", StringComparison.Ordinal)
            || l.Contains("terminal", StringComparison.Ordinal)
            || l.Contains("powershell", StringComparison.Ordinal)
        )
        {
            return "Bash";
        }
        if (
            l.StartsWith("read", StringComparison.Ordinal)
            || l.StartsWith("view", StringComparison.Ordinal)
        )
        {
            return "Read";
        }
        return l switch
        {
            "edit" or "replace" or "edit_file" => "Edit",
            "write" or "write_file" => "Write",
            _ => name,
        };
    }
}
