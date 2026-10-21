// ef-probe <db>: open the store, insert one calls row, count by event.
using Probe;

using var ctx = StoreContext.Open(args[0]);
var n = ctx.Record("hook", "PreToolUse", "Bash", 1024);
Console.Out.Write(n > 0 ? "{}" : "");
return 0;
