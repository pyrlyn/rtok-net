using Pyrlyn.Config.Layered;
using Rtok.Config;

namespace Rtok.Fuzz.Targets;

/// <summary>
/// rtok's config on any TOML text: layering and validation fail only with
/// <see cref="ConfigFileException"/>, and <c>config set</c> of any key and raw value fails only
/// with <see cref="ConfigException"/> and is idempotent. Nothing touches the disk.
/// </summary>
internal static class ConfigToml
{
    private const string File = "/fuzz/.rtok/config.toml";

    private static readonly ConfigLoadOptions Options = new()
    {
        Environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["HOME"] = "/fuzz",
            ["RTOK_HOME"] = "/fuzz/.rtok",
        },
        WorkingDirectory = "/fuzz",
        ReadProjectAndEnvironment = false,
    };

    public static void Run(ReadOnlySpan<byte> data)
    {
        var (pick, rest) = FuzzInput.Picked(data);
        var parts = rest.Split('\0', 2);
        var text = parts[0];
        var raw = parts.Length > 1 ? parts[1] : "1";
        try
        {
            var config = new LayeredConfigBuilder(ConfigLoader.EnvPrefix)
                .AddDefaults(ConfigSchema.Defaults)
                .AddToml("user", text, File)
                .Build();
            _ = ConfigValidator.Check(config.Entries);
        }
        catch (ConfigFileException) { }

        var keys = ConfigSchema.LeafKeys;
        var key = keys[pick % keys.Count];
        try
        {
            var edited = ConfigFile.Edit(text, File, key, raw, Options);
            FuzzInput.Check(
                string.Equals(
                    ConfigFile.Edit(edited, File, key, raw, Options),
                    edited,
                    StringComparison.Ordinal
                ),
                $"config set {key} is not idempotent"
            );
        }
        catch (ConfigException) { }
    }
}
