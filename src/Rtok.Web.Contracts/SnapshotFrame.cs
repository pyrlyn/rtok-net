namespace Rtok.Web.Contracts;

/// <summary>The whole operator snapshot: the first frame on every socket, then one per tick
/// when it changed.</summary>
/// <param name="Snapshot">Every page as plain data.</param>
public sealed record SnapshotFrame(Snapshot Snapshot) : ServerFrame;
