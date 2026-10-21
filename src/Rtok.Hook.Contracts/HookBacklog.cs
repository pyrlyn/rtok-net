namespace Rtok.Hook.Contracts;

/// <summary>
/// Requests the client could not hand to a resident (N8). Each is one request body in
/// <c>&lt;queue&gt;/&lt;utc ticks&gt;-&lt;uuid&gt;.req</c>, written under a temporary name and renamed, so the
/// resident never reads half a file. The resident replays them for their writes only: their
/// reply is already lost, which is fail-open.
/// </summary>
public static class HookBacklog
{
    /// <summary>At most this many requests wait; a resident that never starts cannot fill the disk.</summary>
    public const int MaxQueued = 256;

    private const string Suffix = ".req";

    /// <summary>Queues <paramref name="body"/>; <see langword="false"/> when the queue is full.</summary>
    public static bool Enqueue(HookEndpoint endpoint, ReadOnlySpan<byte> body)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        HookEndpoint.EnsurePrivateDir(endpoint.RunDir);
        HookEndpoint.EnsurePrivateDir(endpoint.QueueDir);
        if (Directory.EnumerateFiles(endpoint.QueueDir).Skip(MaxQueued - 1).Any())
        {
            return false;
        }
        // UTC ticks sort by time (a v7 uuid is random within its millisecond), the rest is unique.
        var name = $"{DateTime.UtcNow.Ticks:x16}-{Guid.NewGuid():N}";
        var tmp = Path.Join(endpoint.QueueDir, name + ".tmp");
        using (var file = new FileStream(tmp, Options()))
        {
            file.Write(body);
        }
        File.Move(tmp, Path.Join(endpoint.QueueDir, name + Suffix));
        return true;
    }

    /// <summary>
    /// The queued request bodies, oldest first; each file is deleted as it is read, so a request
    /// is replayed at most once.
    /// </summary>
    public static IEnumerable<byte[]> Drain(HookEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!Directory.Exists(endpoint.QueueDir))
        {
            yield break;
        }
        var files = Directory
            .EnumerateFiles(endpoint.QueueDir, "*" + Suffix)
            .Order(StringComparer.Ordinal)
            .ToList();
        foreach (var file in files)
        {
            byte[] body;
            try
            {
                body = File.ReadAllBytes(file);
                File.Delete(file);
            }
            catch (IOException)
            {
                continue;
            }
            yield return body;
        }
    }

    private static FileStreamOptions Options()
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }
        return options;
    }
}
