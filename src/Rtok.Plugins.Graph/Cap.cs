using System.Globalization;
using System.Text;
using Rtok.Plugin.Sdk;
using Rtok.Plugins.Read;

namespace Rtok.Plugins.Graph;

/// <summary>
/// Every answer's cap at <c>max_tokens</c>: whole head lines that fit, then
/// <c>N more, expand &lt;id&gt;</c> with the full answer archived. One Measurement only when
/// something was shortened, cut or smaller than what it stands for: an unchanged answer is no
/// saving and writes no row.
/// </summary>
internal static class Cap
{
    /// <summary>Room for the trailer: <c>N more, expand </c> and the printed id.</summary>
    private const int TrailerChars = 120;

    /// <summary>Caps <paramref name="text"/>; <paramref name="beforeBytes"/> is what the answer
    /// stands for, the uncut text unless <c>explore</c> replaced several calls.</summary>
    public static string Apply(Ctx cx, string text, long beforeBytes, string kind)
    {
        var max = (int)Math.Min(Indexer.Config(cx).MaxTokens, int.MaxValue);
        var est = cx.Host.Estimate(text, TextClass.Code);
        string? refId = null;
        var output = text;
        if (est > max)
        {
            refId = cx.Host.PutArchiveFor(Encoding.UTF8.GetBytes(text), cx.AgentId);
            output = Head(text, max, est) + ArchiveIds.Abbreviate(refId);
        }

        var afterBytes = Encoding.UTF8.GetByteCount(output);
        if (refId is not null || afterBytes < beforeBytes)
        {
            cx.Host.Record(
                new Measurement
                {
                    Plugin = "graph",
                    Kind = kind,
                    BeforeBytes = beforeBytes,
                    AfterBytes = afterBytes,
                    EstBefore = est,
                    EstAfter = cx.Host.Estimate(output, TextClass.Code),
                    RefId = refId,
                    CallId = cx.Host.CallId,
                }
            );
        }

        return output;
    }

    /// <summary><see cref="Apply"/> of an answer that stands for itself.</summary>
    public static string Apply(Ctx cx, string text) =>
        Apply(cx, text, Encoding.UTF8.GetByteCount(text), "cap");

    // The estimator is linear in characters, so the character budget scales the same way.
    private static string Head(string text, int max, int est)
    {
        var budget = Math.Max(0, (int)((long)text.Length * max / est) - TrailerChars);
        var lines = TextLines.Split(text);
        var head = new StringBuilder();
        var shown = 0;
        foreach (var line in lines)
        {
            if (shown > 0 && head.Length + line.Length + 1 > budget)
            {
                break;
            }

            head.Append(line).Append('\n');
            shown++;
        }

        return head.Append(CultureInfo.InvariantCulture, $"{lines.Count - shown} more, expand ")
            .ToString();
    }
}
