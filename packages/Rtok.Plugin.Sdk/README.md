# Rtok.Plugin.Sdk

The rtok plugin contract: the plugin interface, its events, and what a plugin may ask of the
host. Depends on the BCL and `System.Text.Json` only; AOT-clean.

## Install

```bash
dotnet add package Rtok.Plugin.Sdk
```

## Use

A plugin implements `IPlugin`: `Manifest` and `DashboardPage` are required, every event has a
no-op default. Three rules hold for every plugin: fail open (return `null` when in doubt),
lossless (archive the original before shortening it), and a saving that is not a
`Measurement` does not exist.

```csharp
using System.Text;
using System.Text.Json.Nodes;
using Rtok.Plugin.Sdk;

public sealed class Shrink : IPlugin
{
    public Manifest Manifest { get; } = new("shrink", Surface.Hook, DefaultOn: true);

    public DashboardPage DashboardPage { get; } =
        new("Shrink", "Rewrites recursive `ls` to the shallow form.", SavesTokens: true);

    public PreToolDecision? OnPreTool(PreToolUse ev, Ctx cx)
    {
        if (ev.ToolName != "Bash" || !ev.ToolInput.TryGetProperty("command", out var c)
            || c.GetString() is not { } cmd || !cmd.StartsWith("ls -R ", StringComparison.Ordinal))
        {
            return null;
        }

        var id = cx.Host.PutArchive(Encoding.UTF8.GetBytes(cmd)); // lossless first
        var shallow = "ls " + cmd[6..];
        cx.Host.Record(new Measurement
        {
            Plugin = "shrink", Kind = "rewrite",
            BeforeBytes = cmd.Length, AfterBytes = shallow.Length,
            EstBefore = cx.Host.Estimate(cmd, TextClass.Code) * 200, EstAfter = 60,
            RefId = id,
        });
        return new PreToolDecision.Rewrite(
            new JsonObject { ["command"] = shallow }, $"original is expand({id})");
    }
}
```

Settings come from the plugin's `[plugins.<id>]` section through a source-generated
`JsonTypeInfo<T>`; a missing or broken section yields `new T()`:

```csharp
var config = cx.PluginConfig("shrink", MyJsonContext.Default.ShrinkConfig);
```

## Host capabilities

`Ctx.Host` is an `ICapabilities`: `IHost` (estimate, record, log, configuration) plus one
narrow interface per area of stored state: `IArchive`, `INotes`, `IReadCache`, `ILedger`,
`ISymbols`. Members a host may not support have defaults that answer empty. A proxy plugin sees
a request through `WireRequest`: its tool results, skill bodies and large blobs, each a
`JsonSlot` it can rewrite in place, and nothing else.

## Test

`Rtok.Plugin.Sdk.Testing.MemoryHost` runs a plugin without a database: it keeps measurements,
log lines, archives and archive decisions in memory and answers everything else empty.

```csharp
var host = new MemoryHost("""{"plugins": {"shrink": {"assumed_after_tokens": 60}}}""");
var decision = new Shrink().OnPreTool(ev, new Ctx(host));
Assert.Single(host.Recorded());
```

## Link into rtok

Registration is static: rtok loads no assembly at run time. List the plugin in a props file

```xml
<Project>
  <ItemGroup>
    <RtokPlugin Include="$(MSBuildThisFileDirectory)Shrink.csproj" Type="Acme.Shrink" />
  </ItemGroup>
</Project>
```

and build rtok from source with `-p:RtokPluginsFile=<that file>`. `rtok plugins` lists it as
`external`. rtok's built-in plugins are linked too unless `-p:RtokPlugins=<ids>` picks them
(`measure`, `"measure,cmd"`, or `none`).
