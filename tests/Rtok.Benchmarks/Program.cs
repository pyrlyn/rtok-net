using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Rtok.Testing;

BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run(
        args,
        DefaultConfig.Instance.WithArtifactsPath(
            Path.Join(RepoPaths.Root, "artifacts", "benchmarks")
        )
    );
