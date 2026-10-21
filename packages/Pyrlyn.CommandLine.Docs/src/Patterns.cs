using System.Text.RegularExpressions;

namespace Pyrlyn.CommandLine.Docs;

internal static partial class Patterns
{
    // "Usage: config get|g [arguments...] [options...] [-h|--help] [--version]"
    [GeneratedRegex(
        @"^Usage: (?<names>(?:[^\s\[][^\s]*(?: [^\s\[][^\s]*)*)?) ?(?:\[command\] )?(?:\[arguments\.\.\.\] )?(?:\[options\.\.\.\] )?\[-h\|--help\] \[--version\]$",
        RegexOptions.None,
        matchTimeoutMilliseconds: 1000
    )]
    internal static partial Regex Usage();

    // Two spaces, the head (words with single spaces: names or "[0] <type>"), then two or more spaces and the text.
    [GeneratedRegex(
        @"^  (?<head>\S+(?: \S+)*)(?: {2,}(?<text>.*))?$",
        RegexOptions.None,
        matchTimeoutMilliseconds: 1000
    )]
    internal static partial Regex Row();

    [GeneratedRegex(@"^\[\d+\] <(?<type>.+)>$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    internal static partial Regex ArgumentHead();

    [GeneratedRegex(
        @"^(?<names>-[^\s,<]+(?:, -[^\s,<]+)*)(?: <(?<type>.+)>)?$",
        RegexOptions.None,
        matchTimeoutMilliseconds: 1000
    )]
    internal static partial Regex OptionHead();

    // A doc-comment tag: <see cref="T:Ns.Type"/>, <paramref name="x"/>, <c>, </para>. A
    // reference's target, without its "T:" prefix, is the name.
    [GeneratedRegex(
        @"<(?:(?:see|seealso)\s+(?:cref|langword|href)=""(?:[A-Z]:)?(?<name>[^""]*)""|(?:paramref|typeparamref)\s+name=""(?<name>[^""]*)"")\s*/>|</?(?:c|code|b|i|para|br)\s*/?>",
        RegexOptions.None,
        matchTimeoutMilliseconds: 1000
    )]
    internal static partial Regex Tag();
}
