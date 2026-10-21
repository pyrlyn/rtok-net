using System.Collections;
using System.Globalization;
using Tomlyn.Model;

namespace Pyrlyn.Config.Layered;

/// <summary>
/// Merges layers in the order they are added; a later layer wins key by key. The usual order
/// is defaults, TOML files, <c>.env</c>, environment, flags.
/// </summary>
/// <remarks>
/// Environment variables map to keys through the defaults: each default leaf
/// <c>proxy.openai_upstream</c> is named <c>&lt;PREFIX&gt;PROXY_OPENAI_UPSTREAM</c>, so an
/// underscore inside a key is never taken for a separator. Variables that name no default leaf
/// are ignored. A value takes the type of its default (bool, integer, float, string); one that
/// does not parse stays a string for the caller's schema to reject. A default array takes a
/// comma-separated list.
/// </remarks>
/// <param name="envPrefix">The prefix of environment variables, for example <c>RTOK_</c>.</param>
public sealed class LayeredConfigBuilder(string envPrefix)
{
    private readonly TomlTable _root = new();
    private readonly Dictionary<string, ConfigSource> _sources = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string[] Path, object Default)> _envNames = new(
        StringComparer.Ordinal
    );
    private readonly List<string> _warnings = [];

    /// <summary>Adds built-in defaults. Their leaves also name the environment variables.</summary>
    /// <param name="values">The default tree.</param>
    /// <param name="layer">The layer name.</param>
    /// <returns>This builder.</returns>
    public LayeredConfigBuilder AddDefaults(TomlTable values, string layer = "default")
    {
        ArgumentNullException.ThrowIfNull(values);
        var source = new ConfigSource(layer, ConfigSourceKind.Defaults);
        Merge([], values, _ => source);
        IndexEnvNames([], values);
        return this;
    }

    /// <summary>Adds TOML text as a file layer.</summary>
    /// <param name="layer">The layer name, for example <c>user</c> or <c>project</c>.</param>
    /// <param name="text">The TOML text.</param>
    /// <param name="file">The file it came from, for sources and errors.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ConfigFileException">The text is not valid TOML.</exception>
    public LayeredConfigBuilder AddToml(string layer, string text, string? file = null)
    {
        var (table, lines) = TomlText.Read(text, file);
        Merge(
            [],
            table,
            path => new ConfigSource(
                layer,
                ConfigSourceKind.File,
                file,
                lines.TryGetValue(KeyPath.Join(path), out var line) ? line : 0
            )
        );
        return this;
    }

    /// <summary>Adds a TOML file. A missing file adds nothing.</summary>
    /// <param name="layer">The layer name.</param>
    /// <param name="file">The file.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ConfigFileException">The file is not valid TOML.</exception>
    public LayeredConfigBuilder AddTomlFile(string layer, string file)
    {
        if (File.Exists(file))
        {
            AddToml(layer, File.ReadAllText(file), file);
        }
        return this;
    }

    /// <summary>
    /// Adds the prefixed variables of <c>.env</c> files. The first file that defines a variable
    /// wins; missing files are skipped. A malformed file keeps the lines before the error and
    /// adds a warning.
    /// </summary>
    /// <param name="layer">The layer name.</param>
    /// <param name="files">The files, highest priority first.</param>
    /// <returns>This builder.</returns>
    public LayeredConfigBuilder AddDotEnv(string layer, IEnumerable<string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            if (!File.Exists(file))
            {
                continue;
            }
            var (vars, error) = DotEnv.Parse(File.ReadAllText(file), file);
            if (error is not null)
            {
                _warnings.Add(error.Message);
            }
            foreach (var v in vars)
            {
                if (Suffix(v.Name) is { } suffix && seen.Add(suffix))
                {
                    SetFromEnv(
                        suffix,
                        v.Value,
                        new ConfigSource(layer, ConfigSourceKind.DotEnv, v.File, v.Line, v.Name)
                    );
                }
            }
        }
        return this;
    }

    /// <summary>Adds environment variables that carry the prefix.</summary>
    /// <param name="layer">The layer name.</param>
    /// <param name="variables">Name and value pairs; null reads the process environment.</param>
    /// <returns>This builder.</returns>
    public LayeredConfigBuilder AddEnvironment(
        string layer,
        IEnumerable<KeyValuePair<string, string>>? variables = null
    )
    {
        variables ??= Environment
            .GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .Select(e => new KeyValuePair<string, string>((string)e.Key, e.Value as string ?? ""));
        foreach (var (name, value) in variables.OrderBy(v => v.Key, StringComparer.Ordinal))
        {
            if (Suffix(name) is { } suffix)
            {
                SetFromEnv(
                    suffix,
                    value,
                    new ConfigSource(layer, ConfigSourceKind.Environment, Name: name)
                );
            }
        }
        return this;
    }

    /// <summary>Lets an extra variable name set a key, for names kept from older versions.</summary>
    /// <param name="name">The full variable name, for example <c>RTOK_UPSTREAM</c>.</param>
    /// <param name="key">The dotted key it sets; it must have a default.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException">The key has no default.</exception>
    public LayeredConfigBuilder AddEnvAlias(string name, string key)
    {
        var target = KeyPath.Join(KeyPath.Split(key));
        var entry = _envNames.Values.FirstOrDefault(e =>
            string.Equals(KeyPath.Join(e.Path), target, StringComparison.Ordinal)
        );
        if (entry.Path is null)
        {
            throw new ArgumentException($"no default for {key}", nameof(key));
        }
        _envNames[
            Suffix(name)
                ?? throw new ArgumentException($"{name} lacks the prefix {envPrefix}", nameof(name))
        ] = entry;
        return this;
    }

    /// <summary>Adds one command-line flag's value.</summary>
    /// <param name="layer">The layer name, for example <c>flag</c>.</param>
    /// <param name="key">The dotted key.</param>
    /// <param name="value">The value.</param>
    /// <param name="flag">The flag as typed, for example <c>--port</c>.</param>
    /// <returns>This builder.</returns>
    public LayeredConfigBuilder AddFlag(string layer, string key, object value, string flag) =>
        AddEntry(
            new ConfigEntry(key, value, new ConfigSource(layer, ConfigSourceKind.Flag, Name: flag))
        );

    /// <summary>Adds one value with a source the caller chose, for layers this builder has no method for.</summary>
    /// <param name="entry">The key, value and source.</param>
    /// <returns>This builder.</returns>
    public LayeredConfigBuilder AddEntry(ConfigEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Merge(KeyPath.Split(entry.Key), entry.Value, _ => entry.Source);
        return this;
    }

    /// <summary>The merged configuration so far.</summary>
    /// <returns>A snapshot; later additions do not change it.</returns>
    public LayeredConfig Build()
    {
        var entries = new List<ConfigEntry>(_sources.Count);
        Collect(_root, [], entries);
        entries.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        return new LayeredConfig(Copy(_root), entries, [.. _warnings]);
    }

    private string? Suffix(string name) =>
        name.StartsWith(envPrefix, StringComparison.OrdinalIgnoreCase)
            ? name[envPrefix.Length..].ToUpperInvariant()
            : null;

    private void SetFromEnv(string suffix, string raw, ConfigSource source)
    {
        if (_envNames.TryGetValue(suffix, out var target))
        {
            Merge(target.Path, Convert(raw, target.Default), _ => source);
        }
    }

    private static object Convert(string raw, object defaultValue)
    {
        var inv = CultureInfo.InvariantCulture;
        return defaultValue switch
        {
            string => raw,
            bool when bool.TryParse(raw.Trim(), out var b) => b,
            int or long when long.TryParse(raw.Trim(), NumberStyles.Integer, inv, out var l) => l,
            double or float when double.TryParse(raw.Trim(), NumberStyles.Float, inv, out var d) =>
                d,
            // `VAR=` is an empty list, not [""].
            IEnumerable => ToArray(
                raw.Split(
                    ',',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                )
            ),
            _ => raw,
        };
    }

    private static TomlArray ToArray(string[] items)
    {
        var array = new TomlArray(items.Length);
        foreach (var item in items)
        {
            array.Add(item);
        }
        return array;
    }

    private void IndexEnvNames(string[] prefix, TomlTable table)
    {
        foreach (var (key, value) in table)
        {
            string[] path = [.. prefix, key];
            if (value is TomlTable child)
            {
                IndexEnvNames(path, child);
            }
            else
            {
                _envNames[string.Join('_', path).ToUpperInvariant()] = (path, value);
            }
        }
    }

    /// <summary>Sets <paramref name="value"/> at <paramref name="path"/>: tables merge key by key, anything else replaces.</summary>
    private void Merge(string[] path, object value, Func<string[], ConfigSource> sourceOf)
    {
        var table = _root;
        for (var i = 0; i < path.Length - 1; i++)
        {
            table = Table(table, path[..(i + 1)]);
        }
        if (value is TomlTable source)
        {
            if (path.Length > 0)
            {
                Table(table, path);
            }
            foreach (var (key, child) in source)
            {
                Merge([.. path, key], child, sourceOf);
            }
            return;
        }
        Forget(path);
        table[path[^1]] = value;
        _sources[KeyPath.Join(path)] = sourceOf(path);
    }

    /// <summary>The table at <paramref name="path"/> (the last segment under <paramref name="parent"/>), replacing a leaf there.</summary>
    private TomlTable Table(TomlTable parent, string[] path)
    {
        if (parent.TryGetValue(path[^1], out var node) && node is TomlTable table)
        {
            return table;
        }
        Forget(path);
        table = new TomlTable();
        parent[path[^1]] = table;
        return table;
    }

    /// <summary>Drops the sources of a key and everything under it.</summary>
    private void Forget(string[] path)
    {
        var key = KeyPath.Join(path);
        var under = key + ".";
        foreach (
            var k in _sources
                .Keys.Where(k =>
                    string.Equals(k, key, StringComparison.Ordinal)
                    || k.StartsWith(under, StringComparison.Ordinal)
                )
                .ToList()
        )
        {
            _sources.Remove(k);
        }
    }

    private void Collect(TomlTable table, string[] prefix, List<ConfigEntry> entries)
    {
        foreach (var (key, value) in table)
        {
            string[] path = [.. prefix, key];
            if (value is TomlTable child)
            {
                Collect(child, path, entries);
            }
            else if (_sources.TryGetValue(KeyPath.Join(path), out var source))
            {
                entries.Add(new ConfigEntry(KeyPath.Join(path), value, source));
            }
        }
    }

    private static TomlTable Copy(TomlTable table)
    {
        var copy = new TomlTable();
        foreach (var (key, value) in table)
        {
            copy[key] = value is TomlTable child ? Copy(child) : value;
        }
        return copy;
    }
}
