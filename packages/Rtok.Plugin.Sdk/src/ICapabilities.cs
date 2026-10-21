namespace Rtok.Plugin.Sdk;

/// <summary>Everything a plugin may touch, in one interface. <see cref="Ctx.Host"/> is one of
/// these.</summary>
public interface ICapabilities : IHost, IArchive, INotes, IReadCache, ILedger, ISymbols;
