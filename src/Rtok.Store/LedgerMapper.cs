using Riok.Mapperly.Abstractions;
using Rtok.Store.Model;

namespace Rtok.Store;

/// <summary>Ledger input rows to their entities (T60). Every input member lands in the column
/// of the same name; the values only the store knows come in as parameters. A member either
/// side leaves unmapped is a build error (.editorconfig), so a new input field cannot be
/// dropped on the way to its table.</summary>
[Mapper]
internal static partial class LedgerMapper
{
    [MapperIgnoreTarget(nameof(Measurement.Id))]
    public static partial Measurement ToMeasurement(
        this MeasurementInput input,
        long ts,
        string session,
        string? onceKey
    );

    [MapperIgnoreTarget(nameof(Usage.Id))]
    public static partial Usage ToUsage(
        this UsageInput input,
        long ts,
        string session,
        long callId
    );
}
