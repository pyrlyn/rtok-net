using Bogus;
using Rtok.Store.Model;

namespace Rtok.Store.Tests;

/// <summary>Bogus data for the store inputs whose values a test does not look at. Every
/// generator starts from the same seed, so a test sees the same rows on every run.</summary>
internal static class Fakes
{
    private const int Seed = 59;

    /// <summary>Loose values: project names, paths, counts, log text.</summary>
    public static Faker Values() => new() { Random = new Randomizer(Seed) };

    /// <summary>A plugin's saving: fewer bytes and tokens after than before, with a ref.</summary>
    public static Faker<MeasurementInput> Measurement() =>
        new Faker<MeasurementInput>()
            .UseSeed(Seed)
            .CustomInstantiator(f =>
            {
                var before = f.Random.Long(1, 100_000);
                var estBefore = f.Random.Long(1, before);
                return new MeasurementInput(
                    f.Lorem.Word(),
                    f.Lorem.Word(),
                    before,
                    f.Random.Long(0, before),
                    estBefore,
                    f.Random.Long(0, estBefore),
                    f.Random.Hash(12)
                );
            });

    /// <summary>One usage result without a key.</summary>
    public static Faker<UsageInput> Usage() =>
        new Faker<UsageInput>()
            .UseSeed(Seed)
            .CustomInstantiator(f => new UsageInput(
                $"{f.Lorem.Word()}-{f.Random.Int(1, 9)}",
                f.PickRandom("anthropic", "openai", "batch"),
                f.Random.Long(0, 1_000_000),
                f.Random.Long(0, 1_000_000),
                f.Random.Long(0, 1_000_000),
                f.Random.Long(0, 100_000),
                null
            ));

    /// <summary>One definition under <paramref name="root"/>.</summary>
    public static Symbol Symbol(string root)
    {
        var symbol = new Faker<Symbol>()
            .UseSeed(Seed)
            .RuleFor(s => s.Path, f => f.System.FileName("cs"))
            .RuleFor(s => s.Name, f => f.Hacker.Noun())
            .RuleFor(s => s.Kind, f => f.PickRandom("class", "function", "method"))
            .RuleFor(s => s.Line, f => f.Random.Long(1, 500))
            .RuleFor(s => s.EndLine, (f, s) => s.Line + f.Random.Long(0, 50))
            .RuleFor(s => s.StartByte, f => f.Random.Long(0, 10_000))
            .RuleFor(s => s.EndByte, (f, s) => s.StartByte + f.Random.Long(1, 2_000))
            .RuleFor(s => s.IsDef, true)
            .RuleFor(s => s.Size, (_, s) => s.EndByte)
            .RuleFor(s => s.Signature, f => f.Lorem.Sentence(3))
            .Generate();
        symbol.Root = root;
        return symbol;
    }
}
