using Rtok.Agents.Usage;
using Rtok.Store;

namespace Rtok.Model;

/// <summary>The Usage page: the <c>rtok agents usage</c> screen for <c>[agents.usage]</c>. It
/// scans the agents' logs, so <see cref="ModelSources"/> reads it in the background.</summary>
/// <param name="store">The store, read when the source is <c>rtok</c> or <c>both</c>.</param>
public sealed class UsageSource(RtokStore store) : IPageSource<TextPage>
{
    /// <inheritdoc />
    public TextPage Read(ModelContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var report = UsageReports.Build(
            context.Config.Config,
            new StoreLedger(store),
            context.Time.GetUtcNow().ToUnixTimeSeconds()
        );
        return new TextPage(UsageText.Render(report), Ready: true);
    }
}
