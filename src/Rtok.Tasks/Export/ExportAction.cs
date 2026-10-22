namespace Rtok.Tasks.Export;

/// <summary>One write the tracker needs.</summary>
/// <param name="id">The task.</param>
/// <param name="kind">What the write does.</param>
/// <param name="fields">What differs (<c>title</c>, <c>body</c>, <c>status</c>, ...), in report order.</param>
/// <param name="url">The issue's page; for a create, known only after the write.</param>
public sealed class ExportAction(
    TaskId id,
    ExportKind kind,
    IReadOnlyList<string> fields,
    string? url
)
{
    /// <summary>The task.</summary>
    public TaskId Id => id;

    /// <summary>What the write does.</summary>
    public ExportKind Kind => kind;

    /// <summary>What differs, in report order; empty for a plain create.</summary>
    public IReadOnlyList<string> Fields => fields;

    /// <summary>The issue's page, once known.</summary>
    public string? Url { get; set; } = url;
}
