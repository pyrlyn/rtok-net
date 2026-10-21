using System.Text.Json.Nodes;
using Rtok.Agent.Sdk;
using Rtok.Mcp.Install;

namespace Rtok.Agents.Hosts.Mcp;

/// <summary>
/// rtok's MCP entry in a host file the shared MCP core does not model: Goose's and Continue's
/// YAML, Reasonix's <c>[[plugins]]</c>. Each edits only rtok's own lines.
/// </summary>
internal abstract class McpFile(string host, string path)
{
    /// <summary>The host id, as in <c>--host</c>.</summary>
    public string Host => host;

    /// <summary>The config file.</summary>
    public string Path => path;

    /// <summary>The entry's place as reports spell it: <c>extensions.rtok</c>.</summary>
    public abstract string At { get; }

    /// <summary>rtok's entry as the file holds it, its compared keys only; null when there is none.</summary>
    /// <exception cref="InvalidDataException">The file does not parse, or is not shaped as the host writes it.</exception>
    public abstract JsonNode? Entry(string text);

    /// <summary>The entry rtok writes, starting <paramref name="command"/>.</summary>
    public abstract JsonObject Ours(string command);

    /// <summary>The text with rtok's entry put in, or in place of the one there.</summary>
    public abstract string With(string text, string command);

    /// <summary>The text without rtok's entry.</summary>
    public abstract string Without(string text);

    /// <summary>Whether <paramref name="have"/> needs no rewrite.</summary>
    public virtual bool Current(JsonNode have, string command) =>
        JsonNode.DeepEquals(have, Ours(command));

    /// <summary>Whether <paramref name="have"/> counts as rtok's MCP module being installed.</summary>
    public virtual bool Serves(JsonNode have, Func<string, bool> isBin) =>
        Ownership.RunsBin(Unnamed(have), isBin);

    /// <summary><c>rtok mcp --host goose</c>.</summary>
    public string Summary(string command) => $"{command} mcp --host {host}";

    /// <summary>The arguments every entry passes: <c>mcp --host &lt;host&gt;</c>.</summary>
    /// <remarks>The <c>params</c> constructor, not a collection expression: that calls the generic <c>Add&lt;T&gt;</c>, which AOT rejects.</remarks>
    protected JsonArray Args() =>
        new(JsonValue.Create("mcp"), JsonValue.Create("--host"), JsonValue.Create(host));

    /// <summary>Installs, updates or removes rtok's entry, as <see cref="McpInstaller.Apply"/> does for the core's files.</summary>
    /// <exception cref="IOException">The file exists but cannot be read.</exception>
    /// <exception cref="InvalidDataException">The file does not parse, or rtok's entry cannot be put in or taken out of it.</exception>
    public string Apply(
        McpMode mode,
        Apply apply,
        McpDisk disk,
        Func<string, bool> isBin,
        string command
    )
    {
        var text = disk.Read(path) ?? "";
        var have = Entry(text);
        if (mode != McpMode.Remove)
        {
            if (have is not null && Current(have, command))
            {
                return Agent.Sdk.Apply.NoChanges;
            }
            var wrote = $"{At}: {Summary(command)}";
            disk.Write(apply, path, With(text, command), wrote);
            return wrote;
        }
        if (have is null)
        {
            return Agent.Sdk.Apply.NoChanges;
        }
        if (
            Ownership.Judge(
                apply,
                $"{At} in {path}",
                Unnamed(have),
                Unnamed(Ours("rtok")),
                isBin
            ) is
            { } leave
        )
        {
            return leave;
        }
        var removed = $"- {At}";
        disk.Write(apply, path, Without(text), removed);
        return removed;
    }

    /// <summary>Whether the file holds an entry that serves rtok; an unreadable file holds none.</summary>
    public bool Installed(McpDisk disk, Func<string, bool> isBin)
    {
        try
        {
            return Entry(disk.Read(path) ?? "") is { } have && Serves(have, isBin);
        }
        catch (Exception e) when (e is IOException or InvalidDataException)
        {
            return false;
        }
    }

    /// <summary>Only <paramref name="keys"/> of <paramref name="node"/>, in that order: the keys a host re-saves around are not an edit.</summary>
    protected static JsonNode? Core(JsonNode? node, IReadOnlyList<string> keys)
    {
        if (node is not JsonObject o)
        {
            return node?.DeepClone();
        }
        var core = new JsonObject();
        foreach (var key in keys)
        {
            if (o.TryGetPropertyValue(key, out var v))
            {
                core[key] = v?.DeepClone();
            }
        }
        return core;
    }

    /// <summary>
    /// <paramref name="node"/> without its <c>name</c>: every entry found under that name says
    /// <c>rtok</c> there, which tells nothing about what it runs.
    /// </summary>
    private static JsonNode? Unnamed(JsonNode? node)
    {
        if (node is not JsonObject o)
        {
            return node;
        }
        var rest = (JsonObject)o.DeepClone();
        rest.Remove("name");
        return rest;
    }
}
