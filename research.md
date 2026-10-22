# Research: rtok on .NET 10 (NativeAOT release, JIT debug)

Research date: 2026-10-21. .NET 10 is the current LTS (SDK 10.0.402, runtime and ILCompiler 10.0.13). .NET 11 is at RC2.

Legend:
- **[measured]**: run locally on an M3 Max, macOS arm64, SDK 10.0.402, using `hyperfine -N` with 300 to 500 runs. The bench projects were throwaway and are not kept; rerun them from the descriptions here.
- **[doc]**: taken from the URL cited next to it.
- **[nuget]**: read from the NuGet registration API (`https://api.nuget.org/v3/registration5-gz-semver2/{id}/index.json`) or from the package's nuspec and DLL metadata.
- **[unverified]**: I could not confirm it. Treat it as a lead, not a fact.

Source for the Rust side: `/Users/listepo/GitHub/pyrlyn/rtok/Cargo.toml` (rtok 0.17.0-alpha.2, GPL-3.0-or-later). It builds two binaries: `rtok`, and `rtok-hook`, a std-only client.

---

## TL;DR

1. **Hook startup.** A NativeAOT hook that only parses JSON with System.Text.Json (STJ) source gen starts in **5.2 to 5.5 ms**. The same code under JIT starts in 43 ms. `/usr/bin/true` takes 1.7 ms and the Rust `rtok --version` takes 6.9 ms **[measured]**. The 10 ms budget is reachable, but **only from a small, separate hook binary**.
   - The same hook path inside a "fat" binary (Kestrel + MCP SDK + SQLite + System.CommandLine) takes **8.8 to 9.3 ms**, even though none of that code runs **[measured]**.
   - Keep `rtok-hook` as its own project, as the Rust version already does (D32 in Cargo.toml).
2. **Turn on PublishAot only in Release.** Setting `PublishAot=true` writes AOT feature switches into `runtimeconfig.json` even for JIT/Debug builds:
   - `IsDynamicCodeSupported=false`
   - `MetadataUpdater.IsSupported=false`
   - `JsonSerializer.IsReflectionEnabledByDefault=false`

   This kills hot reload and makes EF Core throw under plain `dotnet run` **[measured]**. Use `<PublishAot Condition="'$(Configuration)'=='Release'">true</PublishAot>` and keep `IsAotCompatible=true` on every library, so the analyzers still run in Debug.
3. **Inner loop on a fat project [measured]:**
   - Incremental Debug build after an edit: about 0.55 s.
   - `dotnet run --no-build`: about 0.5 s.
   - Launching the apphost directly: about 0.06 s.
   - AOT publish: about 3 s for the small app and about 18 s for the fat one.
