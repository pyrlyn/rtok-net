using System.Diagnostics.CodeAnalysis;

namespace Rtok.Archive;

/// <summary>The bytes to print, or the error to report instead.</summary>
public sealed record ExpandResult(byte[]? Body, string? Error)
{
    [MemberNotNullWhen(true, nameof(Body))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool Ok => Error is null;

    internal static ExpandResult Fail(string error) => new(null, error);
}
