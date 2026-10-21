using Rtok.Fuzz;
using SharpFuzz;

// rtok-fuzz <target>: one libFuzzer target (tests/README.md); rtok-fuzz --list prints them all.
if (args is ["--list"])
{
    foreach (var target in FuzzTargets.All)
    {
        Console.WriteLine(
            $"{target.Name, -18} {(target.Body is null ? $"wired by {target.Task}" : "ready")}"
        );
    }

    return 0;
}

var chosen =
    args.Length == 1
        ? Array.Find(FuzzTargets.All, t => string.Equals(t.Name, args[0], StringComparison.Ordinal))
        : null;
if (chosen is null)
{
    await Console.Error.WriteLineAsync("usage: rtok-fuzz <target> | --list");
    return 2;
}

if (chosen.Body is null)
{
    await Console.Error.WriteLineAsync($"{chosen.Name} has no body yet; {chosen.Task} wires it");
    return 2;
}

Fuzzer.LibFuzzer.Run(chosen.Body);
return 0;
