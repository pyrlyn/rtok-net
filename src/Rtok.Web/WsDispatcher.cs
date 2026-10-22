using System.Text;
using System.Text.Json;
using Rtok.Archive;
using Rtok.Config;
using Rtok.Model;
using Rtok.Web.Contracts;

namespace Rtok.Web;

/// <summary>
/// Answers one <c>/ws</c> text message with one frame that echoes its request id. Every message
/// is answered or refused, never thrown on. Writes go through the operator model, the function
/// the CLI uses; nothing in a message is used as a path to open.
/// </summary>
internal sealed class WsDispatcher(OperatorModel model, Expander? expander)
{
    /// <summary>The largest message read; a bigger one is refused.</summary>
    public const int MaxMessageBytes = 1 << 20;

    private const string NotYet = " is not available in this build yet";

    /// <summary>The frame that answers <paramref name="text"/>.</summary>
    /// <param name="text">One whole text message.</param>
    /// <returns>The answer.</returns>
    public ServerFrame Answer(string text)
    {
        WsRequest? request;
        try
        {
            request = JsonSerializer.Deserialize(text, WsJsonContext.Default.WsRequest);
        }
        catch (JsonException e)
        {
            return new MessageFrame(
                e.Path is { Length: > 1 } at ? $"not a request: bad {at[2..]}" : "not a request"
            );
        }
        if (request is null)
        {
            return new MessageFrame("not a request");
        }
        ServerFrame answer =
            request.Requests() != 1
                ? new MessageFrame(
                    "a request names exactly one of toggle, expand, doctor, graph, project, calls"
                )
                : request switch
                {
                    { Toggle: { } toggle } => Toggle(toggle),
                    { Expand: { } id } => Expand(id),
                    { Doctor: not null } => new MessageFrame("doctor" + NotYet),
                    { Graph: not null } => new MessageFrame("graph" + NotYet),
                    { Project: not null } => new MessageFrame("project links" + NotYet),
                    _ => new MessageFrame("live calls" + NotYet),
                };
        return answer with { Req = request.Req };
    }

    private ServerFrame Expand(string id)
    {
        if (expander is null)
        {
            return new MessageFrame("expand needs the store, and none is open");
        }
        try
        {
            var result = expander.Run(new ExpandRequest(id));
            return result.Ok
                ? new ExpandFrame(id, Encoding.UTF8.GetString(result.Body))
                : new MessageFrame(result.Error);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new MessageFrame($"expand {id}: {e.Message}");
        }
    }

    private ServerFrame Toggle(ToggleRequest toggle)
    {
        try
        {
            model.Apply(model.PlanPluginToggle(toggle.Plugin, toggle.Enabled));
            return new DoneFrame(
                $"plugins.{toggle.Plugin}.enabled = {(toggle.Enabled ? "true" : "false")}"
            );
        }
        catch (Exception e)
            when (e is ConfigException or IOException or UnauthorizedAccessException)
        {
            return new MessageFrame(e.Message);
        }
    }
}
