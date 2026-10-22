using System.Collections.Concurrent;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Rtok.Proxy.Tests;

/// <summary>A logger that keeps every event, for asserting what the proxy logged.</summary>
internal sealed class LogCapture : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    public LogCapture()
    {
        Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(this).CreateLogger();
    }

    public ILogger Logger { get; }

    public IReadOnlyList<LogEvent> Events => [.. _events];

    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);
}
