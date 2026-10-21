using Rtok.Plugin.Sdk;

namespace Rtok.Plugins.Inject;

/// <summary>
/// The inject plugin: the configured prompt modes, offered once per session at SessionStart
/// (priority 5). Core budgets what every plugin offers (<see cref="Core.InjectBudget"/>), so
/// this plugin owns only what the modes say. A later prompt in the same session gets nothing,
/// and the same configuration gives the same bytes, so the prompt cache keeps hitting.
/// </summary>
public sealed class InjectPlugin : IPlugin
{
    /// <summary>The priority modes are offered at.</summary>
    public const int ModePriority = 5;

    /// <inheritdoc/>
    public Manifest Manifest { get; } = new("inject", Surface.Hook, DefaultOn: true);

    /// <inheritdoc/>
    public DashboardPage DashboardPage { get; } =
        new("Inject", "Budgeted SessionStart / prompt context that stays byte-stable.", true);

    /// <inheritdoc/>
    public Injection? OnSessionStart(SessionStart ev, Ctx cx)
    {
        ArgumentNullException.ThrowIfNull(cx);
        var text = ModeFiles.Text(cx);
        return text.Length == 0 ? null : new Injection(Manifest.Id, text, ModePriority);
    }
}
