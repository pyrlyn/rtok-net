namespace Rtok.Tasks.Remote;

/// <summary>The claim lock of an issue tracker: there is no shared lock to take, so claims race
/// at the service and the last write wins.</summary>
internal sealed class NoLock : IDisposable
{
    public static readonly Task<IDisposable> Task =
        System.Threading.Tasks.Task.FromResult<IDisposable>(new NoLock());

    public void Dispose() { }
}
