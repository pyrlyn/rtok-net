# T61. Done

`eng/Rtok.Build/FuzzTask.cs` publishes `tests/Rtok.Fuzz` with `-p:RtokJitOnly=true`. The global
property reaches every referenced project, so Rtok.Cli no longer turns on `PublishAot` in
Release and the publish no longer needs a RID. Commit `T61: Fuzz publish keeps the whole graph
JIT`.

Checked on osx-arm64:

- `dotnet publish tests/Rtok.Fuzz -c Release -o <dir>` without the property fails with
  NETSDK1191 in `src/Rtok.Cli/Rtok.Cli.csproj`; with it the publish succeeds.
- `dotnet sharpfuzz` instruments all 44 `Pyrlyn.*`, `Rtok.*` and `Tomlyn` assemblies of that
  output. Replaying the `durations` corpus through the instrumented `rtok-fuzz.dll` outside the
  harness stops with an AccessViolationException in `HumanDuration.TryParse`, which is the
  instrumentation writing to coverage memory only libfuzzer-dotnet maps, so the
  instrumentation is in place.
- Repo gates: Release build with `-warnaserror` (`--no-incremental`), csharpier and markdownlint
  are green. `dotnet test`: 3539 of 3551 passed; the one failure,
  `WriterProcessTests.EveryLineOfEveryWriterIsInTheRotatedFiles`, is a multi-process log test
  that passes when rerun alone and does not touch the build project.

Not checked: a libFuzzer run. Apple clang ships no libFuzzer, and the conda clang 21.1.8
(mise) fails to link its `libclang_rt.fuzzer_osx.a` on this macOS (`__DefaultRuneLocale`
undefined). Homebrew llvm or Linux clang should run it.

The other failure seen during T57, `FuzzTargetTests.MutatedSeedsRun(agents-install)` with seed
`2lNIJ27z8H8h` on input `\0*xtensions: [`, was already fixed on `main` by T35 (028e9f4, a YAML
key that is not a scalar is a refusal); the seed and the input pass, and
`corpus/agents-install/16` covers the same alias-key case.
