# T4 done

Measured hook latency and the store on NativeAOT on macOS (M3 Max) and Linux (aarch64, in a
container on a colima VM). Windows had no host. With the creator's OK it moved to T52, at the
end of lane L4, and T4 closes on macOS and Linux.

What was done:
- `spikes/T4/` (outside `rtok.slnx`) holds the probes:
  - `hook-client`, the `rtok-hook` shape T14 will build: Unix socket, `posix_spawn`, `execv`;
  - `rtok-probe`, the full binary's shape: ConsoleAppFramework, Tomlyn config, EF Core, Kestrel,
    MCP, Spectre;
  - `ef-probe` and `sqlite-probe`.
- `spikes/T4/bench.sh` runs them with hyperfine; `bench-linux.sh` runs the same in the .NET AOT
  SDK container.
- `research.md` F has the dated table for both OSes:
  - p50/p95 for the empty `rtok-hook`, the socket round trip to a resident, the full `rtok hook`
    with config load, EF Core and plain Microsoft.Data.Sqlite;
  - both fallback candidates;
  - exec-to-`Main` for the static-init and module-initializer cost: one module initializer, and
    6 to 6.5 ms from the 14 MB data image;
  - static SQLite per OS;
  - three EF Core AOT pitfalls found on the way.
- `plan/README.md` N8 names the fallback: no-op reply, request queued in the 0700 run dir,
  resident spawned detached. Measured 2.0 ms p50 on Linux and 5.7 ms on macOS, against 20 to
  30 ms for running the full hook in place. N8 also records SQLite linking per OS.
- T14 takes this fallback, and it stays provisional for Windows until T52 lands.

Check: `research.md` F has the dated macOS and Linux table (Windows: T52), and N8 names the
chosen fallback with its measured cost. The repo-wide build, test and CSharpier check pass.
