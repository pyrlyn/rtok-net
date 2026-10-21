using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Rtok.Agents.Usage;

/// <summary><c>--json</c>: the report with its fields in a fixed order, the money as plain decimals.</summary>
public static class UsageJson
{
    /// <summary>The report as indented JSON, without a trailing newline.</summary>
    public static string Render(UsageReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        using var buffer = new MemoryStream();
        using (
            var w = new Utf8JsonWriter(
                buffer,
                new JsonWriterOptions
                {
                    Indented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                }
            )
        )
        {
            w.WriteStartObject();
            w.WriteString("source", report.Source);
            w.WriteString("tz", report.Tz);
            w.WriteString("through", report.Through);
            w.WriteStartObject("totals");
            Row(w, report.Totals.Row);
            w.WriteNumber("sessions", report.Totals.Sessions);
            w.WriteNumber("daily_rows", report.Totals.DailyRows);
            Saved(w, report.Totals.Saved);
            w.WriteEndObject();
            w.WriteNumber("unpriced_models", report.UnpricedModels);
            Array(w, "unpriced", report.Unpriced, Unpriced);
            Array(w, "agents", report.Agents, Agent);
            if (report.Models is { } models)
            {
                Array(w, "models", models, Model);
            }
            Array(w, "periods", report.Periods, Period);
            Array(w, "skipped", report.Skipped, Skip);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void Unpriced(Utf8JsonWriter w, UsageUnpriced u)
    {
        w.WriteString("model", u.Model);
        w.WriteString("host", u.Host);
        w.WriteNumber("tokens", u.Tokens);
    }

    private static void Model(Utf8JsonWriter w, UsageModelRow m)
    {
        w.WriteString("model", m.Model);
        Row(w, m.Row);
    }

    private static void Period(Utf8JsonWriter w, UsagePeriod p)
    {
        w.WriteString("period", p.Period);
        Row(w, p.Row);
    }

    private static void Skip(Utf8JsonWriter w, UsageSkip s)
    {
        w.WriteString("host", s.Host);
        w.WriteString("reason", s.Reason);
        w.WriteString("path", s.Path);
    }

    /// <summary>
    /// A double the way the rest of rtok's JSON writes one: the shortest digits that read back the
    /// same, as a plain decimal, and a whole number keeps <c>.0</c> (<c>6.0</c>, <c>0.000001</c>).
    /// </summary>
    internal static string Number(double v)
    {
        var r = v.ToString("R", CultureInfo.InvariantCulture);
        var e = r.IndexOfAny(['E', 'e']);
        if (e >= 0)
        {
            var sign = r[0] == '-' ? "-" : "";
            var mantissa = r[sign.Length..e];
            var exponent = int.Parse(
                r[(e + 1)..],
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture
            );
            var point = mantissa.IndexOf('.', StringComparison.Ordinal);
            var digits = mantissa.Replace(".", "", StringComparison.Ordinal);
            var at = (point < 0 ? mantissa.Length : point) + exponent;
            r =
                at <= 0 ? $"{sign}0.{new string('0', -at)}{digits}"
                : at >= digits.Length ? $"{sign}{digits}{new string('0', at - digits.Length)}"
                : $"{sign}{digits[..at]}.{digits[at..]}";
        }
        return r.Contains('.', StringComparison.Ordinal) ? r : r + ".0";
    }

    private static void Agent(Utf8JsonWriter w, UsageAgent a)
    {
        w.WriteString("host", a.Host);
        w.WriteString("name", a.Name);
        Row(w, a.Row);
        if (a.ThroughRtokTokens is { } through)
        {
            w.WriteNumber("through_rtok_tokens", through);
        }
        if (a.Coverage is { } coverage)
        {
            w.WritePropertyName("coverage");
            w.WriteRawValue(Number(coverage));
        }
        Saved(w, a.Saved);
    }

    private static void Row(Utf8JsonWriter w, UsageRow row)
    {
        w.WriteNumber("tokens", row.Tokens);
        w.WriteNumber("input", row.Input);
        w.WriteNumber("cache_write", row.CacheWrite);
        w.WriteNumber("cache_read", row.CacheRead);
        w.WriteNumber("output", row.Output);
        Money(w, "cost_usd", row.CostUsd);
    }

    private static void Saved(Utf8JsonWriter w, UsageSaved? saved)
    {
        if (saved is not null)
        {
            w.WriteNumber("saved_tokens", saved.Tokens);
            Money(w, "saved_usd", saved.Usd);
        }
    }

    private static void Money(Utf8JsonWriter w, string name, double? usd)
    {
        w.WritePropertyName(name);
        if (usd is { } v)
        {
            w.WriteRawValue(Number(v));
        }
        else
        {
            w.WriteNullValue();
        }
    }

    private static void Array<T>(
        Utf8JsonWriter w,
        string name,
        IEnumerable<T> items,
        Action<Utf8JsonWriter, T> write
    )
    {
        w.WriteStartArray(name);
        foreach (var item in items)
        {
            w.WriteStartObject();
            write(w, item);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }
}
