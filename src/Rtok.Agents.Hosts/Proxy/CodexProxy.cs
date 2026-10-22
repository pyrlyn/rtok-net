using Pyrlyn.Config.Layered;
using Rtok.Agent.Sdk;
using Tomlyn.Model;
using Tomlyn.Syntax;

namespace Rtok.Agents.Hosts.Proxy;

/// <summary>
/// Codex's <c>config.toml</c>: <c>model_provider = "rtok"</c> and a
/// <c>[model_providers.rtok]</c> table with rtok's <c>base_url</c> (rtok <c>codex::insert_proxy</c>).
/// The provider it replaces is kept in a <c># rtok: was "&lt;name&gt;"</c> comment on that line and
/// put back on remove; a table the user edited asks first.
/// </summary>
internal sealed class CodexProxy(string path, string url) : OwnedFile(path)
{
    private const string Name = "rtok";
    private const string Was = "# rtok: was ";
    private const string Header = "[model_providers.rtok]";

    public override bool Holds(string text) =>
        new TomlEdit(Path, text).Model.TryGetValue("model_providers", out var mp)
        && mp is TomlTable providers
        && providers.TryGetValue(Name, out var t)
        && t is TomlTable table
        && table.TryGetValue("name", out var n)
        && n is Name;

    public override (string Report, string Text) Install(string text)
    {
        var toml = new TomlEdit(Path, text);
        var table = Table(toml);
        var kv = toml.Root("model_provider");
        var old = (kv?.Value as StringValueSyntax)?.Value;
        if (string.Equals(old, Name, StringComparison.Ordinal) && Ours(table))
        {
            return (Agent.Sdk.Apply.NoChanges, text);
        }
        var was = string.Equals(old, Name, StringComparison.Ordinal) ? Replaced(toml, kv!) : old;
        var line = TomlText.Format(Name) + (was is null ? "" : $" {Was}{TomlText.Format(was)}");
        if (kv is null)
        {
            toml.InsertRoot($"model_provider = {line}");
        }
        else
        {
            toml.SetLine(kv, line);
        }
        (string, object)[] values = [("name", Name), ("base_url", url)];
        if (table is null)
        {
            toml.Append(Header, values);
        }
        else
        {
            toml.Replace(table, Header, values);
        }
        var revert = was is null ? $"remove {Header}" : $"set model_provider to {was}";
        return ($"+ {Header}\nbase_url = {TomlText.Format(url)}\nrevert: {revert}", toml.Edited());
    }

    public override (string Report, string Text) Remove(string text, Apply apply)
    {
        var toml = new TomlEdit(Path, text);
        var table = Table(toml);
        if (
            table is not null
            && !Ours(table)
            && Ownership.KeepEdited(apply, $"model_providers.rtok in {Path}") is { } keep
        )
        {
            return (keep, text);
        }
        var kv = toml.Root("model_provider");
        var key = string.Equals(
            (kv?.Value as StringValueSyntax)?.Value,
            Name,
            StringComparison.Ordinal
        );
        var restored = key ? Replaced(toml, kv!) : null;
        if (restored is not null)
        {
            toml.SetLine(kv!, TomlText.Format(restored));
        }
        else if (key)
        {
            toml.DropLine(kv!);
        }
        if (table is not null)
        {
            toml.Drop(table);
        }
        if (!key && table is null)
        {
            return (Agent.Sdk.Apply.NoChanges, text);
        }
        var report = restored is null
            ? $"- {Header}"
            : $"- {Header}\nrestore model_provider = {TomlText.Format(restored)}";
        return (report, toml.Edited());
    }

    /// <summary>The <c>[model_providers.rtok]</c> table; null when absent.</summary>
    /// <exception cref="InvalidDataException">It, or <c>model_providers</c>, is spelled some other way.</exception>
    private static TomlEdit.Table? Table(TomlEdit toml)
    {
        if (!toml.Model.TryGetValue("model_providers", out var mp))
        {
            return null;
        }
        if (mp is not TomlTable providers)
        {
            throw toml.Shape("model_providers is not a table");
        }
        var tables = toml.Tables(false, "model_providers", Name);
        if (providers.ContainsKey(Name) && tables.Count == 0)
        {
            throw toml.Shape($"model_providers.rtok is not a {Header} table");
        }
        return tables.Count == 0 ? null : tables[0];
    }

    /// <summary>The table exactly as rtok writes it.</summary>
    private bool Ours(TomlEdit.Table? t) =>
        t is not null
        && t.Keys.Count == 2
        && string.Equals(t.Str("name"), Name, StringComparison.Ordinal)
        && string.Equals(t.Str("base_url"), url, StringComparison.Ordinal);

    /// <summary>The provider rtok replaced, read back from the <c># rtok: was</c> comment; null when there is none.</summary>
    private static string? Replaced(TomlEdit toml, KeyValueSyntax kv)
    {
        var tail = toml.Tail(kv);
        if (!tail.StartsWith(Was, StringComparison.Ordinal))
        {
            return null;
        }
        try
        {
            return TomlText.Parse($"v = {tail[Was.Length..]}", "comment")["v"] as string;
        }
        catch (ConfigFileException)
        {
            return null;
        }
    }
}
