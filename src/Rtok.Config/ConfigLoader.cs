using System.Globalization;
using Pyrlyn.Config.Layered;
using Tomlyn.Parsing;
using Tomlyn.Syntax;

namespace Rtok.Config;

/// <summary>
/// Reads the configuration layer by layer: defaults, the user file, the project's
/// <c>.rtok.toml</c>, legacy keys, <c>.env</c> (<c>RTOK_*</c> only), the environment, flags.
/// A later layer wins key by key.
/// </summary>
public static class ConfigLoader
{
    /// <summary>The prefix of rtok's environment variables.</summary>
    public const string EnvPrefix = "RTOK_";

    /// <summary>The project file's name at the root of a git checkout.</summary>
    public const string ProjectFileName = ".rtok.toml";

    /// <summary>Loads the effective configuration. Never throws on bad input: problems land in <see cref="LoadedConfig.Issues"/>.</summary>
    /// <param name="options">What to read besides the files the load finds itself.</param>
    /// <returns>The configuration.</returns>
    public static LoadedConfig Load(ConfigLoadOptions? options = null)
    {
        options ??= new ConfigLoadOptions();
        var env = options.Lookup;
        var cwd = options.WorkingDirectory ?? Directory.GetCurrentDirectory();
        var home = RtokHome.Resolve(env);
        var userHome = RtokHome.UserHome(env);
        var userFile = RtokHome.UserConfigFile(home, options.ConfigFile, env);
        var project = options.ReadProjectAndEnvironment ? ProjectRoot.Find(cwd) : null;
        var projectFile = project is null ? null : Path.Combine(project.Checkout, ProjectFileName);
        var locations = new ConfigLocations(home, userHome, userFile, projectFile);

        var issues = new List<ConfigIssue>();
        var notes = new List<string>();
        var builder = new LayeredConfigBuilder(EnvPrefix).AddDefaults(ConfigSchema.Defaults);
        foreach (var (name, key) in ConfigSchema.EnvAliases)
        {
            builder.AddEnvAlias(name, key);
        }
        AddFile(builder, "user", userFile, options.UserText, issues);
        if (projectFile is not null)
        {
            AddFile(builder, "project", projectFile, null, issues);
        }
        FoldLegacy(builder, notes);
        if (options.ReadProjectAndEnvironment)
        {
            string?[] dotEnv = [DotEnv.FindUp(cwd), Path.Combine(home, ".env")];
            builder.AddDotEnv(".env", dotEnv.OfType<string>().Distinct(StringComparer.Ordinal));
            builder.AddEnvironment(
                "env",
                options.Environment?.Select(p => new KeyValuePair<string, string>(p.Key, p.Value))
            );
        }
        foreach (var flag in options.Flags)
        {
            builder.AddFlag("flag", flag.Key, flag.Value, flag.Flag);
        }
        var merged = builder.Build();
        notes.AddRange(merged.Warnings);
        issues.AddRange(ConfigValidator.Check(merged.Entries));
        return Finish(merged, issues, notes, locations, env);
    }

    /// <summary>The user config file a load reads: <c>--config</c>, else <c>RTOK_CONFIG</c>, else <c>&lt;home&gt;/config.toml</c>.</summary>
    /// <param name="options">The <c>--config</c> value and the environment.</param>
    /// <returns>The path, whether or not the file exists.</returns>
    public static string UserFile(ConfigLoadOptions? options = null)
    {
        options ??= new ConfigLoadOptions();
        var env = options.Lookup;
        return RtokHome.UserConfigFile(RtokHome.Resolve(env), options.ConfigFile, env);
    }

    /// <summary>The configuration a single file gives over the defaults, for <c>rtok config validate &lt;path&gt;</c>.</summary>
    /// <param name="file">The file.</param>
    /// <param name="options">The environment, for the home and path expansion.</param>
    /// <returns>The configuration.</returns>
    public static LoadedConfig LoadFile(string file, ConfigLoadOptions? options = null) =>
        Load(
            (options ?? new ConfigLoadOptions()) with
            {
                ConfigFile = file,
                ReadProjectAndEnvironment = false,
                Flags = [],
            }
        );

