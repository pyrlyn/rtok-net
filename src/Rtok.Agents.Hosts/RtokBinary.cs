namespace Rtok.Agents.Hosts;

/// <summary>Tells rtok's own binary in a host config entry from anything else (rtok T139).</summary>
/// <param name="self">The running binary's path, which counts whatever its name; null when unknown.</param>
public sealed class RtokBinary(string? self)
{
    /// <summary>The running process's binary.</summary>
    public static RtokBinary Current { get; } = new(Environment.ProcessPath);

    /// <summary>The running binary's path; null when unknown.</summary>
    public string? Self => self;

    /// <summary>
    /// True for <c>rtok</c>, <c>rtok.exe</c> or any path to them (any case), and for the exact
    /// path of the running binary.
    /// </summary>
    public bool Is(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (self is { Length: > 0 } && string.Equals(value, self, StringComparison.Ordinal))
        {
            return true;
        }
        var name = value[(value.LastIndexOfAny(['/', '\\']) + 1)..];
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }
        return string.Equals(name, "rtok", StringComparison.OrdinalIgnoreCase);
    }
}
