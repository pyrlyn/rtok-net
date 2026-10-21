using System.Text;
using System.Text.RegularExpressions;

namespace Pyrlyn.TreeSitter;

// Text predicates, evaluated the way the Rust binding does: the plain forms need every
// captured node to agree, the any- forms one; a match with no such node passes.
internal abstract class TextPredicate
{
    public abstract bool IsSatisfied(
        ReadOnlySpan<QueryCapture> captures,
        ReadOnlySpan<byte> source
    );

    public static TextPredicate EqCapture(int left, int right, bool isPositive, bool matchAll) =>
        new EqCapturePredicate(left, right, isPositive, matchAll);

    public static TextPredicate EqString(
        int capture,
        byte[] value,
        bool isPositive,
        bool matchAll
    ) => new EachNodePredicate(capture, isPositive, matchAll, text => text.SequenceEqual(value));

    public static TextPredicate Match(int capture, Regex regex, bool isPositive, bool matchAll) =>
        new EachNodePredicate(
            capture,
            isPositive,
            matchAll,
            text => regex.IsMatch(Encoding.UTF8.GetString(text))
        );

    // #any-of? needs every captured node to be (or, negated, not be) one of the values.
    public static TextPredicate AnyOf(int capture, byte[][] values, bool isPositive) =>
        new EachNodePredicate(capture, isPositive, matchAll: true, text => Contains(values, text));

    // Rust's regex crate has no backtracking (no lookaround or backreferences), and neither
    // has NonBacktracking: the same patterns compile, and none can blow up.
    public static Regex? CompileRegex(string pattern)
    {
        try
        {
            return new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    // True when the fold decided: a disagreeing node under match-all fails, an agreeing node
    // without match-all passes.
    private static bool Decide(bool isMatch, bool isPositive, bool matchAll, out bool result)
    {
        result = isMatch == isPositive;
        return result != matchAll;
    }

    private static bool Contains(byte[][] values, ReadOnlySpan<byte> text)
    {
        foreach (var value in values)
        {
            if (text.SequenceEqual(value))
            {
                return true;
            }
        }
        return false;
    }

    private sealed class EachNodePredicate(
        int capture,
        bool isPositive,
        bool matchAll,
        Func<ReadOnlySpan<byte>, bool> test
    ) : TextPredicate
    {
        public override bool IsSatisfied(
            ReadOnlySpan<QueryCapture> captures,
            ReadOnlySpan<byte> source
        )
        {
            foreach (var c in captures)
            {
                if (
                    c.Index == capture
                    && Decide(test(c.Node.Text(source)), isPositive, matchAll, out var result)
                )
                {
                    return result;
                }
            }
            return true;
        }
    }

    private sealed class EqCapturePredicate(int left, int right, bool isPositive, bool matchAll)
        : TextPredicate
    {
        public override bool IsSatisfied(
            ReadOnlySpan<QueryCapture> captures,
            ReadOnlySpan<byte> source
        )
        {
            var i = Next(captures, left, 0);
            var j = Next(captures, right, 0);
            while (i >= 0 && j >= 0)
            {
                var isMatch = captures[i]
                    .Node.Text(source)
                    .SequenceEqual(captures[j].Node.Text(source));
                if (Decide(isMatch, isPositive, matchAll, out var result))
                {
                    return result;
                }
                i = Next(captures, left, i + 1);
                j = Next(captures, right, j + 1);
            }
            return i < 0 && j < 0;
        }

        private static int Next(ReadOnlySpan<QueryCapture> captures, int index, int from)
        {
            for (var k = from; k < captures.Length; k++)
            {
                if (captures[k].Index == index)
                {
                    return k;
                }
            }
            return -1;
        }
    }
}
