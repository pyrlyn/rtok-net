namespace Rtok.Store.Model;

/// <summary>Table <c>observation_files</c>: a path an observation touched.</summary>
public class ObservationFile
{
    public long ObservationId { get; set; }
    public string Path { get; set; } = "";
}
