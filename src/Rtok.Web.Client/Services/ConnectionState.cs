namespace Rtok.Web.Client.Services;

/// <summary>Where the dashboard's socket stands.</summary>
public enum ConnectionState
{
    /// <summary>Connecting; no snapshot from this socket yet.</summary>
    Loading,

    /// <summary>Open, with a snapshot.</summary>
    Live,

    /// <summary>Closed or refused; the page offers Reconnect.</summary>
    Offline,
}
