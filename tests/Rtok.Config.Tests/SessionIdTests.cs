namespace Rtok.Config.Tests;

public sealed class SessionIdTests
{
    [Fact]
    public void The_payload_id_wins_then_the_session_variable_then_unknown()
    {
        var core = new CoreSection();
        Func<string, string?> env = n =>
            string.Equals(n, "CLAUDE_SESSION_ID", StringComparison.Ordinal) ? "from-env" : null;

        Assert.Equal("from-stdin", SessionId.Resolve("from-stdin", core, env));
        Assert.Equal("from-env", SessionId.Resolve(" ", core, env));
        Assert.Equal("unknown", SessionId.Resolve(null, core, _ => null));
    }
}
