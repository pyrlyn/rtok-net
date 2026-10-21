namespace Rtok.Config;

/// <summary>rtok demon start | stop | restart | status | kill</summary>
/// <remarks>Read from [demon].</remarks>
public sealed class DemonSection
{
    /// <summary>what `rtok demon start` starts when given no name</summary>
    public IList<string> Services { get; set; } = ["proxy"];

    /// <summary>&lt;name&gt;.json, &lt;name&gt;.log and the &lt;name&gt;.stop marker</summary>
    public string StateDir { get; set; } = "~/.rtok/demon";

    /// <summary>wait before the first restart after a crash</summary>
    public ulong BackoffMs { get; set; } = 1000;

    /// <summary>ceiling for a crash loop</summary>
    public ulong MaxBackoffMs { get; set; } = 30000;

    /// <summary>a child up this long resets the backoff</summary>
    public ulong HealthyMs { get; set; } = 10000;

    /// <summary>how often the supervisor looks at its child</summary>
    public ulong PollMs { get; set; } = 200;
}
