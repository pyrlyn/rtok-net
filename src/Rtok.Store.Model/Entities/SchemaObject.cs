namespace Rtok.Store.Model;

/// <summary>A row of <c>sqlite_master</c>: what the open check reads to tell a store rtok-net
/// created from one it did not (README N15).</summary>
public class SchemaObject
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Sql { get; set; }
}
