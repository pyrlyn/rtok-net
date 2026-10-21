using System.Text;
using System.Text.Json;
using Pyrlyn.ChangePreview;
using Pyrlyn.Config.Layered;
using Tomlyn.Model;

namespace Rtok.Config;

/// <summary>
/// <c>rtok config init | path | show | get | set | validate</c>. Each returns the exit code:
/// 0 on success, 1 when the config or the request is wrong.
/// </summary>
/// <param name="stdout">Where results go.</param>
/// <param name="stderr">Where errors and notes go.</param>
/// <param name="options">What a load reads; <see cref="ConfigLoadOptions.ConfigFile"/> is <c>--config</c>.</param>
/// <param name="color">Colour <c>--dry-run</c> diffs; the caller applies its rule for stdout.</param>
public sealed class ConfigCli(
    TextWriter stdout,
    TextWriter stderr,
    ConfigLoadOptions options,
    bool color = false
)
{
    private string UserFile => ConfigLoader.UserFile(options);

    /// <summary>Writes the reference file to the user config file and prints its path.</summary>
    /// <param name="force">Overwrite an existing file.</param>
    /// <param name="dryRun">Print the diff instead of writing.</param>
    /// <returns>The exit code.</returns>
    public int Init(bool force, bool dryRun = false) =>
        Run(() =>
        {
            var file = UserFile;
            if (dryRun)
            {
                Preview(file, ConfigFile.InitText(file, force));
                return;
            }
            ConfigFile.Init(file, force);
            stdout.WriteLine(file);
        });

    /// <summary>Prints the user config file's path.</summary>
    /// <returns>The exit code.</returns>
    public int Path() => Run(() => stdout.WriteLine(UserFile));

    /// <summary>Prints every effective key, with where it came from when asked.</summary>
    /// <param name="sources">Add each value's source.</param>
    /// <param name="json">Print a JSON array of <c>{ key, value, source }</c>.</param>
    /// <returns>The exit code.</returns>
    public int Show(bool sources, bool json) =>
        Run(() =>
        {
            var config = Load();
            Warn(config);
            if (json)
            {
                stdout.WriteLine(Json(w => ShowJson(w, config)));
                return;
            }
            foreach (var e in config.Entries)
            {
                var line = $"{e.Key} = {ConfigDisplay.Show(e.Key, e.Value)}";
                stdout.WriteLine(sources ? $"{line} ({ConfigDisplay.Source(e.Source)})" : line);
            }
        });

    /// <summary>Prints the effective value of one key, or of a whole table.</summary>
    /// <param name="key">The dotted key.</param>
    /// <param name="json">Print the value as JSON.</param>
    /// <returns>The exit code.</returns>
    public int Get(string key, bool json) =>
        Run(() =>
        {
            var config = Load();
            var value = Lookup(config, key) ?? throw new ConfigException($"unknown key: {key}");
            stdout.WriteLine(
                json ? Json(w => Value(w, key, value)) : ConfigDisplay.Show(key, value)
            );
        });

    /// <summary>
    /// Sets a key in the user file, comments and layout kept, after checking the result. Creates
    /// the file from the reference first when it is missing. Prints the new effective value.
    /// </summary>
    /// <param name="key">The dotted key.</param>
    /// <param name="value">The value as typed.</param>
    /// <param name="dryRun">Check the result and print the diff instead of writing.</param>
    /// <returns>The exit code.</returns>
    public int Set(string key, string value, bool dryRun = false) =>
        Run(() =>
        {
            if (dryRun)
            {
                Preview(UserFile, ConfigFile.SetText(UserFile, key, value, options));
                return;
            }
            ConfigFile.Set(UserFile, key, value, options);
            var config = Load();
            var dotted = KeyPath.Join(KeyPath.Split(key));
            if (config.TryGet(dotted, out var entry))
            {
                stdout.WriteLine($"{dotted} = {ConfigDisplay.Show(dotted, entry.Value)}");
                if (!string.Equals(entry.Source.File, UserFile, StringComparison.Ordinal))
                {
                    stderr.WriteLine(
                        $"note: {ConfigDisplay.Source(entry.Source)} overrides the file"
                    );
                }
            }
        });

    /// <summary>
    /// Checks a file over the defaults: each problem goes to stderr with its file:line and the
    /// exit code is 1; otherwise the keys it pins away from their defaults are listed, then
    /// <c>ok &lt;file&gt;</c>. With no path, the user file is checked and created when missing.
    /// </summary>
    /// <param name="path">The file to check, or null for the user file.</param>
    /// <returns>The exit code.</returns>
    public int Validate(string? path) =>
        Run(() =>
        {
            var file = path ?? UserFile;
            if (path is null && !File.Exists(file))
            {
                ConfigFile.Init(file, force: false);
            }
            else if (!File.Exists(file))
            {
                throw new ConfigException($"{file}: no such file");
            }
            var config = ConfigLoader.LoadFile(file, options);
            if (config.Issues.Count > 0)
            {
                throw new ConfigException(string.Join('\n', config.Issues));
            }
            foreach (var note in config.Notes)
            {
                stdout.WriteLine($"note: {note}");
            }
            foreach (var pin in ConfigPin.Of(config))
            {
                stdout.WriteLine($"note {pin}");
            }
            stdout.WriteLine($"ok {file}");
        });

    private LoadedConfig Load() => ConfigLoader.Load(options);

    /// <summary>
    /// The unified diff of a write that was not made, or <c>no changes</c>. An absolute path
    /// loses its leading slash after <c>a/</c> and <c>b/</c>, as in <c>git diff --no-index</c>.
    /// </summary>
    private void Preview(string file, (string Before, string After) text)
    {
        var diff = new Preview().Edit(file.TrimStart('/'), text.Before, text.After).Render();
        stdout.Write(diff.Length == 0 ? "no changes\n" : PreviewText.Paint(diff, color));
    }

    /// <summary>Problems in the effective config do not stop show and get; they are listed first.</summary>
    private void Warn(LoadedConfig config)
    {
        foreach (var issue in config.Issues)
        {
            stderr.WriteLine($"warning: {issue} (using the default)");
        }
    }

    private static object? Lookup(LoadedConfig config, string key)
    {
        try
        {
            return config.Find(key);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private int Run(Action action)
    {
        try
        {
            action();
            return 0;
        }
        catch (ConfigException e)
        {
            stderr.WriteLine(e.Message);
            return 1;
        }
    }

    private static void ShowJson(Utf8JsonWriter w, LoadedConfig config)
    {
        w.WriteStartArray();
        foreach (var e in config.Entries)
        {
            w.WriteStartObject();
            w.WriteString("key", e.Key);
            w.WritePropertyName("value");
            Value(w, e.Key, e.Value);
            w.WriteString("source", ConfigDisplay.Source(e.Source));
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    private static void Value(Utf8JsonWriter w, string key, object value)
    {
        switch (value)
        {
            case TomlTable table:
                w.WriteStartObject();
                foreach (var (k, v) in table)
                {
                    w.WritePropertyName(k);
                    Value(w, $"{key}.{KeyPath.Segment(k)}", v);
                }
                w.WriteEndObject();
                break;
            case TomlArray or TomlTableArray:
                w.WriteStartArray();
                foreach (var item in (System.Collections.IEnumerable)value)
                {
                    Value(w, key, item!);
                }
                w.WriteEndArray();
                break;
            case bool b:
                w.WriteBooleanValue(b);
                break;
            case long l:
                w.WriteNumberValue(l);
                break;
            case double d:
                w.WriteNumberValue(d);
                break;
            case string s:
                w.WriteStringValue(ConfigDisplay.Show(key, s));
                break;
            default:
                w.WriteStringValue(ConfigDisplay.Show(key, value));
                break;
        }
    }

    private static string Json(Action<Utf8JsonWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            write(w);
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
