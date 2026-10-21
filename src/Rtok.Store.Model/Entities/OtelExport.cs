namespace Rtok.Store.Model;

/// <summary>Table <c>otel_export</c>: the watermark of one OTel export stream.</summary>
public class OtelExport
{
    public string Stream { get; set; } = "";
    public long Mark { get; set; }
}
