namespace Rtok.Doctor;

/// <summary>Numbers duplicate groups from 0 across hooks and MCP servers.</summary>
internal sealed class GroupCounter
{
    private int _next;

    public int Next() => _next++;
}
