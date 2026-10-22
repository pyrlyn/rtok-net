# Changelog

## Unreleased

- `IPlugin.CallTool` answers a call of the plugin's own MCP tools.
- `MemoryHost.Cwd` sets the dispatch's working directory, so a `CallTool` test can name one.

## 0.2.0

- `ArchiveIds.Abbreviate`: the 8-hex printed form of an archive id, for every trailer and pointer.
- `MemoryHost` archive ids are the full SHA-256 hex, as in the host's store; `Archived()` lists them.

## 0.1.0

- First release.
