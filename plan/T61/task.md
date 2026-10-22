---
status: in progress
priority: P2
complexity: 1
readiness: 100%
agent: Claude Code / claude-opus-5-5
---

# T61. Fuzz publish keeps the whole graph JIT

`--target=fuzz` (T53, T57) stops at `dotnet publish tests/Rtok.Fuzz -c Release` with
NETSDK1191 in `src/Rtok.Cli`: publishing reaches the referenced executables, and in Release
`Directory.Build.targets` gives Rtok.Cli `PublishAot`, which then needs a RID. Rtok.Fuzz is
`RtokJitOnly`, but the property is its own, not its references'.

Done: the fuzz task publishes with `-p:RtokJitOnly=true`, so no project in the graph turns on
AOT; the publish succeeds and `dotnet sharpfuzz` instruments every `Pyrlyn.*`, `Rtok.*` and
`Tomlyn` assembly.

Plan: one argument in `eng/Rtok.Build/FuzzTask.cs`; check the publish and the instrumentation
by hand, and a libFuzzer run where a clang with libFuzzer links.
