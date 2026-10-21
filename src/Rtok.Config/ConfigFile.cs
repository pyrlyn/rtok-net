using System.Reflection;
using Pyrlyn.Config.Layered;
using Tomlyn.Model;

namespace Rtok.Config;

/// <summary>Writes the user config file: <c>rtok config init</c> and <c>rtok config set</c>.</summary>
public static class ConfigFile
{
    /// <summary>The reference file <c>init</c> writes: every key at its default, commented out, with its doc.</summary>
    public static string Reference { get; } = ReadReference();

    /// <summary>Writes the reference file to the user config file.</summary>
    /// <param name="file">The user config file.</param>
    /// <param name="force">Overwrite an existing file.</param>
    /// <exception cref="ConfigException">The file exists and <paramref name="force"/> is false.</exception>
    public static void Init(string file, bool force) => PlanInit(file, force).Apply();

    /// <summary>The write <c>init</c> makes: the reference over the user file.</summary>
    /// <param name="file">The user config file.</param>
    /// <param name="force">Overwrite an existing file.</param>
    /// <returns>The planned edit.</returns>
    /// <exception cref="ConfigException">The file exists and <paramref name="force"/> is false.</exception>
    public static ConfigEdit PlanInit(string file, bool force)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (File.Exists(file) && !force)
        {
            throw new ConfigException($"{file} exists; pass --force to overwrite");
        }
        return new ConfigEdit(file, File.Exists(file) ? File.ReadAllText(file) : "", Reference);
    }

    /// <summary>
    /// The user file's text with <paramref name="key"/> set. Comments and every other line stay
    /// byte for byte; a commented-out <c># key = v</c> is taken over in place.
    /// </summary>
    /// <param name="text">The file's text.</param>
    /// <param name="file">The file, for messages.</param>
    /// <param name="key">The dotted key.</param>
    /// <param name="raw">The value as typed: TOML (<c>8080</c>, <c>true</c>, <c>["a"]</c>), else a string.</param>
    /// <param name="options">The environment the result is checked in.</param>
    /// <returns>The edited text.</returns>
    /// <exception cref="ConfigException">The key is unknown or the result does not validate.</exception>
    public static string Edit(
        string text,
        string file,
        string key,
        string raw,
        ConfigLoadOptions? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(raw);
        var path = SplitKey(key);
        var dotted = KeyPath.Join(path);
        var expected =
            ConfigSchema.Lookup(path, out _) ?? throw new ConfigException($"unknown key: {key}");
        if (expected is TomlTable)
        {
            throw new ConfigException($"{dotted} is a table; set one of its keys");
        }
        var value = expected is string ? raw : TomlText.ParseValueOrString(raw);
        string edited;
        try
        {
            edited = TomlEditor.Set(text, dotted, value, file);
        }
        catch (ConfigFileException e)
        {
            throw new ConfigException(e.Message, e);
        }
        var check = ConfigLoader.LoadFile(
            file,
            (options ?? new ConfigLoadOptions()) with
            {
                UserText = edited,
            }
        );
        if (check.Issues.Count > 0)
        {
            throw new ConfigException(string.Join('\n', check.Issues));
        }
        return edited;
    }

    /// <summary>Sets a key in the user file, creating the file from the reference first when it is missing.</summary>
    /// <param name="file">The user config file.</param>
    /// <param name="key">The dotted key.</param>
    /// <param name="raw">The value as typed.</param>
    /// <param name="options">The environment the result is checked in.</param>
    /// <exception cref="ConfigException">The key is unknown or the result does not validate.</exception>
    public static void Set(
        string file,
        string key,
        string raw,
        ConfigLoadOptions? options = null
    ) => PlanSet(file, key, raw, options).Apply();

    /// <summary>
    /// The write <c>set</c> makes, checked as a real run is; a missing file starts from the
    /// reference.
    /// </summary>
    /// <param name="file">The user config file.</param>
    /// <param name="key">The dotted key.</param>
    /// <param name="raw">The value as typed.</param>
    /// <param name="options">The environment the result is checked in.</param>
    /// <returns>The planned edit.</returns>
    /// <exception cref="ConfigException">The key is unknown or the result does not validate.</exception>
    public static ConfigEdit PlanSet(
        string file,
        string key,
        string raw,
        ConfigLoadOptions? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(file);
        var text = File.Exists(file) ? File.ReadAllText(file) : null;
        return new ConfigEdit(file, text ?? "", Edit(text ?? Reference, file, key, raw, options));
    }

    private static string[] SplitKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var trimmed = key.Trim();
        if (
            trimmed.Length == 0
            || trimmed.StartsWith('.')
            || trimmed.EndsWith('.')
            || trimmed.Contains("..", StringComparison.Ordinal)
        )
        {
            throw new ConfigException("empty key");
        }
        try
        {
            return KeyPath.Split(key);
        }
        catch (ArgumentException e)
        {
            throw new ConfigException($"unknown key: {key}", e);
        }
    }

    private static string ReadReference()
    {
        using var stream =
            typeof(ConfigFile).Assembly.GetManifestResourceStream("Rtok.Config.default.toml")
            ?? throw new InvalidOperationException("the reference config is not embedded");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
