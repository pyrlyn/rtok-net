using Rtok.Config;

namespace Rtok.Model;

/// <summary>
/// The operator model behind the web dashboard and the TUI: one snapshot built off-thread from
/// <see cref="ModelSources"/>, and the write actions. Every write is planned first (a dry-run
/// diff) and applied only from that plan; a stale plan is refused.
/// </summary>
public sealed class OperatorModel
{
    private readonly ModelSources _sources;
    private readonly ConfigLoadOptions _options;
    private readonly ConfigHolder _config;
    private readonly TimeProvider _time;
    private readonly HashSet<string> _pluginIds;
    private readonly SnapshotBuilder<Snapshot> _builder;

    /// <summary>Creates the model; nothing is built until <see cref="Refresh"/>.</summary>
    /// <param name="sources">Where each page comes from.</param>
    /// <param name="pluginIds">The ids of the linked plugins, the only ones a toggle may touch.</param>
    /// <param name="options">The config environment, also the one writes are checked in.</param>
    /// <param name="time">The clock; null is the system clock.</param>
    /// <param name="built">Called after each new snapshot, on the build's thread.</param>
    public OperatorModel(
        ModelSources sources,
        IEnumerable<string> pluginIds,
        ConfigLoadOptions? options = null,
        TimeProvider? time = null,
        Action<Snapshot>? built = null
    )
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(pluginIds);
        _sources = sources;
        _options = options ?? new ConfigLoadOptions();
        _config = new ConfigHolder(() => ConfigLoader.Load(_options));
        _time = time ?? TimeProvider.System;
        _pluginIds = pluginIds.ToHashSet(StringComparer.Ordinal);
        _builder = new SnapshotBuilder<Snapshot>(Build, built);
    }

    /// <summary>The last complete snapshot, or null before the first build ends.</summary>
    public Snapshot? Current => _builder.Current;

    /// <summary>The last build's error, or null when it succeeded.</summary>
    public string? Error => _builder.Error;

    /// <summary>Asks for a new snapshot and returns at once; requests during a build collapse
    /// into one more.</summary>
    public void Refresh() => _builder.Refresh();

    /// <summary>Reloads the config, then rebuilds.</summary>
    public void Reload()
    {
        _config.Reload();
        _builder.Refresh();
    }

    /// <summary>Builds one snapshot on the caller's thread.</summary>
    /// <returns>The snapshot.</returns>
    public Snapshot Build() => _sources.Build(new ModelContext(_config.Current, _time));

    /// <summary>
    /// Plans turning a plugin on or off: <c>plugins.&lt;id&gt;.enabled</c> in the user file,
    /// written and checked exactly as <c>rtok config set</c> writes it.
    /// </summary>
    /// <param name="id">A linked plugin's id.</param>
    /// <param name="enabled">The new state.</param>
    /// <returns>The planned edit; <see cref="ConfigEdit.Diff"/> is the preview.</returns>
    /// <exception cref="ConfigException">The id is not a linked plugin, or the result does not validate.</exception>
    public ConfigEdit PlanPluginToggle(string id, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (!_pluginIds.Contains(id))
        {
            throw new ConfigException($"no such plugin: {id}");
        }
        return ConfigFile.PlanSet(
            ConfigLoader.UserFile(_options),
            $"plugins.{id}.enabled",
            enabled ? "true" : "false",
            _options
        );
    }

    /// <summary>Applies a planned edit, then reloads the config and rebuilds.</summary>
    /// <param name="edit">A plan from this model.</param>
    /// <exception cref="ConfigException">The file changed since the plan.</exception>
    public void Apply(ConfigEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        edit.Apply();
        Reload();
    }
}
