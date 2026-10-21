namespace Rtok.Model;

/// <summary>One effective config key: its shown value (secrets redacted) and where it came from.</summary>
public sealed record ConfigRow(string Key, string Value, string Source)
{
    /// <summary>The row as the page prints it: <c>key = value (source)</c>.</summary>
    public override string ToString() => $"{Key} = {Value} ({Source})";
}
