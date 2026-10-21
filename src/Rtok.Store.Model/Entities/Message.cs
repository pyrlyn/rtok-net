namespace Rtok.Store.Model;

/// <summary>Table <c>messages</c>: a message between agents and the user.</summary>
public class Message
{
    public long Id { get; set; }
    public string? FromAgent { get; set; }
    public string ToAgent { get; set; } = "";
    public string Body { get; set; } = "";
    public long CreatedAt { get; set; }
    public long? DeliveredAt { get; set; }
    public long? ReadAt { get; set; }
    public string? Task { get; set; }
}
