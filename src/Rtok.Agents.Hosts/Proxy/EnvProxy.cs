using Rtok.Agent.Sdk;

namespace Rtok.Agents.Hosts.Proxy;

/// <summary>
/// A base URL in a JSON file's <c>env</c> object: Claude Code's <c>ANTHROPIC_BASE_URL</c>
/// (rtok <c>proxy_env</c>) and OpenCode's <c>OPENAI_BASE_URL</c>. Install sets it, over a foreign
/// value too, which the <c>revert:</c> line names; remove takes it out only while it is rtok's.
/// </summary>
internal sealed class EnvProxy(string path, string key, string url, int port) : OwnedFile(path)
{
    public override bool Holds(string text) =>
        Value(new JsonEdit(Path, text)) is { } v && ProxyOwner.Ours(v, url, port);

    public override (string Report, string Text) Install(string text)
    {
        var json = new JsonEdit(Path, text);
        json.Object("env");
        var prev = Value(json);
        if (string.Equals(prev, url, StringComparison.Ordinal))
        {
            return (Agent.Sdk.Apply.NoChanges, text);
        }
        json.Set(["env", key], url);
        var revert = prev is null ? $"remove env.{key}" : $"set env.{key} to {prev}";
        return ($"env.{key}: {url}\nrevert: {revert}", json.Text);
    }

    public override (string Report, string Text) Remove(string text, Apply apply)
    {
        var json = new JsonEdit(Path, text);
        if (Value(json) is not { } v || !ProxyOwner.Ours(v, url, port))
        {
            return (Agent.Sdk.Apply.NoChanges, text);
        }
        json.Remove("env", key);
        json.DropEmpty("env");
        return ($"- env.{key}", json.Text);
    }

    /// <summary>The string at <c>env.&lt;key&gt;</c>; null when absent, not a string, or <c>env</c> is not an object.</summary>
    private string? Value(JsonEdit json) =>
        json.Get("env") is { } env ? JsonEdit.Str(env, key) : null;
}
