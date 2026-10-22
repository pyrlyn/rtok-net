using Rtok.Agents.Junk;

namespace Rtok.Agents.Tests;

/// <summary>A store whose archives past retention are given, and which records each retention run.</summary>
internal sealed class FakeJunkStore(params string[] archives) : IJunkStore
{
    public List<(int Calls, int HookBodies)> Runs { get; } = [];

    public Exception? Fails { get; init; }

    public IReadOnlyList<string> ArchivesPendingRetention(int days) => archives;

    public void RunRetention(int calls, int hookBodies)
    {
        if (Fails is { } e)
        {
            throw e;
        }
        Runs.Add((calls, hookBodies));
        foreach (var a in archives)
        {
            File.Delete(a);
        }
    }
}
