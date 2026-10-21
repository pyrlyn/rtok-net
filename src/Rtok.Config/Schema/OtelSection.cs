namespace Rtok.Config;

/// <summary>OpenTelemetry export; off until endpoint resolves</summary>
/// <remarks>Read from [otel].</remarks>
public sealed class OtelSection
{
    /// <summary>OTLP/HTTP base URL, e.g. "http://localhost:4318"; "" = $OTEL_EXPORTER_OTLP_ENDPOINT</summary>
    public string Endpoint { get; set; } = "";

    /// <summary>"k=v,k2=v2", e.g. "signoz-ingestion-key=…"; "" = $OTEL_EXPORTER_OTLP_HEADERS</summary>
    public string Headers { get; set; } = "";

    /// <summary>resource service.name</summary>
    public string ServiceName { get; set; } = "rtok";

    /// <summary>gen_ai.input/output.messages, tool arguments and results on spans</summary>
    public bool Content { get; set; } = true;

    /// <summary>per attribute; beyond it rtok.archive.id → `rtok expand &lt;id&gt;`</summary>
    public uint ContentBytes { get; set; } = 65536;

    /// <summary>proxy / mcp flush interval, and the POST timeout</summary>
    public uint FlushSecs { get; set; } = 5;
}
