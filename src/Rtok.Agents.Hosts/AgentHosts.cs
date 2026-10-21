namespace Rtok.Agents.Hosts;

/// <summary>The host table and the lookups <c>rtok agents</c> makes in it.</summary>
public static class AgentHosts
{
    /// <summary>Every host, in <c>agents list</c> order.</summary>
    public static IReadOnlyList<AgentHost> All => AgentHostTable.All;

    /// <summary>The host with id <paramref name="id"/>; null when there is none.</summary>
    public static AgentHost? Find(string id) =>
        AgentHostTable.All.FirstOrDefault(h => string.Equals(h.Id, id, StringComparison.Ordinal));

    /// <summary><c>claude, codex</c> as <c>["claude", "codex"]</c>: split on commas, trimmed, empties dropped.</summary>
    /// <exception cref="AgentsException">Nothing is left.</exception>
    public static IReadOnlyList<string> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var hosts = text.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
        return hosts.Length > 0 ? hosts : throw new AgentsException($"unknown host: {text}");
    }

    /// <summary>The hosts named, in the order given.</summary>
    /// <exception cref="AgentsException">None is named, or one is unknown.</exception>
    public static IReadOnlyList<AgentHost> Resolve(IReadOnlyList<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0)
        {
            throw new AgentsException("no host given");
        }
        return [.. ids.Select(id => Find(id) ?? throw new AgentsException($"unknown host: {id}"))];
    }
}
