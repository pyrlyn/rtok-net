namespace Rtok.Store.Model;

/// <summary>Table <c>archive_summaries</c>: an LLM summary of an archived body.</summary>
public class ArchiveSummary
{
    public string ArchiveId { get; set; } = "";
    public string Version { get; set; } = "";
    public string Summary { get; set; } = "";
    public long CreatedAt { get; set; }
}
