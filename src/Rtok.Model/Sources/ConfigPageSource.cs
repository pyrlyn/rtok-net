using Rtok.Config;

namespace Rtok.Model;

/// <summary>The Config page: every effective key with its source, secrets redacted as
/// <c>rtok config show</c> redacts them.</summary>
public sealed class ConfigPageSource : IPageSource<IReadOnlyList<ConfigRow>>
{
    /// <inheritdoc />
    public IReadOnlyList<ConfigRow> Read(ModelContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return
        [
            .. context.Config.Entries.Select(e => new ConfigRow(
                e.Key,
                ConfigDisplay.Show(e.Key, e.Value),
                ConfigDisplay.Source(e.Source)
            )),
        ];
    }
}
