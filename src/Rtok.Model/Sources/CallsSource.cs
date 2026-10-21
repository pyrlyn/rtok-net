using Rtok.Store;

namespace Rtok.Model;

/// <summary>The Calls page: the newest calls first, with their usage.</summary>
/// <param name="store">The store.</param>
/// <param name="limit">How many calls.</param>
public sealed class CallsSource(RtokStore store, int limit = 200)
    : IPageSource<IReadOnlyList<CallItem>>
{
    /// <inheritdoc />
    public IReadOnlyList<CallItem> Read(ModelContext context) =>
        [
            .. store
                .RecentCalls(limit)
                .Select(c => new CallItem(
                    c.Id,
                    c.Ts,
                    c.Session,
                    c.Surface,
                    c.Kind,
                    c.Plugin,
                    c.Name,
                    c.ParentId,
                    c.Ms,
                    c.Ok,
                    c.Error,
                    c.Host,
                    c.Provider,
                    c.Model,
                    c.Input,
                    c.CacheCreate,
                    c.CacheRead,
                    c.Output
                )),
        ];
}
