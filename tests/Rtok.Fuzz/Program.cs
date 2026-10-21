using Rtok.Fuzz;
using SharpFuzz;

// rtok-fuzz <target>: one libFuzzer target (tests/README.md); rtok-fuzz <target> <file>... replays
// inputs, such as a crash, without libFuzzer; rtok-fuzz --list prints them all.
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
    args.Length >= 1
        ? Array.Find(FuzzTargets.All, t => string.Equals(t.Name, args[0], StringComparison.Ordinal))
        : null;
if (chosen is null)
{
    await Console.Error.WriteLineAsync("usage: rtok-fuzz <target> [<file>...] | --list");
    return 2;
}

if (chosen.Body is null)
{
    await Console.Error.WriteLineAsync($"{chosen.Name} has no body yet; {chosen.Task} wires it");
    return 2;
}

if (args.Length > 1)
{
    foreach (var file in args[1..])
    {
        chosen.Body(File.ReadAllBytes(file));
    }
    return 0;
}

Fuzzer.LibFuzzer.Run(chosen.Body);
return 0;
