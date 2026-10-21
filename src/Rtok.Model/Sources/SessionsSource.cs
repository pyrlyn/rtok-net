using Rtok.Store;

namespace Rtok.Model;

/// <summary>The Sessions page: the most recently active sessions first, with their usage.</summary>
/// <param name="store">The store.</param>
/// <param name="limit">How many sessions.</param>
public sealed class SessionsSource(RtokStore store, int limit = 100)
    : IPageSource<IReadOnlyList<SessionItem>>
{
    /// <inheritdoc />
    public IReadOnlyList<SessionItem> Read(ModelContext context) =>
        [
            .. store
                .RecentSessions(limit)
                .Select(s => new SessionItem(
                    s.Id,
                    s.Host,
                    s.Project,
                    s.Cwd,
                    s.Source,
                    s.StartedAt,
                    s.EndedAt,
                    s.LastActivity,
                    s.Input,
                    s.CacheCreate,
                    s.CacheRead,
                    s.Output
                )),
        ];
}
