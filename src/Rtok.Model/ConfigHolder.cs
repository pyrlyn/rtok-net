using Rtok.Config;

namespace Rtok.Model;

/// <summary>
/// The config the model reads. A reload swaps the whole loaded config under a lock, and a build
/// takes it once, so every page of one snapshot sees the same file: the old one or the new one,
/// never a mix.
/// </summary>
/// <param name="load">Loads the config; called at start and on every <see cref="Reload"/>.</param>
public sealed class ConfigHolder(Func<LoadedConfig> load)
{
    private readonly Lock _lock = new();
    private LoadedConfig _current = load();

    /// <summary>The config as last loaded.</summary>
    public LoadedConfig Current
    {
        get
        {
            lock (_lock)
            {
                return _current;
            }
        }
    }

    /// <summary>Loads the config again and swaps it in.</summary>
    public void Reload()
    {
        var next = load();
        lock (_lock)
        {
            _current = next;
        }
    }
}
