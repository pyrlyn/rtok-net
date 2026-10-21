namespace Rtok.Store.Model;

/// <summary>Table <c>call_io</c>: the request and response of one call, inline up to a cap, else archived.</summary>
public class CallIo
{
    public long CallId { get; set; }
    public long RequestBytes { get; set; }
    public long ResponseBytes { get; set; }
    public string? RequestSha256 { get; set; }
    public string? ResponseSha256 { get; set; }
    public string? RequestJson { get; set; }
    public string? ResponseJson { get; set; }
    public string? RequestArchive { get; set; }
    public string? ResponseArchive { get; set; }
    public byte[]? RequestRaw { get; set; }
    public byte[]? ResponseRaw { get; set; }
    public long? HookSessionId { get; set; }
}
