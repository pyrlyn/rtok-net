# T35.4. Agent hosts: proxy base-URL module — done

`rtok agents install --proxy` now points a host's model base URL at rtok's proxy. Uninstall takes the URL back only while it is rtok's, and `agents list|info` show a `proxy` row.

## What was done

- **One URL rule.** `ProxyUrls` moved from `Rtok.Proxy` to `Rtok.Config`, so the hosts library reads the proxy's dial URL (`[proxy] bind` and `port`) without the Kestrel proxy. `HostSettings` carries `Proxy` (`setup.proxy`, `--proxy`) and `ProxyUrl`.
- **Ownership** (`Proxy/ProxyOwner.cs`, rtok `loopback_port`). A base URL is rtok's when it is the URL rtok writes, or an `http(s)` URL whose parsed authority is `localhost`, `127.0.0.0/8` or `[::1]` on the proxy's port. A loopback address in a path, a query or user info is foreign, and so is an IPv4-mapped `::ffff:127.0.0.1`.
- **Proxy module** (`Proxy/ProxyModule.cs`), beside hooks and MCP:
  - `EnvProxy`: Claude Code `env.ANTHROPIC_BASE_URL` in `settings.json` (the root URL) and OpenCode `env.OPENAI_BASE_URL` in `opencode.json` (CLI and Desktop, `/v1`). The JSONC splice keeps every other byte, and a non-object `env` is refused.
  - `CodexProxy`: `model_provider = "rtok" # rtok: was "<old>"` plus `[model_providers.rtok]` with `name` and `base_url` (`/v1`). It puts the root key before the first table and rewrites the provider table in place. Remove restores the replaced provider byte for byte. A provider table the user edited asks first (`?` on a dry run, `leave …` once declined, removed with `--yes`).
  - `AiderProxy`: the `openai-api-base` line in `.aider.conf.yml`, edited as text. Comments, the indent, a trailing comment and the line breaks (CRLF too) stay, a lookalike key is not touched, and a quoted value is rewritten unquoted.
- **Which installs write it.** Claude Code, Codex and aider write the URL only with `--proxy`. OpenCode is `Support.Yes` as in rtok, so every install writes it. `agents update` carries `--proxy` where the URL reads back as rtok's. Uninstall always runs the module, and a foreign URL survives it.
- **Report lines are rtok's:**
  - Claude Code and OpenCode: `env.<KEY>: <url>` then `revert: remove env.<KEY>` or `revert: set env.<KEY> to <old>`, and `- env.<KEY>` on uninstall.
  - Codex: `+ [model_providers.rtok]`, `base_url = "…"`, then `revert: …`. On uninstall, `- [model_providers.rtok]` and `restore model_provider = "<old>"`.
  - aider: `+ openai-api-base: <url>` then `revert: …`, and `- openai-api-base` on uninstall.
  - A current URL gives no changes.
- **`--replace` on Claude Code** now also moves an `ANTHROPIC_BASE_URL` on a legacy loopback port (8788, 8787) to rtok's proxy. It keeps the URL's path, query and fragment and prints `ANTHROPIC_BASE_URL <old> -> <new>` after the hook lines (rtok `retarget_proxy`).
- **`HostRun`.**
  - Install, update and uninstall run hooks, then MCP, then the proxy. The read-back warns "proxy did not read back as installed".
  - Update skips a host only when nothing of rtok is there, proxy included.
  - The `config` line and the module rows (text and JSON) carry the proxy file and state.
- **Paths.** `[setup.aider] config_path` moves aider's file through the new `SetupPaths.ProxyKeys`. Claude Code's file follows `[setup.claude] settings_path`, and Codex and OpenCode use their MCP file.
- **Shared edit types.** `HookFile`, `HookJson` and `TomlHooks` are now the shared `OwnedFile`, `JsonEdit` and `TomlEdit`. `TomlEdit` gained plain `[table]` lookups, root keys (`Root`, `Tail`, `SetLine`, `DropLine`, `InsertRoot`) and an in-place `Replace` of a table.
- **CLI.** `--proxy` on `agents install` and `agents update`. The help, completion and man snapshots and `docs/cli/commands.json` are regenerated.

## Deviations

- **Claude Code and OpenCode ownership is parsed, as the task asks.** rtok compares Claude's URL as an exact string and takes any OpenCode `OPENAI_BASE_URL`, a foreign one too. Here both read back and remove only rtok's URL or a loopback one on the proxy's port.
- **Codex** reads back a parsed `[model_providers.rtok]` table named `rtok`, where rtok uses a substring match. Its update after a port change still carries `--proxy`, as in rtok.
- **A changed port.** On the other hosts the URL on the old port is no longer rtok's, so `install --proxy` moves it and `update` alone does not (as in rtok for Claude and aider).
- **Codex** rewrites `[model_providers.rtok]` where it stands. rtok's toml_edit does the same, and extra keys are dropped.
- **aider** keeps the file's own line breaks. rtok's `lines()` turns CRLF into LF.
- **`env` that is not an object** is refused. rtok replaces it with `{}`.

## How it was checked

- `tests/Rtok.Agents.Hosts.Tests/ProxyTests.cs`:
  - ownership theories: rtok's foreign and ours lists, plus IPv4-mapped;
  - Claude Code: the byte roundtrip, a foreign URL taken over and kept on remove, a `localhost` spelling cleared, a non-object `env` refused;
  - OpenCode: both apps, and a foreign URL kept;
  - Codex: `# rtok: was "openai"` restore byte for byte, re-install after a port change still remembering `openai`, a new provider placed before the first table, an edited table that asks (`leave`, `?`, `--yes`);
  - aider: append with CRLF, a quoted value with a trailing comment, foreign and lookalike keys kept;
  - `--proxy` gating and update read-back through `HostRun`;
  - `--replace` retarget theories from rtok `only_legacy_loopback_endpoints_are_retargeted`.
- `InstallMatrixTests.Proxy_install_twice_and_uninstall_keep_a_foreign_base_url` runs on Linux, macOS and Windows:
  - `--proxy` on every host leaves the URL exactly on the five apps that support it;
  - a second install changes no byte;
  - uninstall takes every URL back;
  - a foreign base URL put in their place survives uninstall byte for byte.
- `AgentsCommandsTests`: every MCP, hooks and proxy path key is a config key.
- The `agents info` Verify snapshots carry the proxy rows.
- The gates are all green: `dotnet build rtok.slnx -c Release -warnaserror --no-incremental`, `dotnet test`, `dotnet csharpier check .` and `npx --yes markdownlint-cli2@0.23.3`.
