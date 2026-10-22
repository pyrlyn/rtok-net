namespace Rtok.Plugins.Graph;

/// <summary>What <c>explore</c> asks a backend; one assembler gives every backend one answer
/// shape.</summary>
internal interface IExploreParts
{
    /// <summary>A query token as definition names: exact, else the best 5 by prefix.</summary>
    IReadOnlyList<string> Resolve(string token);

    /// <summary>Every definition of <paramref name="name"/> with its body.</summary>
    string Defs(string name);

    /// <summary>Call chains <c>a → … → b</c> in the caller direction, at most 3 hops.</summary>
    IReadOnlyList<string> Paths(string a, string b);

    /// <summary>What <c>impact(name, 1)</c> prints, and the row count behind it.</summary>
    (string Text, int Rows) Impact1(string name);

    int DefCount(string name);
}
