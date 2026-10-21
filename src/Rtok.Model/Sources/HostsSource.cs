using Rtok.Agents.Hosts;
using Rtok.Config;

namespace Rtok.Model;

/// <summary>The Hosts page: the <c>rtok agents list</c> screen. It probes every host's apps, so
/// <see cref="ModelSources"/> reads it in the background.</summary>
/// <param name="hosts">The host context for a config: <see cref="HostContexts.Of"/> on a real
/// machine.</param>
public sealed class HostsSource(Func<LoadedConfig, HostContext> hosts) : IPageSource<TextPage>
{
    /// <inheritdoc />
    public TextPage Read(ModelContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var run = new HostRun(hosts(context.Config));
        return new TextPage(run.List(AgentHosts.All), Ready: true);
    }
}
