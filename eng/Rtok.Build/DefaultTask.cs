using Cake.Frosting;

namespace Rtok.Build;

/// <summary>No <c>--target</c>: lists the tasks and their arguments.</summary>
[TaskName("Default")]
public sealed class DefaultTask : FrostingTask<BuildContext>
{
    private const string Help = """
        usage: dotnet run --project eng/Rtok.Build -- --target=<task> [arguments]

          publish            --rid=<rid> [--aot-tests]
                             NativeAOT rtok and rtok-hook (and the AOT lane) into artifacts/dist/<rid>/
          smoke              --rid=<rid> --dir=<dir>
                             rtok --version and every hook fixture against rtok-hook in <dir>
          package-release    --rid=<rid> --rtok=<native rtok> [--allow-missing]
                             artifacts/release/rtok-<rid>.tar.gz (.zip for win-*) and its .sha256
          publish-package    --tag=<Name>/v<version> [--dry-run]
                             test, pack and push one own package (packages/README.md)
          affected-tests     --out=<file.slnf> (--base=<git ref> | --all)
                             a solution filter with the JIT test projects a change touches
          check-marketplace  [--strict]
                             the Claude and Codex marketplace catalogs
          new-package        --name=<Pyrlyn.Name|Rtok.Name> --summary="<one line>"
                             scaffold packages/<Name>/ and add it to rtok.slnx
          fuzz               --fuzz-target=<target> [--flags="<libFuzzer flags>"]
                             one rtok-fuzz target under libFuzzer (tests/README.md)

        <rid>: osx-arm64, osx-x64, linux-x64, linux-arm64, linux-musl-x64, linux-musl-arm64,
        win-x64, win-arm64.
        """;

    public override void Run(BuildContext context) => Console.WriteLine(Help);
}
