# T35.1. Host: Trae — done

Trae still documents no user-level MCP file, so rtok has no Trae installer. The host table now says so, with the date checked and the source, and `rtok agents install trae` refuses with that reason.

## Research (2026-10-22)

[Trae: Add MCP servers](https://docs.trae.ai/ide/add-mcp-servers) gives two ways to add a server:

- **Global servers**: only through Settings > MCP, including its "Raw Config (JSON)" editor, which edits "the mcp.json file in TRAE". The page names no path on disk.
- **Project servers**: `.trae/mcp.json` under the project root, after the "Enable Project MCP" toggle.

Paths such as `~/Library/Application Support/Trae/mcp.json` turn up only in third-party projects, not in Trae's docs. The task sets "a documented user-level path" as the bar, so rtok does not write there. A project-level file is not a user-level install.

## What was done

- **`UnsupportedHost`** (`src/Rtok.Agents.Hosts/UnsupportedHost.cs`): a record of id, app name, kind, the day it was checked, sources, and the reason as one clause.
- **`AgentHostTable.Unsupported`** holds the Trae row, exposed as `AgentHosts.Unsupported`.
- **`AgentHosts.Resolve`** throws `trae is not supported: <reason>` for an unsupported id, and still says `unknown host: <id>` for anything else. Install, uninstall, update and info all resolve before touching a file, so nothing is written. The CLI prints the refusal as `error: …` and exits 1.
- **`SupportMatrix.Table()`** gives each unsupported host a row of `—` cells. After the table comes a "Not supported" list with the reason, the date and the sources. `docs/agents.md` is regenerated from it.

## Tests

- `Rtok.Agents.Hosts.Tests`:
  - `UnsupportedHostTests`: the Trae row is dated 2026-10-22, its sources are on docs.trae.ai, and its reason names the missing user-level file. No unsupported id is also an installable one. `Resolve(["claude", "trae"])` refuses with the reason.
  - `SupportMatrixTests`: the table carries the Trae row and its "Not supported" line, and the row count includes unsupported hosts.
- `Rtok.Cli.Tests`: `AgentsCommandsTests.Install_trae_is_not_supported_and_writes_nothing` is a Verify snapshot of exit 1, empty stdout and the stderr refusal, and it checks that the fake home stays empty.

The installer tests and the fuzz seed apply only if an installer is built, and none was.

## Deviations

- The task names `Rtok.Agents.Tests` for the unit tests. The host table lives in `Rtok.Agents.Hosts`, so they are in `Rtok.Agents.Hosts.Tests`, next to the other host-table tests.
- `docs/port/open-work.md` is left as is. It is the snapshot of rtok's open work from when the port was planned.

## Checks

- `dotnet build rtok.slnx -c Release -warnaserror --no-incremental`
- `dotnet test`
- `dotnet csharpier check .`
- `npx --yes markdownlint-cli2@0.23.3`
