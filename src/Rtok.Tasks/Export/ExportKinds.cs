namespace Rtok.Tasks.Export;

/// <summary>The words of <see cref="ExportKind"/>.</summary>
public static class ExportKinds
{
    /// <summary>The kind as the report and the JSON name it: <c>create</c>, <c>update</c>, <c>close</c>, <c>reopen</c>.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The word.</returns>
    public static string Word(this ExportKind kind) =>
        kind switch
        {
            ExportKind.Create => "create",
            ExportKind.Update => "update",
            ExportKind.Close => "close",
            _ => "reopen",
        };
}