    private static void AddFile(
        LayeredConfigBuilder builder,
        string layer,
        string file,
        string? text,
        List<ConfigIssue> issues
    )
    {
        try
        {
            text ??= File.Exists(file) ? File.ReadAllText(file) : null;
            if (text is not null)
            {
                builder.AddToml(layer, text, file);
                EmptyTables(text, file, issues);
            }
        }
        catch (ConfigFileException e)
        {
            issues.Add(new ConfigIssue("", $"{file}:{Math.Max(e.Line, 1)}", e.Reason));
        }
        catch (IOException e)
        {
            issues.Add(new ConfigIssue("", file, e.Message));
        }
        catch (UnauthorizedAccessException e)
        {
            issues.Add(new ConfigIssue("", file, e.Message));
        }
    }

    /// <summary>An empty <c>[table]</c> sets no leaf, so only its header can say it is unknown.</summary>
    private static void EmptyTables(string text, string file, List<ConfigIssue> issues)
    {
        foreach (var table in SyntaxParser.Parse(text, file).Tables.OfType<TableSyntax>())
        {
            if (table.Name is { } name && table.Items.ChildrenCount == 0)
            {
                var key = KeyPath.Join(KeyPath.Split(name.ToString().Trim()));
                ConfigValidator.CheckTable(key, $"{file}:{name.Span.Start.Line + 1}", issues);
            }
        }
    }

    /// <summary>
    /// A legacy key a file still holds sets its new key while that one is at its default.
    /// <c>[dashboard]</c> counts only where it differs from the default, since old
    /// <c>rtok config init</c> wrote it out unchanged.
    /// </summary>
    private static void FoldLegacy(LayeredConfigBuilder builder, List<string> notes)
    {
        var snapshot = builder.Build();
        var dashboard = false;
        foreach (var (oldKey, newKey) in ConfigSchema.LegacyKeys)
        {
            if (
                !snapshot.TryGet(oldKey, out var legacy)
                || !snapshot.TryGet(newKey, out var current)
                || current.Source.Kind != ConfigSourceKind.Defaults
            )
            {
                continue;
            }
            var isDashboard = oldKey.StartsWith("dashboard.", StringComparison.Ordinal);
            if (isDashboard && Equals(legacy.Value, current.Value))
            {
                continue;
            }
            builder.AddEntry(new ConfigEntry(newKey, legacy.Value, legacy.Source));
            if (isDashboard)
            {
                dashboard = true;
            }
            else
            {
                notes.Add($"{oldKey} is now {newKey} (using {ConfigDisplay.Format(legacy.Value)})");
            }
        }
        if (dashboard)
        {
            notes.Add("[dashboard] is now [web] (using it)");
        }
    }

    /// <summary>The file and line of an issue, so a file's issues read top to bottom.</summary>
    private static (string Where, int Line) Place(ConfigIssue issue)
    {
        var colon = issue.Where.LastIndexOf(':');
        return
            colon > 0
            && int.TryParse(
                issue.Where.AsSpan(colon + 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var line
            )
            ? (issue.Where[..colon], line)
            : (issue.Where, 0);
    }

    private static LoadedConfig Finish(
        LayeredConfig merged,
        List<ConfigIssue> issues,
        List<string> notes,
        ConfigLocations locations,
        Func<string, string?> env
    )
    {
        var legacy = ConfigSchema.LegacyKeys.Select(l => l.Old).ToHashSet(StringComparer.Ordinal);
        var values = merged.Values;
        foreach (var old in legacy)
        {
            ConfigTree.Remove(values, old);
        }
        var entries = new List<ConfigEntry>(merged.Entries.Count);
        foreach (var entry in merged.Entries)
        {
            if (legacy.Contains(entry.Key))
            {
                continue;
            }
            var value = ConfigPaths.Expand(entry, locations, env);
            if (!ReferenceEquals(value, entry.Value))
            {
                ConfigTree.Set(values, entry.Key, value);
            }
            entries.Add(value == entry.Value ? entry : entry with { Value = value });
        }
        var config = ConfigBinder.Bind(entries, issues);
        var ordered = issues
            .OrderBy(i => Place(i).Where, StringComparer.Ordinal)
            .ThenBy(i => Place(i).Line)
            .ToList();
        return new LoadedConfig(config, values, entries, ordered, notes, locations);
    }
}
