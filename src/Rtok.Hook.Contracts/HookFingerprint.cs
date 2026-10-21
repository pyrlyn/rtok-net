using System.Collections;
using System.Text;

namespace Rtok.Hook.Contracts;

/// <summary>
/// What in the environment changes the hook's inputs. Equal fingerprints mean the hook reads the
/// same config; the client computes its own per call, the resident once at start.
/// </summary>
public static class HookFingerprint
{
    /// <summary>Variables besides <c>RTOK_*</c> that change the hook's config.</summary>
    public static readonly IReadOnlyList<string> ConfigVariables =
    [
        "HOME",
        "USERPROFILE",
        "OTEL_EXPORTER_OTLP_ENDPOINT",
        "OTEL_EXPORTER_OTLP_HEADERS",
    ];

    /// <summary>
    /// Variables a host sets per call. They travel in the request (<see cref="HookRequest.HostEnv"/>)
    /// instead of the fingerprint, so one resident serves every call.
    /// </summary>
    public static readonly IReadOnlyList<string> HostVariables =
    [
        "GROK_HOOK_EVENT",
        "DEVIN_PROJECT_DIR",
    ];

    /// <summary>
    /// FNV-1a over the sorted <c>name\0value\0</c> pairs of the <c>RTOK_*</c> and
    /// <see cref="ConfigVariables"/> in <paramref name="vars"/>; every other variable is ignored.
    /// </summary>
    public static ulong Of(IEnumerable<KeyValuePair<string, string>> vars)
    {
        var kept = vars.Where(kv => Counts(kv.Key))
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .ThenBy(kv => kv.Value, StringComparer.Ordinal);
        var bytes = new List<byte>();
        foreach (var (name, value) in kept)
        {
            bytes.AddRange(Encoding.UTF8.GetBytes(name));
            bytes.Add(0);
            bytes.AddRange(Encoding.UTF8.GetBytes(value));
            bytes.Add(0);
        }
        return Fnv(bytes.ToArray());
    }

    /// <summary>The fingerprint of this process's environment.</summary>
    public static ulong Current() => Of(Environment());

    /// <summary>This process's <see cref="HostVariables"/> that are set, in that order.</summary>
    public static KeyValuePair<string, string>[] CurrentHostEnv() =>
        [
            .. HostVariables
                .Select(name => (name, value: System.Environment.GetEnvironmentVariable(name)))
                .Where(v => v.value is not null)
                .Select(v => new KeyValuePair<string, string>(v.name, v.value!)),
        ];

    /// <summary>64-bit FNV-1a.</summary>
    public static ulong Fnv(ReadOnlySpan<byte> bytes)
    {
        var h = 0xcbf2_9ce4_8422_2325UL;
        foreach (var b in bytes)
        {
            h = (h ^ b) * 0x0100_0000_01b3UL;
        }
        return h;
    }

    private static bool Counts(string name) =>
        name.StartsWith("RTOK_", StringComparison.Ordinal)
        || ConfigVariables.Contains(name, StringComparer.Ordinal);

    private static IEnumerable<KeyValuePair<string, string>> Environment()
    {
        foreach (DictionaryEntry e in System.Environment.GetEnvironmentVariables())
        {
            if (e.Key is string name && e.Value is string value)
            {
                yield return new(name, value);
            }
        }
    }
}
