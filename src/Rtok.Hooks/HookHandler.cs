using Rtok.Hook.Contracts;

namespace Rtok.Hooks;

/// <summary>
/// Runs one hook call and returns its stdout. <paramref name="replay"/> is set for a queued
/// request whose reply is already lost: only its writes count. Calls are serialized.
/// </summary>
public delegate ReadOnlyMemory<byte> HookHandler(HookRequest request, bool replay);
