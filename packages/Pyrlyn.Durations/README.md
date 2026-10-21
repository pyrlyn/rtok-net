# Pyrlyn.Durations

Parses and formats human durations such as 30m, 24h and 7d (humantime grammar) and day or hour windows such as 7d or 12h.

## Install

```bash
dotnet add package Pyrlyn.Durations
```

## Use

```csharp
using Pyrlyn.Durations;

HumanDuration.Parse("30m");               // 00:30:00
HumanDuration.Parse("1h 30m");            // 01:30:00
HumanDuration.ParseDaysOrHours("7");      // 7 days: a bare number is days
HumanDuration.ParseDaysOrHours("12h");    // 12 hours
HumanDuration.Format(TimeSpan.FromHours(24));         // "1d"
HumanDuration.FormatShort(TimeSpan.FromSeconds(65));  // "1m05s"
```

- `Parse` reads the humantime grammar: `<integer><unit>` pairs, optionally spaced, summed.
  Units run from `ns` to `y` (`s`, `m`, `h`, `d`, `w`, `M` = 30.44 days, `y` = 365.25 days,
  with long and plural names). No sign, no fraction, and a number needs a unit, so `5x`,
  `-1d`, `5` and an empty string are errors.
- `ParseDaysOrHours` reads `<n>` (days), `<n>d` or `<n>h`.
- Both report overflow instead of wrapping.
- `Format` is exact and parses back; `FormatShort` keeps two units for narrow columns.
