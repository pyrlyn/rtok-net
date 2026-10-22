namespace Rtok.Store;

/// <summary>One write transaction around many store calls; see
/// <see cref="RtokStore.BeginBatch"/>. Disposing it without <see cref="Commit"/> rolls every
/// write back.</summary>
public sealed class StoreBatch : IDisposable
{
    private RtokStore? _store;

    internal StoreBatch(RtokStore store) => _store = store;

    /// <summary>Commits every write made since the batch began.</summary>
    public void Commit()
    {
        var store = _store ?? throw new ObjectDisposedException(nameof(StoreBatch));
        _store = null;
        store.EndBatch(commit: true);
    }

    public void Dispose()
    {
        var store = _store;
        _store = null;
        store?.EndBatch(commit: false);
    }
}
