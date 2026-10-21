using Rtok.Hook.Contracts;

namespace Rtok.Hooks;

/// <summary>The handlers <c>rtok hook</c> runs.</summary>
public static class HookHandlers
{
    /// <summary>Answers every call with the no-op reply. T13 replaces it with the plugin dispatch.</summary>
    public static ReadOnlyMemory<byte> NoOp(HookRequest request, bool replay) =>
        HookWire.NoOp.ToArray();
}
