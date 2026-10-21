namespace Rtok.Store.Model;

/// <summary>A row of EF's <c>__EFMigrationsHistory</c>. The table comes from the migration
/// scripts; it is mapped only so the runner reads it with a precompiled query.</summary>
public class MigrationHistoryRow
{
    public string MigrationId { get; set; } = "";
    public string ProductVersion { get; set; } = "";
}
