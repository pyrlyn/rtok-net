---
status: todo
priority: P1
complexity: 2
readiness: 0%
agent:
---

# T10. Logging: rotating log and `rtok logs`

One bounded log for everything in `rtok` (rtok D26), through Serilog (N20). `rtok-hook` does not log.

Done when:
- `Rtok.Log` builds the Serilog logger in code from `[log]`; the keys behave as in the catalog: path, max_bytes 1 MiB, 5 files, level, to_db. The Serilog minimum level is the level floor, so lines below it never reach a sink.
- The file sink is an `ILogEventSink` over Pyrlyn.RotatingLog (T12.10): `.N` rotation, the `errors.log` copy, lock-safe appends. Serilog.Sinks.File is not used.
- One Serilog pipeline is the single funnel: it writes the file line and, with `to_db`, the logs row (a second sink), and fails open: no sink error reaches the caller.
- EF Core, ASP.NET Core and MCP SDK `ILogger` output joins the same pipeline through Serilog.Extensions.Logging.
- `rtok logs [--lines N]`, `logs export` and `logs watch` work. `watch` follows rotation by file identity and restarts after in-place truncation.
- `RTOK_LOG` debug output goes to stderr through Serilog.Sinks.Console.
- Serilog and its sinks add no AOT or trim warnings to the Release build of `rtok`, and `rtok-hook` references none of them.

Spec: catalog A27 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T24.0, T24.1, T24.2, T24.3, T24.4, T24.5, T225, T225.1, T270, T589.

Check: Rotation, retention and follow tests pass, including truncation and an empty replacement file. A test logs through Serilog from several processes and finds every line in the `.N` files.
