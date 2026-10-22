---
status: todo
priority: P3
complexity: 2
readiness: 0%
agent:
---

# T35.2.9. Not-supported host rows

The T35.2 research found popular agents rtok cannot install into. Each gets a row in T35.1's `AgentHosts.Unsupported` with the date, sources and reason, so `rtok agents install <id>` says why instead of `unknown host`.

Candidates, each re-checked against the vendor's docs before its row is written:

| Host | Reason found | Source |
| --- | --- | --- |
| JetBrains AI Assistant | MCP is set only in the settings UI | <https://www.jetbrains.com/help/ai-assistant/mcp.html> |
| GitHub Copilot for JetBrains | `mcp.json` path not documented | GitHub Copilot customization docs |
| Qodo (IDE, Command CLI) | MCP set in the UI or a project-level file only | <https://docs.qodo.ai/qodo-ide/tools-mcps/agentic-tools-mcps> |
| Letta Code | no file-based MCP config documented | <https://docs.letta.com/letta-code/configuration/> |
| iFlow CLI | discontinued; moved to Qoder | <https://platform.iflow.cn/en/cli/configuration/settings> |
| Jules | cloud only; MCP keys set in the web settings | <https://jules.google/docs/changelog/2026-02-02/> |
| Cherry Studio | MCP set only in the settings UI | <https://docs.cherry-ai.com/docs/en-us/advanced-basic/mcp/install> |
| Void | development paused; no docs | — |
| CodeCompanion, avante.nvim | configured in the user's Lua `setup()` code | <https://codecompanion.olimorris.dev/configuration/mcp> |

A host whose docs now name a user-level file is left out here and proposed to the creator as its own sub-task. A host the T35.2.1–8 sub-tasks turn into not-supported rows lands in those tasks, not here.

Tests:
- `Rtok.Agents.Hosts.Tests` unit: every not-supported row is dated and has a reason and at least one source (Void may cite its repository's archive notice); no id is both supported and not supported.
- `Rtok.Agents.Hosts.Tests` unit: the support matrix and `docs/agents.md` carry each row and its reason.
- `Rtok.Agents.Hosts.Tests` unit: `AgentHosts.Resolve` refuses each id with `<id> is not supported: <reason>`.

Check: Each listed host has a dated not-supported row with a reason and sources, or a note in `done.md` on why it was left out.
