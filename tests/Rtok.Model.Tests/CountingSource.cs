namespace Rtok.Model.Tests;

/// <summary>A text page that counts its reads and can hold a read until released.</summary>
internal sealed class CountingSource(string text) : IPageSource<TextPage>
{
    private int _reads;

    public int Reads => Volatile.Read(ref _reads);

    public ManualResetEventSlim Gate { get; } = new(initialState: true);

    public TextPage Read(ModelContext context)
    {
        var n = Interlocked.Increment(ref _reads);
        Gate.Wait(TimeSpan.FromSeconds(10));
        return new TextPage($"{text} {n}\n", Ready: true);
    }
}
