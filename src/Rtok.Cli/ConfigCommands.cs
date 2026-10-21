using ConsoleAppFramework;
using Rtok.Cli.Ui;
using Rtok.Config;

namespace Rtok.Cli;

/// <summary>Read and change rtok's configuration.</summary>
internal sealed class ConfigCommands
{
    private readonly TextWriter _stdout = Console.Out;
    private readonly TextWriter _stderr = Console.Error;

    /// <summary>Write the reference config file (every key commented out at its default) and print its path.</summary>
    /// <param name="force">Overwrite an existing file.</param>
    /// <param name="dryRun">Print the diff instead of writing.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    [Command("init")]
    public int Init(bool force = false, bool dryRun = false, string? config = null) =>
        Cli(config).Init(force, dryRun);

    /// <summary>Print the path of the user config file.</summary>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    [Command("path")]
    public int Path(string? config = null) => Cli(config).Path();

    /// <summary>Print every effective key and value; secrets are redacted.</summary>
    /// <param name="sources">Show where each value came from: default, a file and line, env or flag.</param>
    /// <param name="json">Print JSON.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    [Command("show")]
    public int Show(bool sources = false, bool json = false, string? config = null) =>
        Cli(config).Show(sources, json);

    /// <summary>Print the effective value of one key or table.</summary>
    /// <param name="key">The dotted key, for example proxy.port.</param>
    /// <param name="json">Print JSON.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    [Command("get")]
    public int Get([Argument] string key, bool json = false, string? config = null) =>
        Cli(config).Get(key, json);

    /// <summary>Set a key in the user config file, keeping its comments; the result is checked first.</summary>
    /// <param name="key">The dotted key, for example proxy.port.</param>
    /// <param name="value">The value: TOML (8080, true, ["a"]) or plain text.</param>
    /// <param name="dryRun">Check the result and print the diff instead of writing.</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    [Command("set")]
    public int Set(
        [Argument] string key,
        [Argument] string value,
        bool dryRun = false,
        string? config = null
    ) => Cli(config).Set(key, value, dryRun);

    /// <summary>Check a config file: unknown keys, types and ranges, with file:line; exit 1 on any problem.</summary>
    /// <param name="path">The file to check; the user config file when omitted (created when missing).</param>
    /// <param name="config">The config file to use instead of ~/.rtok/config.toml.</param>
    [Command("validate")]
    public int Validate([Argument] string? path = null, string? config = null) =>
        Cli(config).Validate(path);

    private ConfigCli Cli(string? config) =>
        new(
            _stdout,
            _stderr,
            new ConfigLoadOptions { ConfigFile = config },
            Lines.ColorOn(stderr: false)
        );
}