4. **Stack.** Nearly everything rtok needs has an AOT-clean BCL or NuGet answer:
   - STJ, GeneratedRegex and ConsoleAppFramework v5 (the creator's choice over System.CommandLine 2, which also works).
   - Kestrel slim, YARP and the MCP C# SDK 2.3.
   - Microsoft.Data.Sqlite, Tomlyn, ML.Tokenizers, ZstdSharp, DiffPlex, OpenTelemetry, Spectre.Console (rendering only) and Terminal.Gui v2.
5. **Known blockers:**
   - **EF Core**: AOT is "highly experimental"; with a compiled model and precompiled queries it works (C.4), but costs about 9 ms per cold process and cannot run migrations itself.
   - **Spectre.Console.Cli**: fails at runtime under AOT.
   - **linq2db**: fails at runtime under AOT.
   - **Tree-sitter**: the only NuGet ships per-grammar dylibs (62 MB) and lacks Dart, Kotlin and Swift. Write your own package.
   - **Claude tokenizer**: there is no local one. Use the free `count_tokens` API.
   - **Cross-OS AOT**: not possible. You need one CI runner per OS.

---

## A. NativeAOT in .NET 10

### A.1 csproj properties

| Property | Use | Source |
|---|---|---|
| `PublishAot` | Turns on ILC at publish time. Also turns on AOT, trim and single-file analyzers at build time, and **writes AOT feature switches into runtimeconfig even for `dotnet run`**. | [doc] https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/ ; switch effect [measured] |
| `IsAotCompatible` | For libraries. Implies `IsTrimmable`, `EnableTrimAnalyzer`, `EnableSingleFileAnalyzer` and `EnableAotAnalyzer`. **New in .NET 10:** it also stamps `[assembly: AssemblyMetadata("IsAotCompatible","True")]`. | [doc] https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trimming-options ; metadata [nuget, scanned DLLs] |
| `VerifyReferenceAotCompatibility` | Warns with **IL3058** when a referenced assembly is not marked AOT-compatible. Useful as a gate in CI. | [doc] trimming-options page above |
| `InvariantGlobalization` | Avoids ICU and saves size. Required for a lean hook. | [doc] https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/optimizing |
| `OptimizationPreference` | `Size` or `Speed`; the default is blended. On a hook-sized app: blended 5.2 to 5.5 ms / 2.26 MB, Speed 5.9 ms / 2.31 MB, Size 5.7 to 6.3 ms / 2.21 MB. **Leave it at the default.** | [doc] optimizing page; numbers [measured] |
| `StripSymbols` | Default true on Linux/macOS since .NET 8. It produces a `.dbg` / `.dSYM` file next to the binary. | [doc] optimizing page |
| `UseSystemResourceKeys` | Exception messages become resource keys, which saves size. | [doc] https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trimming-options |
| `EventSourceSupport=false` | Removes EventSource. **Do not set this if you use OpenTelemetry metrics through `System.Diagnostics.Metrics` listeners [unverified].** | [doc] trimming-options |
| `StackTraceSupport` | Keep it true; the hook logs stack traces when it fails open. `StackTraceLineNumberSupport` is new in .NET 11. | [doc] https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/runtime |
| `UseSizeOptimizedLinq` | .NET 10; defaults to true under AOT. | [doc] optimizing page |
| `Http3Support=false` | .NET 10 switch; drops msquic from the proxy if you don't need H3. | [doc] optimizing page |
| `TrimmerSingleWarn=false` | Reports one warning per call site instead of one per assembly. Use it while you clean up warnings. | [doc] trimming-options |
| `TreatWarningsAsErrors` with `WarningsAsErrors=IL2026;IL2104;IL3050;IL3053;IL3058` | Makes AOT regressions fail the build. | [doc] trimming-options |
| `DirectPInvoke`, `NativeLibrary`, `LinkerArg` | Link a native `.a` statically into the binary, for example SQLite or tree-sitter grammars. | [doc] https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/interop |
| `IlcGenerateMstatFile`, `IlcGenerateDgmlFile` | Size analysis; use them with sizoscope. | [doc] optimizing page |

### A.2 Startup and size numbers [measured]

All numbers are macOS arm64 (M3 Max).

| Binary | Contents | Size | `hook` path wall time |
|---|---|---|---|
| `/usr/bin/true` | baseline | n/a | 1.7 ms |
| Rust `rtok --version` (dev profile) | baseline | n/a | 6.9 ms |
| hook, AOT, blended | STJ source gen; reads stdin, writes a JSON decision | 2.26 MB | **5.2 to 5.5 ms** |
| hook, JIT apphost | same code | n/a | 43.2 ms |
| fat2, AOT | hook + System.CommandLine + Microsoft.Data.Sqlite | 3.9 MB + libe_sqlite3.dylib 1.7 MB | 5.0 to 6.0 ms |
| fat3, AOT | fat2 + ASP.NET Core FrameworkReference (Kestrel) | 9.0 MB | 6.5 ms |
| fat, AOT | fat3 + ModelContextProtocol.AspNetCore 2.3.0 | 16.5 MB | **8.8 to 9.3 ms** |
| fat `--help` | System.CommandLine | n/a | 9.1 ms |
| caf, AOT | ConsoleAppFramework 5.7.13 + Spectre.Console, command or `--help` | 2.7 MB | 4.7 ms |
| fat `db` | open SQLite and query | n/a | 11.0 ms |
| proxy, AOT | Kestrel slim + YARP + SSE + WebSocket + OTel OTLP | 15.5 MB | first HTTP response about 80 to 95 ms (upper bound; includes the curl polling loop). JIT about 200 ms, first JIT run 609 ms. |

Notes:
- Kestrel costs about 1.5 ms and the MCP SDK about 2.3 ms on a hook path that never touches them. The likely cause is static constructors, module initializers and binary size, not dyld: both binaries load the same 701 dylib entries.
- Every macOS AOT binary links CoreFoundation, Foundation, Security, Network, CryptoKit, GSS and swiftCore, regardless of what the app uses.
- I measured only on macOS. **Linux and Windows numbers are [unverified].** Linux startup is typically lower than macOS because dyld and code-signing checks don't apply, but measure in CI.

### A.3 Cross-compilation and RIDs

- **No cross-OS compilation.** You cannot build a Linux binary on macOS or a Windows binary on Linux. Cross-architecture builds (x64 to arm64) work on the same OS when the native toolchain is present: on Linux that means clang, lld and a sysroot; on Windows, the VS arm64 tools. [doc] https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/cross-compile
- RIDs to ship: `osx-arm64`, `osx-x64`, `linux-x64`, `linux-arm64`, `linux-musl-x64`, `linux-musl-arm64`, `win-x64`, `win-arm64`. [doc] https://learn.microsoft.com/en-us/dotnet/core/rid-catalog
- The **.NET 11** x64 baseline rises to x86-64-v2. [doc] https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/runtime
- Linux prerequisites: clang (or gcc), and zlib1g-dev or the equivalent. At runtime HttpClient TLS loads **OpenSSL (libssl)** dynamically, so unlike rustls this is not a fully static TLS stack. [doc] native-aot prerequisites on the main AOT page.
- Release CI is a matrix of one runner per OS, each building its arches. `dotnet-releaser` 0.24.0 (BSD-2) [nuget] automates a cargo-dist-like flow; it is NativeAOT-aware per its README [unverified in this session].

### A.4 Analyzers

These warnings come up during an AOT build:
- IL2026 (RequiresUnreferencedCode)
- IL2070 to IL2095 (DynamicallyAccessedMembers flow)
- IL3050 (RequiresDynamicCode)
- IL3000 (`Assembly.Location` is empty in single-file)
- IL2104 / IL3053 (an assembly produced trim or AOT warnings; shown collapsed)
- IL3058 (the reference is not AOT-compatible)

The analyzers run at build time when `IsAotCompatible` or `PublishAot` is set, so you see the warnings in Debug without publishing. ILC still finds a few more at publish time, which is why CI needs an AOT publish leg (B.8). [doc] https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/

### A.5 Required source generators

| Need | Generator | Source |
|---|---|---|
| JSON | `JsonSerializerContext` + `[JsonSerializable]`, with `JsonSourceGenerationOptions` | [doc] https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation |
| Regex | `[GeneratedRegex]` on a `partial` method/property | [doc] https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-source-generators |
| Logging | `[LoggerMessage]` | [doc] https://learn.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator |
| Config binding | `EnableConfigurationBindingGenerator=true`; it is automatic under PublishAot | [doc] https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-generator |
| Options validation | `[OptionsValidator]` | [doc] same family |
| P/Invoke | `[LibraryImport]` instead of `[DllImport]` | [doc] https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke-source-generation |
| Minimal APIs | Request Delegate Generator; automatic with `PublishAot` in the Web SDK | [doc] https://learn.microsoft.com/en-us/aspnet/core/fundamentals/native-aot |
| COM / marshalling | `[GeneratedComInterface]` (probably not needed) | [doc] |
| MCP tools | The ModelContextProtocol.Core package ships an analyzer/generator; `WithTools<T>()` with attribute tools published with zero warnings | [nuget] analyzers folder; [measured] |
| TOML | Tomlyn 2.x `TomlSerializerContext`, or CsToml `[TomlSerializedObject]` | [nuget]; https://github.com/xoofx/Tomlyn , https://github.com/prozolic/CsToml |
| DI (optional) | Jab 0.12.0 or Pure.DI 2.5.5 compile-time containers. MS.DI also works under AOT if you avoid open generics over value types. | [nuget] |
| SQLite rows | SQLite.Framework.SourceGenerator 7.1.1 (`UseGeneratedMaterializers()`), or Dapper.AOT 1.1.0 interceptors | [measured] for SQLite.Framework; https://github.com/DapperLib/DapperAOT |

### A.6 Reflection pitfalls (and what broke here)

- **STJ reflection is disabled** under AOT (`IsReflectionEnabledByDefault=false`). Any `JsonSerializer.Serialize(obj)` call without type info throws, so always pass `Ctx.Default.T`. That includes `Results.Json` and `HttpClient.GetFromJsonAsync`.
- **`Assembly.Location` is empty** (IL3000). Use `AppContext.BaseDirectory` and `Environment.ProcessPath`. Spectre.Console.Cli and linq2db hit this.
- **`Type.GetType("...")` string lookups, `Activator.CreateInstance(Type)` and `MakeGenericType` over value types** need annotations or rooting.
- **`System.Linq.Expressions` `Compile()`** is interpreted under AOT: it is slow, and ORMs built on it break. EF Core says "Model building is not supported when publishing with NativeAOT" [measured]. linq2db failed with `Method not found ... ClearAllPools` [measured].
- **`dynamic`** is not supported (Scriban's dynamic binder gives IL2026).
- **Generic virtual methods over many value types** can bloat the binary. .NET 11 adds a faster AOT interface-dispatch helper. [doc] dotnet-11 runtime page.
- **Library-specific findings [measured]:**
  - **Spectre.Console.Cli 0.57.2** fails at runtime with "Could not get settings type for command". Spectre.Console (rendering) is clean.
  - **ConsoleAppFramework 5.7.13 + Spectre.Console 0.57.2** (2026-10-21, osx-arm64): zero AOT warnings, 2.7 MB, about 4.7 ms for a command or `--help`. Nested commands, doc-comment aliases (`-a, Agent id.`) and raw arguments after `--` (`ConsoleAppContext.EscapedArguments`) work. A parse error exits 1 (hooking `ConsoleApp.LogError` lets the app exit 2); an unknown command prints the root help and exits 0. There is no command tree at runtime. A doc comment naming a missing parameter is a build error (CAF015).
  - **RazorConsole.Core 0.6.0** is binary-incompatible with Spectre.Console 0.57.2: ILC reports "Failed to load type Spectre.Console.Style".

---

## B. Debug inner loop

### B.1 Rule 1: PublishAot only in Release

```xml
<PublishAot Condition="'$(Configuration)' == 'Release'">true</PublishAot>
<IsAotCompatible>true</IsAotCompatible>  <!-- keeps the analyzers on in Debug -->
```

Why [measured]: with an unconditional `PublishAot=true`, `bin/Debug/.../rtok.runtimeconfig.json` contains `System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported: false` and `System.Reflection.Metadata.MetadataUpdater.IsSupported: false`. As a result:
- hot reload is disabled;
- code paths that depend on dynamic code take their AOT branch even under JIT.

That is useful as an "AOT-like" test mode, but it is not the fast loop. If you want the AOT-like mode, add a `Configuration=DebugAot` that sets PublishAot.

### B.2 Fastest JIT build settings (Debug only)

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
  <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild> <!-- IDE still runs them; CI runs them -->
  <EnableNETAnalyzers>false</EnableNETAnalyzers>
  <GenerateDocumentationFile>false</GenerateDocumentationFile>
  <TieredPGO>false</TieredPGO>                  <!-- less JIT work in short runs -->
  <TieredCompilation>true</TieredCompilation>   <!-- default; keep quick tier-0 -->
  <PublishReadyToRun>false</PublishReadyToRun>
  <UseAppHost>true</UseAppHost>                  <!-- run bin/Debug/.../rtok directly -->
  <Deterministic>false</Deterministic>
  <ProduceReferenceAssembly>true</ProduceReferenceAssembly> <!-- default; skips dependents when only internals change -->
</PropertyGroup>
```

Measured on the fat project (about 12 source files, 5 packages) **[measured]**:

| Action | Time |
|---|---|
| no-op `dotnet build` | about 0.5 s |
| edit + `dotnet build` (warm build server) | about 0.55 s |
| warm full build | 0.98 s |
| `dotnet run` (with up-to-date check) | about 0.9 s |
| `dotnet run --no-build` | about 0.5 s |
| launching `bin/Debug/net10.0/fat` directly | 0.06 s |

At this size, `RunAnalyzers=false` made no measurable difference. It matters once the solution has many analyzer packages (Roslynator, Meziantou, StyleCop) **[unverified for rtok scale]**.

Other options:
- **ReadyToRun for Debug**: no. R2R slows the build. Use it only for a JIT release if you ever ship one.
- **`TieredCompilation` quick-jit for loops** (`TieredCompilationQuickJitForLoops`) is the default since .NET 7 and is fine.
- **Shared compilation**: the Roslyn compiler server (VBCSCompiler) and the MSBuild node reuse are on by default (`UseSharedCompilation=true`). Don't pass `-nodeReuse:false` locally. `dotnet build-server shutdown` resets them. [doc] https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build-server
- **`--no-restore`**: use `dotnet build --no-restore` after the first restore. `dotnet run --no-restore` is also valid. Restore is a measurable part of each no-op build.
- **Solution filter (`.slnf`) / `.slnx`**: build only `src/Rtok.Cli` and its references when iterating. .NET 10 defaults to the `.slnx` format. [doc] https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/sdk

### B.3 dotnet watch / hot reload

- `dotnet watch run` applies hot reload for supported edits. For rude edits, `DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1` restarts the app without asking. Other options: `--no-hot-reload`, `--non-interactive`, and `DefaultItemExcludes` / `<Watch Remove>` to ignore generated or embedded files. [doc] https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-watch
- Hot reload requires `MetadataUpdater.IsSupported=true`, so it **does not work if PublishAot is on in Debug** (see B.1).
- Hot reload has little value for a CLI that exits in milliseconds. It is worth using for the long-running parts: `rtok proxy`, `rtok mcp --http` and the dashboard.

### B.4 File-based apps (.NET 10)

- `dotnet run app.cs` supports the directives `#:package`, `#:property`, `#:sdk` and `#:project`. `#:include` arrived in SDK 10.0.300 / .NET 11 P3. **File-based apps default to PublishAot=true.** [doc] https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps
- .NET 11 caches the AOT build for file-based launches. [doc] dotnet-11 SDK notes [unverified detail]
- Use them for spikes, benchmarks and repo scripts (codegen, release helpers), not for the product.

### B.5 Directory.Build.props for the loop

See section E.2. Its Debug block holds the settings from B.2, so every project gets them.

### B.6 Test frameworks with NativeAOT

| Framework | Version | AOT story | Source |
|---|---|---|---|
| xUnit v3 | 4.0.2 (Apache-2.0) | Dedicated AOT packages (`xunit.v3.aot`, `xunit.v3.aot.mtp-v2`, `xunit.v3.assert.aot`); net9+, C# only. Limits: no generic theories, no `Assert.Equivalent`, etc. Runs on Microsoft.Testing.Platform (MTP). | [doc] https://xunit.net/docs/getting-started/v3/native-aot ; [nuget] |
| MSTest | 4.5.1 | AOT since 4.4 through `MSTest.Sdk` + PublishAot, with a source-generated reflection-free mode | [doc] https://devblogs.microsoft.com/dotnet/mstest-source-generation/ |
| TUnit | 1.76.0 | Source-generated, built on MTP, claims NativeAOT and trimmed single-file support | [doc] https://tunit.dev/ ; **details [unverified]** |

You already chose xUnit, so use **xUnit v3 on MTP** and set the test runner in global.json (E.3). Keep most tests on JIT, because it is fast and supports all of xUnit. Add one small `Rtok.AotTests` project that uses `xunit.v3.aot` and is published with AOT in CI, plus smoke tests that run the real AOT `rtok` and `rtok-hook` binaries through CliWrap (B.8). [doc] https://devblogs.microsoft.com/dotnet/testing-your-native-aot-dotnet-apps/ recommends this split: keep the managed lane, add a native lane and compare test counts.

### B.7 Supporting test packages [nuget]

| Rust dev dependency | .NET choice | Version |
|---|---|---|
| insta | Verify.XunitV3 | 33.3.4 |
| proptest | CsCheck (Apache-2.0) or FsCheck (BSD-3) | 4.9.1 (5.0.0-rc2 exists) / 3.5.0 |
| httpmock | WireMock.Net (Apache-2.0) | 2.19.0 |
| divan | BenchmarkDotNet | 0.15.8 |
| portable-pty | Porta.Pty | 2.2.3 (declares AOT-compatible) |
| assert_cmd / trycmd | CliWrap + Verify | 3.10.5 |
| tokio-tungstenite | `System.Net.WebSockets.ClientWebSocket` | BCL |
| pulldown-cmark | Markdig (BSD-2) | 1.4.2 |
| jsonschema | JsonSchema.Net | 9.4.1 (has trim warnings; fine in tests) |
| unicode-width | Wcwidth | 4.0.1 |

Also useful:
- Fuzzing: SharpFuzz 2.3.0.
- Mutation testing: dotnet-stryker 5.0.0.
- Coverage: Microsoft.Testing.Extensions.CodeCoverage 18.12.0 with MTP, or coverlet.collector 10.1.0. Reports: reportgenerator 5.5.11.

### B.8 Testing AOT in CI

```yaml
# per OS runner (macos-15, ubuntu-24.04, ubuntu-24.04-arm, windows-2025)
- run: dotnet build -c Release -warnaserror        # analyzers + IL warnings
- run: dotnet test -c Debug                        # managed lane (MTP)
- run: dotnet publish src/Rtok.Hook -c Release -r ${{ matrix.rid }}
- run: dotnet publish src/Rtok.Cli  -c Release -r ${{ matrix.rid }}
- run: dotnet publish tests/Rtok.AotTests -c Release -r ${{ matrix.rid }} && ./artifacts/.../Rtok.AotTests
- run: dotnet test tests/Rtok.Smoke -- --binary ./artifacts/publish/... # CliWrap runs real binaries
- run: hyperfine -N --warmup 20 --export-json hook.json "./rtok-hook < fixtures/pre_tool_use.json"   # fail if median > 10 ms
```

Gate the build on the ILC warning count staying at 0. Use `-warnaserror` with `IL*` in `WarningsAsErrors`.

---

## C. Rust dependency to .NET mapping

Notation:
- **AOT status** is one of: **declared** (the package sets `IsAotCompatible`), **clean** (I published it with the whole assembly rooted and got zero IL warnings), **runtime-ok** (I exercised it under AOT here), **warnings** (rooted publish showed IL2104/IL3053) or **fails**.
- **BCL** means it is built into .NET 10.
- Licenses are MIT unless stated.

### C.1 Workspace and shared crates

| Rust | .NET | Version | AOT | License / notes |
|---|---|---|---|---|
| serde + serde_json | **BCL** System.Text.Json + source gen | 10.0 | yes | `JsonSerializerContext` everywhere; `Utf8JsonReader`/`Writer` for the hook fast path |
| jsonc-parser (read) | **BCL** STJ `JsonCommentHandling.Skip` + `AllowTrailingCommas` | 10.0 | yes | |
| jsonc-parser **cst** (lossless edit in rtok-mcp) | **write own package** `Rtok.Jsonc` | n/a | n/a | No maintained lossless JSONC CST on NuGet. A token-preserving editor over `Utf8JsonReader` positions is a few hundred lines. |
| toml_edit (lossless edit) | **Tomlyn** `SyntaxParser` / `DocumentSyntax` (keeps trivia) | 2.10.1 | declared, clean | BSD-2-Clause. The README mentions TOML 1.1; **TOML 1.0 compliance and round-trip fidelity are [unverified]**. Test with a golden corpus. |
| toml 0.8 (serde) | Tomlyn `TomlSerializerContext` (source gen), or **CsToml** | 2.10.1 / 1.8.6 | declared, clean | CsToml is source-generated, TOML 1.0 by default with 1.1 optional; its comment preservation is [unverified]. Tomlet 6.2.0 is trim-only and shows warnings, so avoid it. |
| figment (layered config + provenance) | **write own** small layered merger (defaults < global TOML < project TOML < env), with a provenance map | n/a | n/a | M.E.Configuration + binder source gen is AOT-OK but loses per-key provenance, so own code is simpler. |
| dotenvy | **write own** (about 50 lines), or DotNetEnv 3.2.0 / dotenv.net 4.2.0 | | clean (not declared) | |
| schemars | **BCL** `System.Text.Json.Schema.JsonSchemaExporter` (.NET 9+) | 10.0 | yes, via `JsonTypeInfo` from the context | [doc] https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/extract-schema |
| thiserror / anyhow | **BCL** exception types, plus a `Result<T,E>`-style record in Rtok.Core | | yes | |
| regex | **BCL** `[GeneratedRegex]`; use `RegexOptions.NonBacktracking` for user-supplied patterns (linear time, like the Rust crate) | 10.0 | yes | .NET 11 adds `RegexOptions.AnyNewLine` [doc] dotnet-11 libraries |
| sha2 | **BCL** `SHA256.HashData` | | yes | |
| subtle | **BCL** `CryptographicOperations.FixedTimeEquals` | | yes | |
| uuid | **BCL** `Guid.CreateVersion7()` / `Guid.NewGuid()` | | yes | |
| humantime | **write own** parser/formatter (`TimeSpan`, about 80 lines). Humanizer.Core 3.0.10 is AOT-declared but heavy. | | | |
| globset | **BCL-adjacent** Microsoft.Extensions.FileSystemGlobbing | 10.0.13 | declared | DotNet.Glob 3.1.3 is faster but not declared AOT (rooted clean) |
| url | **BCL** `Uri` | | yes | |
| fs-kit, change-preview, operation-icons | **write own** packages (they are pyrlyn internals) | | | |

### C.2 rtok binary crates

| Rust | .NET | Version | AOT | Notes |
|---|---|---|---|---|
| clap (derive) | **ConsoleAppFramework** (chosen) | 5.7.13 | source generator, no reflection; **runtime-ok** [measured] | https://github.com/Cysharp/ConsoleAppFramework. Alternative: **System.CommandLine 2.0.13**, also runtime-ok and with a runtime command tree ([doc] https://learn.microsoft.com/en-us/dotnet/standard/commandline/). **Not** Spectre.Console.Cli, which fails under AOT [measured]. |
| shell completions | **write own**: static scripts from a command model parsed out of ConsoleAppFramework `--help` (plan T12.11) | | | |
| clap_mangen | **write own**: roff from the same command model (plan T12.11) | | | |
| shlex | **write own** (POSIX split/quote, about 100 lines) | | | |
| wasmi (optional plugin host) | **WACS** 0.16.14 (pure C#, Apache-2.0; trim-only, warnings) or **Wasmtime** 48.0.2 (native lib, Apache-2.0 WITH LLVM-exception; warnings) | | warnings | Leave it feature-gated. WACS is closer to wasmi (an interpreter, no native code). |
| zstd | **ZstdSharp.Port** now; **BCL `ZstandardStream`** in .NET 11 | 0.8.8 | rooted clean, not declared | [doc] https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/libraries |
| pathdiff | **BCL** `Path.GetRelativePath` | | yes | |
| rmcp (stdio + streamable HTTP) | **ModelContextProtocol** + `.AspNetCore` | 2.3.0 (Apache-2.0) | declared, **runtime-ok over HTTP** [measured] | Use `AddMcpServer().WithStdioServerTransport()` / `.WithHttpTransport()` and `MapMcp("/mcp")`. Initialize and tools/call worked under AOT. No `Mcp-Session-Id` was returned by default (the default looked stateless; check `HttpServerTransportOptions.Stateless` [unverified]). **Adds about 2.3 ms to process startup, so keep it out of the hook.** |
| ignore (gitignore-aware walk) | **BCL** `FileSystemEnumerable<T>` + **write own** `Rtok.Ignore` (gitignore semantics + .ignore + global excludes) | | | NuGet options exist: Ignore 0.2.1 and GitignoreParserNet 0.2.0.15, both rooted clean but small and with incomplete semantics. MAB.DotIgnore 3.0.2 is stale (2020). |
| same-file | **write own**: `[LibraryImport]` `stat`/`fstat` (dev + inode) on unix, `GetFileInformationByHandle` on Windows | | yes | |
| serde-saphyr (pnpm-workspace.yaml) | **SharpYaml** 3.16.0 (declared AOT, has a source generator) or **VYaml** 1.4.1 (source gen; rooted with warnings) | | SharpYaml declared | YamlDotNet 18.1.0 has warnings unless you use its static context. For a single small file, a minimal hand parser is also reasonable. |
| notify 9 | **BCL** `FileSystemWatcher` | | yes | It uses FSEvents on macOS, inotify on Linux and ReadDirectoryChangesW on Windows. Add your own debounce. Linux needs a high `max_user_watches` for large trees. |
| owo-colors | **Spectre.Console** markup/`AnsiConsole`, or plain ANSI. Pastel 8.0.0 is tiny but not declared AOT. | 0.57.2 | declared, clean | |
| printpdf | **QuestPDF** 2026.9.1 (declared AOT; native Skia/HarfBuzz/qpdf; own community/commercial license) or **PDFsharp** 6.5.0 (**AGPL-3.0-only** in 6.x; check before mixing with GPL-3.0-or-later) | | | Keep it in a separate optional command or plugin, because of the native libs and size. |
| tokio | **BCL** async/await, `Task`, `Channel<T>`, `PeriodicTimer` | | yes | .NET 11 runtime-async (`<Features>runtime-async=on</Features>`) is a preview opt-in [doc] |
| axum (+ ws) | **ASP.NET Core** `WebApplication.CreateSlimBuilder` / `CreateEmptyBuilder` + Minimal APIs + `UseWebSockets` | 10.0 | partial (Minimal APIs partial, WebSockets full; MVC/Blazor Server unsupported) | [doc] https://learn.microsoft.com/en-us/aspnet/core/fundamentals/native-aot . Adds about 1.5 ms of startup **when linked**, so keep it out of the hook. |
| rust-embed (React SPA) | **BCL** `EmbeddedResource` + Microsoft.Extensions.FileProviders.Embedded `ManifestEmbeddedFileProvider` + `UseStaticFiles` | 10.0.13 | declared | Embeds the published Blazor WebAssembly client (`Rtok.Web.Client`, N13) through an MSBuild target before compile. Only standalone WebAssembly works with the AOT host: Blazor Server and interactive SSR don't. |
| React UI kit (rtok SPA: TanStack Table, ECharts charts) | **Radzen.Blazor** (MIT) in `Rtok.Web.Client` (N13): layout, `RadzenDataGrid`, dialogs, notifications, `RadzenChart`, `RadzenSparkline`, `ThemeService` | 12.1.5 | not trim-annotated; WASM client only, never in the AOT `rtok` [measured] | In a trimmed Blazor WebAssembly publish it adds about 0.43 MB brotli (`Radzen.Blazor.wasm`) plus one theme CSS; it pulls Microsoft.Extensions.AI.Abstractions (2 KB). With `SuppressTrimAnalysisWarnings=false` it raises IL2091 in its generic dropdown/list and `QueryableExtension`, as the Blazor framework itself does (IL2072/IL2111), so keep the Blazor default and root `Rtok.Web.Contracts`. Under a strict CSP it needs `style-src-attr 'unsafe-inline'` and nothing more: it sets inline style attributes, injects no `<style>` elements [measured in a browser]. The package ships every theme; embed only the two used. |
| mime_guess | **BCL** `FileExtensionContentTypeProvider` (ASP.NET Core) | | yes | |
| percent-encoding | **BCL** `Uri.EscapeDataString`, `WebUtility.UrlEncode` | | yes | |
| futures-util | **BCL** `IAsyncEnumerable`, `System.Linq.AsyncEnumerable` (in the BCL since .NET 10) | | yes | |
| ratatui + crossterm | **Terminal.Gui** v2 (your choice), or XenoAtom.Terminal.UI 3.10.0 (BSD-2) | 2.5.0 | declared, rooted clean | **Interactive behavior under AOT is [unverified]**; only the publish was tested. |
| inquire | **Spectre.Console** prompts (`SelectionPrompt`, `TextPrompt`, `ConfirmationPrompt`) | 0.57.2 | declared | |
| indicatif (rtok-sys) | Spectre.Console `Status` / `Progress` | 0.57.2 | declared | |
| reqwest + rustls + webpki-roots + rustls-pemfile | **BCL** `HttpClient` / `SocketsHttpHandler`, `SslStream`, `X509Certificate2.CreateFromPem` | | yes | Uses OS TLS: SecureTransport/Network on macOS, SChannel on Windows, OpenSSL on Linux, which must be present at runtime. The "blocking" feature is `HttpClient.Send` (sync). |
| HTTP reverse proxy (rtok proxy) | **Yarp.ReverseProxy** `AddHttpForwarder` + `MapForwarder`, or a hand-rolled `HttpClient` pass-through | 2.3.0 | trim-declared, rooted clean, **runtime-ok with SSE streaming** [measured] | No official AOT statement from YARP [unverified]. For rewriting requests and responses (token reduction), a custom `HttpTransformer` or a hand-written forwarder gives more control. SSE: `System.Net.ServerSentEvents` `SseParser` (client) and `TypedResults.ServerSentEvents` (server, .NET 10) [measured]. |
| tree-sitter + tags + 13 grammars | **write own package `Rtok.TreeSitter`**: `[LibraryImport]` over the tree-sitter C API, with grammars statically linked through `NativeLibrary` + `DirectPInvoke` | n/a | yes (own) | TreeSitter.DotNet 1.3.0 works under AOT [measured] but loads per-grammar dylibs (62 MB for osx-arm64), lacks **Dart, Kotlin and Swift**, and does not declare AOT. Building the C sources with `zig cc` or clang per RID in CI gives one static `.a` set. |
| tiktoken-rs (o200k_base) | **Microsoft.ML.Tokenizers** + `.Data.O200kBase` | 2.0.1 (3.0.0-preview available) | not declared; rooted warnings, but **runtime-ok with zero ML warnings in real use** [measured] | `TiktokenTokenizer.CreateForModel("gpt-4o")` gave 11 tokens, matching SharpToken 2.0.6 and Tiktoken 3.1.5 (tryAGI), which also work. |
| Claude token counting | **no public local tokenizer**. Use the `POST /v1/messages/count_tokens` API through the official `Anthropic` NuGet, or raw `HttpClient` + STJ | | | **Free**, with a separate rate limit: 5,000 / 10,000 / 20,000 RPM for the Start / Build / Scale tiers. The count is an estimate. Claude 4.7+ models count about 30% more tokens than older ones. [doc] https://platform.claude.com/docs/en/build-with-claude/token-counting . The official **Anthropic** NuGet 13.0.0 (Apache-2.0, net9 lib) shows IL2104/IL3053 when rooted [measured], so it is **not AOT-clean**. For the AOT binary, call the endpoint with a small STJ-source-gen client, and keep a local estimate (o200k × calibrated factor) for offline use. |
| semver | **Semver** 3.0.0 | | rooted clean | |
| uuid / humantime | see C.1 | | | |
| trash 5 | **write own** `Rtok.Trash`: macOS `NSFileManager trashItemAtURL` via objc_msgSend P/Invoke (or `osascript` fallback); Linux the FreeDesktop trash spec (`~/.local/share/Trash`, `.trashinfo`); Windows `IFileOperation` / `SHFileOperation` with `FOF_ALLOWUNDO` | | | |
| jiff (IANA tz) | **BCL** `TimeZoneInfo` (IANA ids on all OSes since .NET 6), `DateTimeOffset`, `TimeProvider` | | yes | NodaTime 3.3.5 if you need the full calendar arithmetic; trim-only declared, rooted clean |
| resvg (SVG to PNG) | **ResvgSharp** 1.0.3 (native resvg, rooted clean) or **Svg.Skia** 5.2.3 + SkiaSharp (declared AOT, native Skia) | | | Both are native, so make it an optional command. |
| base64 | **BCL** `Convert.ToBase64String`, `System.Buffers.Text.Base64Url` (.NET 9+) | | yes | |
| log + env_logger (rtok-log) | **BCL** `Microsoft.Extensions.Logging` + `[LoggerMessage]`; or for the hook a **hand-rolled append-only writer** (no DI/host) | 10.0.13 | declared | ZLogger 2.5.10 (trim-declared, source-gen, zero-alloc) and Serilog 4.4.0 (declared) are also fine |
| OpenTelemetry | **OpenTelemetry** + `.Exporter.OpenTelemetryProtocol` + `.Extensions.Hosting` | 1.20.1 (Apache-2.0) | declared, runtime-ok (OTLP builds) [measured] | Uses BCL `ActivitySource` / `Meter`. Keep it out of the hook. |
| git | **LibGit2Sharp** for reads and ordinary operations; **shell out to `git`** through ProgressRunner for `worktree add/remove/list` (N21, creator decision) | 0.32.0 | to prove (native libgit2) | LibGit2Sharp has no worktree add/remove parity. GitReader 1.18.0 is read-only. |
| diffing | **DiffPlex** 1.9.0 (Apache-2.0) | | rooted clean, not declared | Or write your own Myers/patience diff (about 300 lines) if you need unified-diff output identical to the Rust tool. |
| shell output filtering | **BCL** GeneratedRegex + spans | | yes | |
| unix-socket daemon | **BCL** `Socket(AddressFamily.Unix, ...)` + `UnixDomainSocketEndPoint`; Windows 10+ also supports AF_UNIX. Kestrel can `ListenUnixSocket`. | | yes | |
| WebSocket dashboard | **BCL** ASP.NET Core `UseWebSockets` | | yes [measured echo] | |
| SQLite (Diesel + libsqlite3-sys bundled, FTS5) | **Microsoft.Data.Sqlite** 10.0.13 + SQLitePCLRaw 3.0.5 (Apache-2.0). Typed queries: **SQLite.Framework** 7.1.1 + SourceGenerator (runtime-ok under AOT [measured]) or **Dapper.AOT** 1.1.0. Migrations: **write own** (an ordered embedded `.sql` list + `PRAGMA user_version`, mirroring diesel_migrations). | | Microsoft.Data.Sqlite rooted clean, **runtime-ok** [measured] | See C.4 for EF Core and C.5 for static linking. |
| sqlite FTS5 | Included in the `e_sqlite3` build that SQLitePCLRaw bundles [doc] https://github.com/ericsink/SQLitePCL.raw | | | |

### C.3 Other areas checked

| Area | Choice | Version | AOT |
|---|---|---|---|
| fuzzy matching | Raffinert.FuzzySharp 6.0.1 / Fastenshtein 1.0.12 (declared) | | clean |
| templating (if needed) | Fluid.Core 2.40.0 (warnings), Scriban 7.5.1 (dynamic, warnings). Prefer own string templates. | | warnings |
| markdown | Markdig 1.4.2 (declared) | | clean |
| syntax highlighting | TextMateSharp 2.0.4 / ColorCode.Core 2.0.15 (rooted clean, not declared) | | clean |
| retries | Polly.Core 8.8.0 (BSD-3, trim-declared) | | clean |
| streams/pipes | Nerdbank.Streams 2.14.408, BCL `System.IO.Pipelines` | | clean |
| hashing (xxhash/crc) | System.IO.Hashing 10.0.13 (declared) | | clean |
| DI | none on the hook; MS.DI or Jab 0.12.0 / Pure.DI 2.5.5 in the main CLI | | clean |
| Reactive | System.Reactive 7.0.0 shows warnings; avoid it | | warnings |

### C.4 EF Core under NativeAOT (the creator's choice for the store)

Docs: EF Core 10 supports NativeAOT only with a **compiled model and precompiled queries**,
and Microsoft labels it **"highly experimental"**. Dynamic queries and LINQ shapes the
precompiler cannot see fail at runtime.
[doc] https://learn.microsoft.com/en-us/ef/core/performance/nativeaot-and-precompiled-queries

Probe, 2026-10-21, M3 Max, SDK 10.0.402, EF Core 10.0.13, `hyperfine -N`, 50 runs **[measured]**.
The probe opens a SQLite file, inserts one row through `SaveChanges` and runs one `Count`
query.

| Binary | Size | Wall time |
|---|---|---|
| EF Core + SQLite, AOT, compiled model + precompiled queries | 18.3 MB + libe_sqlite3.dylib 1.7 MB | **16.9 to 17.2 ms** |
| Microsoft.Data.Sqlite only, AOT, same work | 2.7 MB + libe_sqlite3.dylib 1.7 MB | **7.7 ms** |
| STJ-only hook (no store), AOT | 2.3 MB | 4.7 ms (`echo` is 3.2 ms) |
| same hook, JIT apphost (Debug) | n/a | 41.9 ms |

What the probe found:
- **The compiled model and precompiled queries work under AOT.** Setup: reference
  `Microsoft.EntityFrameworkCore.Tasks` (it regenerates the model and the query interceptors
  at publish when `PublishAot` is on) and set
  `<InterceptorsNamespaces>$(InterceptorsNamespaces);Microsoft.EntityFrameworkCore.GeneratedInterceptors</InterceptorsNamespaces>`
  for every configuration, not only Release. The generated
  `[assembly: DbContextModel(...)]` attribute makes EF pick the model up with no
  `UseModel` call.
- **Interceptors are bound to source locations.** A hand-run `dotnet ef dbcontext optimize`
  output goes stale after any edit to a file with queries (CS9143). Let the Tasks package
  regenerate it on every publish; never commit the generated query files.
- **`Database.Migrate()` and `EnsureCreated()` throw under AOT**: "Design-time DbContext
  operations are not supported when publishing with NativeAOT". The build also warns with
  IL3050.
- **`dotnet ef migrations script --idempotent` is not supported for SQLite.** A plain
  per-migration script works (`dotnet ef migrations script <from> <to>`): it creates
  `__EFMigrationsHistory`, wraps the change in a transaction and records the migration id.
- So the AOT binary applies migrations with its own small runner: EF-generated SQL scripts,
  one per migration, embedded as resources, applied in order inside a transaction, with
  `__EFMigrationsHistory` as the source of truth. The SQL is generated, never hand-written.
- EF Core costs about 9 ms more than Microsoft.Data.Sqlite per cold process, so the
  10 ms hook budget cannot include a cold EF context. Keep EF out of `rtok-hook` and let the
  resident hook server (long-lived) own the warm `DbContext`.
- EF's own assemblies report IL2104/IL3053 (trim/AOT warnings in the package) at publish. The
  app's warnings can still be errors; list those three assemblies as known exceptions and
  gate on the app's own warnings plus the AOT smoke tests.

Lower-risk alternatives, if EF under AOT turns out unworkable: Microsoft.Data.Sqlite with
SQLite.Framework 7.1.1 + its source generator (LINQ joins and group-by worked under AOT
[measured]) or Dapper.AOT 1.1.0.

### C.5 SQLite native linking

- SQLitePCLRaw v3: `bundle_e_sqlite3` is now `config.e_sqlite3` + `SourceGear.sqlite3`, and v3 dropped several bundles (e_sqlite3mc and others). [doc] https://github.com/ericsink/SQLitePCL.raw/blob/main/v3.md
- SourceGear.sqlite3 3.53.3 ships a **static `libe_sqlite3.a` for linux-x64/arm64/musl and iOS**, but only a dylib for macOS and a DLL for Windows [nuget, inspected]. So:
  - On Linux, use `<DirectPInvoke Include="e_sqlite3" />` + `<NativeLibrary Include="...libe_sqlite3.a" />` to get one binary.
  - On macOS and Windows, either ship the dylib/DLL next to the exe, or compile the amalgamation (with FTS5) to a `.a`/`.lib` in CI [unverified]. StaticLink.e_sqlite3 2.1.10.1 (third-party) exists: https://www.nuget.org/packages/StaticLink.e_sqlite3 [unverified for v3].

---

## D. Startup strategy for the 10 ms hook

1. **Separate binary `rtok-hook`** (`src/Rtok.Hook`). AOT, `InvariantGlobalization`, `UseSystemResourceKeys`, `EventSourceSupport=false`, no FrameworkReference to ASP.NET. It references only a tiny `Rtok.Hook.Contracts` project (JSON contracts + STJ context) and the fast-path filter rules. Measured at 5.2 to 5.5 ms [measured].
2. **No Generic Host, DI, `IConfiguration` or ILogger on the hook path.** Top-level `Main`:
   - read stdin into a pooled buffer;
   - parse it with `Utf8JsonReader` or the source-gen context;
   - decide;
   - write the result with `Utf8JsonWriter` to `Console.OpenStandardOutput()`;
   - return.
3. **Fail open.** Wrap everything in `try/catch (Exception)` and return 0 with an "allow" decision. Add `AppDomain.CurrentDomain.UnhandledException` as a backstop.
   - A NativeAOT stack overflow or failfast still kills the process, which matches the Rust rule against `panic=abort`. The caller (the agent) must also treat a crash as allow.
   - Set a hard internal deadline: a `CancellationTokenSource(TimeSpan.FromMilliseconds(8))` around any I/O.
4. **Daemon fallback for heavy work.** When the hook needs the store, graph or tokenizer:
   - connect to the resident `rtok daemon` over a unix socket (`$XDG_RUNTIME_DIR/rtok.sock`, or a named pipe on Windows) with a connect plus read timeout of about 3 ms;
   - send a compact request (length-prefixed JSON);
   - on timeout or connection refused, **fail open** and optionally spawn the daemon detached for next time.

   This keeps the hook binary free of SQLite, ASP.NET and MCP.
5. **Avoid static-init cost.**
   - No large `static readonly` tables. Regexes are `[GeneratedRegex]`, which construct lazily.
   - Don't touch `Console.Out` with encoding setup; write bytes.
   - Don't read config TOML on every call. Have the daemon or the installer precompute a small binary or JSON snapshot keyed by mtime.
6. **Measure in CI** with hyperfine on each OS. Fail if the median exceeds 10 ms or regresses by more than 20%.
7. **The main `rtok` binary** can be fat (the CLI parser, Kestrel, MCP, SQLite, Spectre, Terminal.Gui). Its `--help` takes about 9 ms and a SQLite command about 11 ms [measured], which is fine for interactive commands.

---

## E. Solution layout and repo config

### E.1 Layout

```text
rtok-net/
  global.json
  Directory.Build.props
  Directory.Build.targets
  Directory.Packages.props
  .editorconfig
  rtok.slnx
  src/
    Rtok.Cli/            # rtok exe: ConsoleAppFramework, Spectre, Terminal.Gui, Kestrel, MCP, proxy (AOT)
    Rtok.Hook/           # rtok-hook exe: tiny, AOT, no ASP.NET (D)
    Rtok.Hook.Contracts/ # hook JSON DTOs + JsonSerializerContext (shared by Cli + Hook)
    Rtok.Core/           # filters, plugins, config model, regex rules
    Rtok.Config/         # layered TOML (Tomlyn) + env + provenance
    Rtok.Store/          # SQLite (Microsoft.Data.Sqlite + SQLite.Framework or EF compiled model), migrations
    Rtok.Mcp/            # MCP tools (ModelContextProtocol), stdio + HTTP
    Rtok.Proxy/          # YARP/HttpClient forwarder, SSE rewrite
    Rtok.Graph/          # code graph over Rtok.TreeSitter
    Rtok.Daemon/         # unix-socket server
    Rtok.Web/            # dashboard endpoints + embedded Blazor WebAssembly client
    Rtok.Web.Client/     # Blazor WebAssembly dashboard (N13)
  packages/              # reusable, separately versioned, IsAotCompatible=true
    Rtok.TreeSitter/     # LibraryImport bindings + static grammars per RID
    Rtok.Ignore/         # gitignore semantics + walker
    Rtok.Jsonc/          # lossless JSONC edit
    Rtok.FsKit/  Rtok.ChangePreview/  Rtok.OperationIcons/  Rtok.Trash/  Rtok.Shlex/
  tests/
    Rtok.Core.Tests/ ... (xUnit v3 on MTP, JIT)
    Rtok.AotTests/       # xunit.v3.aot, published + run in CI
    Rtok.Smoke/          # CliWrap against published binaries
    Rtok.Benchmarks/     # BenchmarkDotNet
  web/                   # dashboard screenshots
```

### E.2 Directory.Build.props

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <InvariantGlobalization>true</InvariantGlobalization>
    <IsAotCompatible>true</IsAotCompatible>
    <ArtifactsPath>$(MSBuildThisFileDirectory)artifacts</ArtifactsPath> <!-- .NET 8+ artifacts layout -->
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <TreatWarningsAsErrors Condition="'$(CI)' == 'true'">true</TreatWarningsAsErrors>
    <WarningsAsErrors>$(WarningsAsErrors);IL2026;IL2104;IL3050;IL3053;IL3058;nullable</WarningsAsErrors>
    <Authors>listepo</Authors>
    <PackageLicenseExpression>GPL-3.0-or-later</PackageLicenseExpression>
  </PropertyGroup>

  <!-- Exes: AOT only in Release (B.1) -->
  <PropertyGroup Condition="'$(OutputType)' == 'Exe' and '$(Configuration)' == 'Release'">
    <PublishAot>true</PublishAot>
    <UseSystemResourceKeys>true</UseSystemResourceKeys>
    <StackTraceSupport>true</StackTraceSupport>
  </PropertyGroup>

  <!-- Fast JIT inner loop (B.2) -->
  <PropertyGroup Condition="'$(Configuration)' == 'Debug'">
    <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>
    <EnforceCodeStyleInBuild>false</EnforceCodeStyleInBuild>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
    <TieredPGO>false</TieredPGO>
    <Deterministic>false</Deterministic>
  </PropertyGroup>

  <ItemGroup Condition="'$(IsTestProject)' != 'true'">
    <PackageReference Include="Microsoft.SourceLink.GitHub" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

Notes:
- Note that `OutputType` is often set in the project file after `Directory.Build.props` is imported. Put the Exe/AOT block in **`Directory.Build.targets`**, or set `PublishAot` in each exe's csproj.
- `Rtok.Hook.csproj` additionally sets `EventSourceSupport=false` and `OptimizationPreference` at the default (blended).

### E.3 global.json

```json
{
  "sdk": { "version": "10.0.402", "rollForward": "latestFeature", "allowPrerelease": false },
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

The `test.runner` key opts `dotnet test` into MTP (.NET 10 SDK). [doc] https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/sdk

### E.4 Directory.Packages.props (Central Package Management)

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="ConsoleAppFramework" Version="5.7.13" />
    <PackageVersion Include="Spectre.Console" Version="0.57.2" />
    <PackageVersion Include="Terminal.Gui" Version="2.5.0" />
    <PackageVersion Include="Tomlyn" Version="2.10.1" />
    <PackageVersion Include="Microsoft.Data.Sqlite" Version="10.0.13" />
    <PackageVersion Include="SQLitePCLRaw.bundle_e_sqlite3" Version="3.0.5" />
    <PackageVersion Include="SQLite.Framework" Version="7.1.1" />
    <PackageVersion Include="SQLite.Framework.SourceGenerator" Version="7.1.1" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.13" /> <!-- only if EF kept (C.4) -->
    <PackageVersion Include="ModelContextProtocol" Version="2.3.0" />
    <PackageVersion Include="ModelContextProtocol.AspNetCore" Version="2.3.0" />
    <PackageVersion Include="Yarp.ReverseProxy" Version="2.3.0" />
    <PackageVersion Include="Microsoft.ML.Tokenizers" Version="2.0.1" />
    <PackageVersion Include="Microsoft.ML.Tokenizers.Data.O200kBase" Version="2.0.1" />
    <PackageVersion Include="ZstdSharp.Port" Version="0.8.8" />
    <PackageVersion Include="DiffPlex" Version="1.9.0" />
    <PackageVersion Include="CliWrap" Version="3.10.5" />
    <PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.20.1" />
    <PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.20.1" />
    <PackageVersion Include="Microsoft.Extensions.FileSystemGlobbing" Version="10.0.13" />
    <PackageVersion Include="Microsoft.Extensions.FileProviders.Embedded" Version="10.0.13" />
    <PackageVersion Include="SharpYaml" Version="3.16.0" />
    <PackageVersion Include="Semver" Version="3.0.0" />
    <PackageVersion Include="Microsoft.SourceLink.GitHub" Version="10.0.402" />
    <!-- tests -->
    <PackageVersion Include="xunit.v3" Version="4.0.2" />
    <PackageVersion Include="xunit.v3.aot" Version="4.0.2" /> <!-- exact id/version: check https://xunit.net/docs/getting-started/v3/native-aot [unverified id] -->
    <PackageVersion Include="Verify.XunitV3" Version="33.3.4" />
    <PackageVersion Include="CsCheck" Version="4.9.1" />
    <PackageVersion Include="WireMock.Net" Version="2.19.0" />
    <PackageVersion Include="Porta.Pty" Version="2.2.3" />
    <PackageVersion Include="BenchmarkDotNet" Version="0.15.8" />
    <PackageVersion Include="Microsoft.Testing.Extensions.CodeCoverage" Version="18.12.0" />
  </ItemGroup>
  <ItemGroup>
    <!-- analyzers for every project -->
    <GlobalPackageReference Include="Roslynator.Analyzers" Version="5.0.1" />
    <GlobalPackageReference Include="Meziantou.Analyzer" Version="3.0.315" />
    <GlobalPackageReference Include="Microsoft.VisualStudio.Threading.Analyzers" Version="18.12.1" />
  </ItemGroup>
</Project>
```

- The .NET 10 SDK prunes framework-provided packages and warns with NU1510 about redundant references. [doc] https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/sdk
- Optional analyzers: SonarAnalyzer.CSharp 10.35.0.4138, StyleCop.Analyzers 1.3.149-beta (the 1.1.118 stable is old) and IDisposableAnalyzers 4.0.8 (2024, stale) [nuget].

### E.5 .editorconfig and formatting

- Use `.editorconfig` with `root = true`:
  - `dotnet_analyzer_diagnostic.category-Performance.severity = warning`
  - `dotnet_diagnostic.CA2007.severity = none` (no ConfigureAwait in an app)
  - `dotnet_diagnostic.IDE0005.severity = warning` (requires `GenerateDocumentationFile` in CI for IDE0005 to fire)
  - `csharp_style_namespace_declarations = file_scoped:warning`
- Formatter: **CSharpier 1.3.0** as a local tool (`dotnet tool install csharpier`, run `dotnet csharpier check .` in CI) for zero-config, rustfmt-like formatting. Or use `dotnet format --verify-no-changes` with `.editorconfig` alone. Running both is redundant; pick CSharpier for whitespace and keep analyzers for semantics.
- `.config/dotnet-tools.json` lists csharpier, dotnet-stryker 5.0.0, reportgenerator 5.5.11 and dotnet-releaser 0.24.0.
- Versioning: Nerdbank.GitVersioning 3.10.94 or GitVersion.MsBuild 6.8.2. An explicit `<Version>` in Directory.Build.props is also fine, since Cargo.toml is hand-versioned.
- .NET 10 SDK: RID-specific and AOT **.NET tools** plus `dnx` make `dnx rtok` a possible distribution channel in addition to GitHub releases. [doc] https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/sdk

---

## F. T4: hook latency and the store on NativeAOT (2026-10-22)

Measured 2026-10-22 **[measured]** with `spikes/T4/bench.sh` (`hyperfine -N`, 20 warmups, 300 runs, stdin
= a 424-byte Claude PreToolUse payload). Linux runs in `spikes/T4/bench-linux.sh`: the
`mcr.microsoft.com/dotnet/sdk:10.0-noble-aot` container (SDK 10.0.402) in a colima VZ VM (4 vCPU,
Ubuntu kernel 6.8, aarch64) on the same M3 Max, with the store on a Docker volume (the container
overlay made fsync slow: the first Linux run, on the overlay, showed 14 ms for the resident's warm
write). Windows had no host and moved to T52.

macOS ran while five other lanes were building (load average 11 to 31 on 16 cores), so its p95
column is inflated; the p50 column matches an earlier run at load 9 within 0.5 ms. Linux p95 is
clean.

Probes (`spikes/T4/`): `rtok-hook` is the real `src/Rtok.Hook` (prints `{}`); `hook-client` is
`rtok-hook` as T14 will build it (Unix socket, one length-prefixed frame, 50 ms timeouts,
`posix_spawn`/`execv` by P/Invoke); `rtok-probe` is the full binary's shape: ConsoleAppFramework,
Spectre.Console, Tomlyn (source-generated, user + project layer), EF Core with a compiled model and
precompiled queries, Kestrel and the MCP SDK; `ef-probe` and `sqlite-probe` open a WAL store, insert
one calls row and count by event.

| Probe | macOS p50 | macOS p95 | Linux p50 | Linux p95 |
|---|---|---|---|---|
| process floor (`true`) | 3.22 | 3.71 | 0.15 | 0.21 |
| empty `rtok-hook` | 4.63 | 5.09 | 0.59 | 0.76 |
| `rtok-hook` + socket round trip, resident replies at once | 6.49 | 26.88 | 0.92 | 1.10 |
| `rtok-hook` + socket round trip, resident writes a calls row (warm EF) | 7.96 | 17.85 | 3.46 | 5.76 |
| full `rtok hook` + config load | 11.86 | 21.24 | 7.60 | 8.62 |
| full `rtok hook` + config load + EF store (cold) | 23.13 | 65.72 | 19.17 | 22.25 |
| EF Core compiled model: open, insert, query | 18.73 | 37.47 | 15.34 | 18.54 |
| Microsoft.Data.Sqlite: open, insert, query | 8.79 | 19.36 | 3.29 | 5.42 |
| same, SQLite linked statically (StaticLink.e_sqlite3) | 6.94 | 9.36 | 2.97 | 5.25 |
| same, SQLite linked statically (SQLitePCLRaw 3 + SourceGear.sqlite3 `.a`) | n/a | n/a | 2.43 | 3.32 |
| fallback a: resident down, no-op reply + detached spawn | 5.75 | 10.67 | 2.74 | 4.02 |
| fallback a + the request queued in the run dir for the resident | 5.67 | 7.94 | 1.95 | 3.83 |
| fallback b: resident down, `execv` the full hook with the store | 29.63 | 58.88 | 20.18 | 22.52 |

All times in ms, start to exit.

Static init and module initializers of the full binary. `hook-client` spawns each binary, and the
child logs the monotonic time when its `Main` starts:

| Spawned binary | Size | exec→Main p50 | exec→Main p95 | spawn→exit p50 |
|---|---|---|---|---|
| macOS `hook-client` | 2.1 MB | 4.46 | 5.99 | 5.06 |
| macOS `rtok-probe` | 30.5 MB | 10.21 | 11.58 | 11.08 |
| Linux `hook-client` | 2.2 MB | 0.59 | 0.71 | 0.70 |
| Linux `rtok-probe` | 32.0 MB | 6.75 | 7.44 | 7.45 |

- The full binary pays **6 to 6.5 ms before `Main`** on both OSes, and its hook work after `Main`
  (TOML load, JSON parse, reply) is under 1 ms. That cost is the image, not initializers: the ILC
  map lists **one** module initializer (`__module_initializers` is 4 bytes), while `__DATA` is
  14.1 MB with 10.5k dyld fixups against 0.4 MB and 0.8k for `hook-client`. So the full `rtok hook`
  misses the 10 ms budget on macOS even without the store, and every feature linked into `rtok`
  adds to it. The hook path must not start `rtok`.
- A cold EF context costs 12 to 15 ms over plain Microsoft.Data.Sqlite. Warm in the resident, the
  same insert and query take 1 to 3 ms.
- Sizes: `rtok-hook` 1.1 MB (macOS) / 1.2 MB (Linux); `ef-probe` 16.9 / 17.8 MB; `rtok-probe`
  30.5 / 32.0 MB; `sqlite-probe` 2.7 / 2.9 MB plus a 1.7 MB native SQLite, or 4.2 / 4.4 MB
  with SQLite linked in.

SQLite linked into the binary:
- **macOS**: SourceGear.sqlite3 (3.53.3 and 3.53.4) ships only a dylib. StaticLink.e_sqlite3
  2.1.10.1 ships `libe_sqlite3.a` for osx-arm64/x64, linux(-musl)-arm64/x64 and win-arm64/x64/x86
  (`e_sqlite3.lib`) and wires `DirectPInvoke` + `NativeLibrary` itself. With it the binary runs with
  no dylib beside it, EF Core included, SQLite 3.46.1 with FTS5 working. Its nuspec names
  "StaticLink.e_sqlite3 contributors", points `projectUrl` at SQLitePCL.raw and has no repository:
  the provenance of its prebuilt `.a` files is unverified.
- **Linux**: both work. SourceGear.sqlite3 **3.53.3** ships `libe_sqlite3.a` for linux-x64/arm64
  (3.53.4 dropped it), and with SQLitePCLRaw 3.0.5 (`SQLitePCLRaw.config.e_sqlite3` +
  `Microsoft.Data.Sqlite.Core`), `<DirectPInvoke Include="e_sqlite3" />`, the `.a` as
  `NativeLibrary` and `-lm` it links SQLite 3.53.3 with FTS5. StaticLink.e_sqlite3 works too.
- **Windows**: not measured (T52).
- `rtok-hook` links no SQLite at all (N6), so "both binaries" means `rtok`, which is also the
  resident.

EF Core under AOT, found while building the probes (adds to C.4):
- The query precompiler (EF 10.0.13) takes only locals. A context or a captured value that is a
  method parameter fails the publish with "Dynamic LINQ queries are not supported when
  precompiling queries" or `UnreachableException: IdentifierName of type ParameterSymbol`. Copy
  parameters into locals before the query. A query inside the `DbContext` class fails the same way.
- The generated interceptors use `?` outside a `#nullable` context (CS8669), which
  `WarningsAsErrors=nullable` turns into an error: the store project needs `NoWarn` CS8669.
- Without an `IDesignTimeDbContextFactory`, the Tasks package runs the app's entry point at publish
  to find a host.

More from T8 (the real store, EF 10.0.13; `src/Rtok.Store/StoreHost.targets` holds the wiring):
- **The compiled model is scaffolded only in the context's own assembly**, and EF finds it only
  there. With the context in `Rtok.Store.Model` and the queries in `Rtok.Store`, the model project
  needs its own Tasks reference, and the executable must reference both projects directly:
  `_EFPrepareDependenciesForPublishAOT` optimizes direct references only. Build-stage
  generation (`EFScaffoldModelStage=build`) is no way out: the task's "NativeAOT support is
  experimental" warning has no code, so `-warnaserror` fails every Release build. The same
  warning fails a `-warnaserror` publish, so `eng/publish.sh` passes `TreatWarningsAsErrors=true`
  instead, and StoreHost.targets keeps IL2104, IL3053 and IL3002 (EF Sqlite's SpatialiteLoader
  reads `DependencyContext`, which single-warn does not fold) as warnings.
- ILC flags the scaffolded model's `byte[]` comparers (IL3050, `Array.CreateInstance` in
  expression trees, which run interpreted). `TrimmerSingleWarn` on the model assembly folds them
  into one IL3053, like EF's own assemblies.
- A list parameter (`ids.Contains(x.Id)`) precompiles, but at run time EF builds its JSON reader
  (`JsonCollectionOfStructsReaderWriter<,>`) by reflection, which NativeAOT lacks. Bound a batch
  by its highest id instead.
- A query that is not ended by an operator the precompiler knows (`ToList`, `First`, `Any`, ...)
  is not precompiled and throws at run time: a collection spread `[.. query]` enumerates it unseen.
- `ExecuteUpdate` with several setters: the generated interceptor reads the captured values in
  the reverse order of the `SetProperty` calls, so two different values swap or fail a cast.
  Keep one captured value per `ExecuteUpdate` (or only nulls in the same lambda form). A constant
  value fails the precompile (`SqlConstantExpression.Quote`), as does a block-bodied setter lambda.
- A value-type value in `SetProperty(prop, value)` fails the precompile; use `_ => value` with a
  nullable local for a nullable column. `AsNoTracking()` and anonymous-type projections generate
  code that does not compile; tracked materialization needs non-sealed entity classes.
- The generated files also raise CS0184, CS0618 and CS9270; `.editorconfig` silences them for
  `*.EFInterceptors.*.g.cs`.

---

## G. T27: proxy overhead (2026-10-22)

Measured 2026-10-22 **[measured]** with `ProxyBenchmarks` in `tests/Rtok.Benchmarks`
(`dotnet run --project tests/Rtok.Benchmarks -c Release -- --filter '*Proxy*' --job short`;
BenchmarkDotNet 0.15.8, JIT, .NET 10.0.13, Apple M3 Max, macOS 27.0.1). One call posts rtok's
40 KB Anthropic Messages fixture (`tests/fixtures/proxy/messages.json`) and reads the streamed
answer (`anthropic_messages_stream.json`) from a WireMock upstream in the same process: straight
to WireMock, through the proxy in plain mode, and through the recording proxy, which also writes
the call, `call_io`, `usage` and `tokens` rows to a real store.

The machine ran other lanes' builds (load average about 140 on 16 cores), so the times are noisy
(the 99.9% error is larger than the mean); the allocations are stable. Allocated counts the
client, the proxy and WireMock together.

| Route | Mean | StdDev | Allocated |
|---|---|---|---|
| direct | 4.6 ms | 2.5 ms | 1.28 MB |
| plain proxy | 11.5 ms | 2.4 ms | 1.45 MB |
| recording proxy | 14.8 ms | 4.5 ms | 2.27 MB |

The plain hop costs about 0.17 MB a call; recording adds about 0.8 MB more (the 40 KB request
read whole for usage and `call_io`, the tee, JSON parsing and the EF rows). Rerun on an idle
machine before quoting the times.

---

## Unverified / open items

- Startup numbers on Windows (T52; macOS and Linux are in F).
- Terminal.Gui v2 and XenoAtom interactive behavior under AOT (only a rooted publish was clean).
- Tomlyn: TOML 1.0 compliance and lossless round-trip on real rtok configs. CsToml comment preservation.
- Static SQLite on Windows (T52), and where its `.a`/`.lib` should come from (F: StaticLink.e_sqlite3 has unverified provenance).
- YARP has no official AOT statement; it was rooted clean and SSE was runtime-tested here.
- TUnit AOT details; the exact xunit.v3 AOT package ids.
- The MCP SDK stateless/session default over HTTP.
- `EventSourceSupport=false` interaction with OpenTelemetry metrics.
