namespace Rtok.Tasks.Tests;

/// <summary>The running test's cancellation token, for every call that takes one.</summary>
internal static class TestCt
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;
}
