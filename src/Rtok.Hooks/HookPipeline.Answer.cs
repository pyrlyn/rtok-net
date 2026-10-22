using System.Text.Json;
using System.Text.Json.Nodes;
using Rtok.Config;
using Rtok.Core;
using Rtok.Hooks.Hosts;
using Rtok.Plugin.Sdk;
using Rtok.Store;

namespace Rtok.Hooks;

public sealed partial class HookPipeline
{
    private static StoreRuntime Runtime(
        RtokStore store,
        LoadedConfig config,
        HookInput input,
        Call call,
        string? once,
        long? hostId
    )
    {
        var cwd = input.Cwd;
        var root = string.IsNullOrEmpty(cwd) ? null : ProjectRoot.Find(cwd);
        var session = SessionId.Resolve(input.SessionId, config.Config.Core, call.Env);
        return new StoreRuntime(
            store,
            new StoreRuntimeOptions(session)
            {
                Cwd = cwd,
                Project = root is null ? null : Path.GetFileName(root.Main),
                HostId = hostId,
                Once = once,
                Config = config.Json,
            }
        );
    }

    // The plugins' answer to one event (rtok dispatch_event), in the host-neutral reply.
    private HookReply Answer(
        LoadedConfig config,
        HookInput input,
        StoreRuntime runtime,
        RtokStore store,
        long? hostId
    )
    {
        var name = input.HookEventName;
        var dispatcher = new Dispatcher(
            new PluginRegistry(options.Plugins(), config.Json),
            runtime
        );
        switch (name)
        {
            case "PreToolUse":
                return PreTool(dispatcher, input);
            case "PostToolUse":
                return Context(
                    name,
                    dispatcher.PostTool(
                        new PostToolUse(
                            input.ToolName ?? "",
                            Element(input.ToolInput),
                            Element(input.ToolResponse)
                        ),
                        input.AgentId
                    )
                );
            case "SessionStart":
                return Context(
                    name,
                    dispatcher.SessionStart(new SessionStart(input.Source ?? "startup"))
                );
            case "UserPromptSubmit":
                return Context(name, dispatcher.PromptSubmit(new PromptSubmit(input.Prompt ?? "")));
            case "SubagentStart":
                return Context(
                    name,
                    dispatcher.SubagentStart(
                        new SubagentStart(input.AgentType ?? "", input.TaskDescription ?? "")
                    )
                );
            case "PreCompact":
                dispatcher.PreCompact(
                    new PreCompact(input.Trigger ?? "", input.TranscriptPath ?? "")
                );
                return new HookReply(name);
            case "PostCompact":
                // Claude puts its summary on PostCompact and reads no context there; the other
                // hosts take the post-compaction injection here, as on SessionStart("compact").
                return
                    config.Config.Hook.Host is "claude"
                    && input.Fields["compact_summary"] is not null
                    ? new HookReply(name)
                    : Context(name, dispatcher.SessionStart(new SessionStart("compact")));
            case "SessionEnd":
                store.EndSession(runtime.Session, hostId);
                return new HookReply(name);
            default:
                return new HookReply(name);
        }
    }

    private static HookReply PreTool(Dispatcher dispatcher, HookInput input)
    {
        var name = input.HookEventName;
        return dispatcher.PreTool(
            new PreToolUse(input.ToolName ?? "", Element(input.ToolInput)),
            input.AgentId
        ) switch
        {
            PreToolDecision.Deny deny => HookReply.Deny(name, deny.Reason),
            PreToolDecision.Rewrite rewrite => new HookReply(name)
            {
                Reason = rewrite.Reason,
                UpdatedInput = rewrite.Input,
            },
            _ => new HookReply(name),
        };
    }

    private static HookReply Context(string eventName, string context) =>
        new(eventName) { AdditionalContext = context.Length > 0 ? context : null };

    private static JsonElement Element(JsonNode? node)
    {
        using var doc = JsonDocument.Parse(node is null ? "{}"u8.ToArray() : HookJson.Bytes(node));
        return doc.RootElement.Clone();
    }
}
