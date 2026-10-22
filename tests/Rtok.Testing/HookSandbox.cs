using Rtok.Hook.Contracts;

namespace Rtok.Testing;

/// <summary>
/// The published <c>rtok</c> and <c>rtok-hook</c> copied side by side into a temp <c>bin/</c>,
/// as an install keeps them (a link would not do: the client looks beside its real path), with rtok's home in the same temp dir: the client finds
/// the <c>rtok</c> it may start, and no resident touches the real home. Disposing it deletes the
/// resident's lock, which stops the resident within a tick.
/// </summary>
public sealed class HookSandbox : IDisposable
{
    private readonly TempDir _dir;

    private HookSandbox(TempDir dir, string rtok, string hook)
    {
        _dir = dir;
        Directory.CreateDirectory(dir.Combine("bin"));
        Rtok = dir.Combine("bin", Path.GetFileName(rtok));
        Hook = dir.Combine("bin", Path.GetFileName(hook));
        File.Copy(rtok, Rtok);
        File.Copy(hook, Hook);
        Home = dir.Combine("h");
        Endpoint =
            HookEndpoint.For(Home)
            ?? throw new InvalidOperationException($"{Home} is too long for a socket");
    }

    /// <summary>The copied <c>rtok</c>.</summary>
    public string Rtok { get; }

    /// <summary>The copied <c>rtok-hook</c>.</summary>
    public string Hook { get; }

    /// <summary>rtok's home; pass it as <c>RTOK_HOME</c> (<see cref="Environment"/>).</summary>
    public string Home { get; }

    /// <summary>The resident's endpoint in <see cref="Home"/>.</summary>
    public HookEndpoint Endpoint { get; }

    /// <summary>The variables every process of the sandbox runs with.</summary>
    public IReadOnlyDictionary<string, string?> Environment =>
        new Dictionary<string, string?>(StringComparer.Ordinal) { ["RTOK_HOME"] = Home };

    public static async Task<HookSandbox> CreateAsync(CancellationToken ct = default) =>
        new(
            new TempDir("rs-"),
            await PublishedApps.PathAsync("rtok", ct).ConfigureAwait(false),
            await PublishedApps.PathAsync("rtok-hook", ct).ConfigureAwait(false)
        );

    /// <summary>Waits until <paramref name="condition"/> holds; false after <paramref name="timeout"/>.</summary>
    public static async Task<bool> UntilAsync(
        Func<bool> condition,
        TimeSpan timeout,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(condition);
        var until = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > until)
            {
                return false;
            }
            await Task.Delay(20, ct).ConfigureAwait(false);
        }
        return true;
    }

    public void Dispose()
    {
        try
        {
            File.Delete(Endpoint.LockPath);
        }
        catch (IOException) { }
        // A resident on its way out can still write its store and log into the home.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                _dir.Dispose();
                return;
            }
            catch (IOException) when (attempt < 50)
            {
                Thread.Sleep(100);
            }
        }
    }
}
