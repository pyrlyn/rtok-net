using System.Text;
using Pyrlyn.TreeSitter.Native;

namespace Pyrlyn.TreeSitter;

// One pattern's predicates, sorted the way the Rust binding sorts them: text predicates it
// evaluates, #set! settings, #is?/#is-not? properties, and the rest as general predicates.
internal sealed class PredicateParser
{
    private readonly string[] _captures;
    private readonly string[] _strings;
    private readonly int _row;

    public PredicateParser(
        string[] captures,
        string[] strings,
        int row,
        ReadOnlySpan<TSQueryPredicateStep> steps
    )
    {
        _captures = captures;
        _strings = strings;
        _row = row;
        var start = 0;
        for (var i = 0; i < steps.Length; i++)
        {
            if (steps[i].Type != NativeMethods.PredicateStepDone)
            {
                continue;
            }
            if (i > start)
            {
                Add(steps[start..i]);
            }
            start = i + 1;
        }
    }

    public List<TextPredicate> Text { get; } = [];

    public List<QueryProperty> Settings { get; } = [];

    public List<(QueryProperty, bool)> Properties { get; } = [];

    public List<QueryPredicate> General { get; } = [];

    private void Add(ReadOnlySpan<TSQueryPredicateStep> p)
    {
        if (p[0].Type != NativeMethods.PredicateStepString)
        {
            throw Fail(
                $"Expected predicate to start with a function name. Got @{_captures[p[0].ValueId]}."
            );
        }
        var op = _strings[p[0].ValueId];
        switch (op)
        {
            case "eq?" or "not-eq?" or "any-eq?" or "any-not-eq?":
                AddEq(op, p);
                break;
            case "match?" or "not-match?" or "any-match?" or "any-not-match?":
                AddMatch(op, p);
                break;
            case "set!":
                Settings.Add(Property(op, p[1..]));
                break;
            case "is?" or "is-not?":
                Properties.Add(
                    (Property(op, p[1..]), string.Equals(op, "is?", StringComparison.Ordinal))
                );
                break;
            case "any-of?" or "not-any-of?":
                AddAnyOf(op, p);
                break;
            default:
                AddGeneral(op, p);
                break;
        }
    }

    private void AddEq(string op, ReadOnlySpan<TSQueryPredicateStep> p)
    {
        if (p.Length != 3)
        {
            throw Fail(
                $"Wrong number of arguments to #eq? predicate. Expected 2, got {p.Length - 1}."
            );
        }
        RequireCapture(p[1], "#eq?");
        var positive = op is "eq?" or "any-eq?";
        var all = op is "eq?" or "not-eq?";
        Text.Add(
            p[2].Type == NativeMethods.PredicateStepCapture
                ? TextPredicate.EqCapture((int)p[1].ValueId, (int)p[2].ValueId, positive, all)
                : TextPredicate.EqString(
                    (int)p[1].ValueId,
                    Encoding.UTF8.GetBytes(_strings[p[2].ValueId]),
                    positive,
                    all
                )
        );
    }

    private void AddMatch(string op, ReadOnlySpan<TSQueryPredicateStep> p)
    {
        if (p.Length != 3)
        {
            throw Fail(
                $"Wrong number of arguments to #match? predicate. Expected 2, got {p.Length - 1}."
            );
        }
        RequireCapture(p[1], "#match?");
        if (p[2].Type == NativeMethods.PredicateStepCapture)
        {
            throw Fail(
                $"Second argument to #match? predicate must be a literal. Got capture @{_captures[p[2].ValueId]}."
            );
        }
        var pattern = _strings[p[2].ValueId];
        var regex = TextPredicate.CompileRegex(pattern) ?? throw Fail($"Invalid regex '{pattern}'");
        Text.Add(
            TextPredicate.Match(
                (int)p[1].ValueId,
                regex,
                op is "match?" or "any-match?",
                op is "match?" or "not-match?"
            )
        );
    }

    private void AddAnyOf(string op, ReadOnlySpan<TSQueryPredicateStep> p)
    {
        if (p.Length < 2)
        {
            throw Fail(
                $"Wrong number of arguments to #any-of? predicate. Expected at least 1, got {p.Length - 1}."
            );
        }
        RequireCapture(p[1], "#any-of?");
        var values = new byte[p.Length - 2][];
        for (var i = 2; i < p.Length; i++)
        {
            if (p[i].Type == NativeMethods.PredicateStepCapture)
            {
                throw Fail(
                    $"Arguments to #any-of? predicate must be literals. Got capture @{_captures[p[i].ValueId]}."
                );
            }
            values[i - 2] = Encoding.UTF8.GetBytes(_strings[p[i].ValueId]);
        }
        Text.Add(
            TextPredicate.AnyOf(
                (int)p[1].ValueId,
                values,
                string.Equals(op, "any-of?", StringComparison.Ordinal)
            )
        );
    }

    private void AddGeneral(string op, ReadOnlySpan<TSQueryPredicateStep> p)
    {
        var args = new QueryPredicateArg[p.Length - 1];
        for (var i = 1; i < p.Length; i++)
        {
            args[i - 1] =
                p[i].Type == NativeMethods.PredicateStepCapture
                    ? new QueryPredicateArg((int)p[i].ValueId, null)
                    : new QueryPredicateArg(null, _strings[p[i].ValueId]);
        }
        General.Add(new QueryPredicate(op, args));
    }

    private void RequireCapture(TSQueryPredicateStep step, string name)
    {
        if (step.Type != NativeMethods.PredicateStepCapture)
        {
            throw Fail(
                $"First argument to {name} predicate must be a capture name. Got literal \"{_strings[step.ValueId]}\"."
            );
        }
    }

    private QueryProperty Property(string function, ReadOnlySpan<TSQueryPredicateStep> args)
    {
        if (args.Length is 0 or > 3)
        {
            throw Fail(
                $"Wrong number of arguments to {function} predicate. Expected 1 to 3, got {args.Length}."
            );
        }
        int? capture = null;
        string? key = null;
        string? value = null;
        foreach (var arg in args)
        {
            if (arg.Type == NativeMethods.PredicateStepCapture)
            {
                if (capture is not null)
                {
                    throw Fail(
                        $"Invalid arguments to {function} predicate. Unexpected second capture name @{_captures[arg.ValueId]}"
                    );
                }
                capture = (int)arg.ValueId;
            }
            else if (key is null)
            {
                key = _strings[arg.ValueId];
            }
            else if (value is null)
            {
                value = _strings[arg.ValueId];
            }
            else
            {
                throw Fail(
                    $"Invalid arguments to {function} predicate. Unexpected third argument @{_strings[arg.ValueId]}"
                );
            }
        }
        return key is null
            ? throw Fail($"Invalid arguments to {function} predicate. Missing key argument")
            : new QueryProperty(key, value, capture);
    }

    private QueryException Fail(string message) => QueryErrors.Predicate(_row, message);
}
