using System.IO.Abstractions;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pyrlyn.Config.Layered;
using Rtok.Agents.Hosts;
using Rtok.Config;
using Rtok.Mcp.Install;

namespace Rtok.Doctor;

/// <summary>The machine one doctor run looks at: paths, the disk and the environment, all through <see cref="DoctorOptions"/>.</summary>
internal sealed class DoctorHome(DoctorOptions options)
{
    private static readonly JsonDocumentOptions Jsonc = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public DoctorOptions Options => options;

    public RtokConfig Config => options.Config;

    public HostContext Hosts => options.Hosts;

    public IFileSystem Fs => options.Hosts.Fs;

    public HostEnvironment Env => options.Hosts.Env;

    public bool Windows => Env.Os == McpOs.Windows;

    public string Home => Env.Home;

    public string Cwd => options.Cwd;

    public string Project => options.Project ?? options.Cwd;

    /// <summary>An environment variable; null when unset or empty.</summary>
    public string? Var(string name) => Env.Variable(name) is { Length: > 0 } v ? v : null;

    /// <summary>A config path on this machine: <c>~/</c> and <c>$VAR/</c> expanded, a relative one under the working directory.</summary>
    public string PathOf(string spec)
    {
        var path = Hosts.Probe.Expand(spec);
        return Fs.Path.IsPathRooted(path) ? path : Fs.Path.Combine(Cwd, path);
    }

    /// <summary>A path under the home from <c>/</c>-separated parts.</summary>
    public string InHome(string relative) => Under(Home, relative);

    /// <summary>A path under <paramref name="root"/> from <c>/</c>-separated parts.</summary>
    public string Under(string root, string relative) =>
        Fs.Path.Combine([root, .. relative.Split('/', StringSplitOptions.RemoveEmptyEntries)]);

    /// <summary>The file's text; null when there is none. A file that cannot be read throws.</summary>
    /// <exception cref="IOException">It exists but cannot be read.</exception>
    /// <exception cref="InvalidDataException">It is not UTF-8.</exception>
    public string? Read(string path) => Hosts.Disk.Read(path);

    /// <summary>The text, or null when the file is missing or unreadable: for probes that report nothing about the file itself.</summary>
    public string? TryRead(string path)
    {
        try
        {
            return Read(path);
        }
        catch (Exception e) when (e is IOException or InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>JSON with comments and trailing commas allowed; blank text is an empty object.</summary>
    /// <exception cref="JsonException">The text does not parse.</exception>
    public static JsonNode? ParseJsonc(string text) =>
        string.IsNullOrWhiteSpace(text) ? new JsonObject() : JsonNode.Parse(text, null, Jsonc);

    /// <summary>TOML as a JSON tree.</summary>
    /// <exception cref="ConfigFileException">The text does not parse.</exception>
    public static JsonNode? ParseToml(string text, string path) =>
        TomlText.ToJson(TomlText.Parse(text, path));

    /// <summary>The JSON file's tree; null when it is missing, does not parse, or cannot be read.</summary>
    public JsonNode? TryJson(string path)
    {
        if (TryRead(path) is not { } text)
        {
            return null;
        }
        try
        {
            return ParseJsonc(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The path with symlinks resolved, for telling two spellings of one file apart.</summary>
    public string Canonical(string path)
    {
        try
        {
            var full = Fs.Path.GetFullPath(path);
            var target =
                Fs.File.Exists(full) ? Fs.File.ResolveLinkTarget(full, returnFinalTarget: true)
                : Fs.Directory.Exists(full)
                    ? Fs.Directory.ResolveLinkTarget(full, returnFinalTarget: true)
                : null;
            return target?.FullName ?? full;
        }
        catch (Exception e)
            when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return path;
        }
    }

    /// <summary>A string member; null when absent or not a string.</summary>
    public static string? Str(JsonNode? node) =>
        node is JsonValue v && v.GetValueKind() == JsonValueKind.String
            ? v.GetValue<string>()
            : null;
}
