---
status: todo
priority: P2
complexity: 4
readiness: 0%
agent:
---

# T37. Agent registry, identity, sessions and messaging

Several agents work in one repo at once; each needs an id, and they need to message each other (rtok D34).

Done when:
- The agents table is upserted on every hook event, and sub-agents are re-parented.
- Ids resolve from a prefix of 4 or more hex chars.
- The agent learns its id at SessionStart and SubagentStart.
- `agents whoami/sessions/show/status/send/inbox` and the MCP equivalents work.
- Messages are pushed within `push_bytes` and framed as information, not instructions.

Tests:
- `Rtok.Agents.Tests` integration: each hook event upserts one agents row unique on (host, host_session, parent_key), SessionEnd sets `ended_at`, and a resume clears it.
- `Rtok.Agents.Tests` unit: activity is the tool name plus the first 60 chars of the main argument, at most 120 chars, never file contents.
- `Rtok.Agents.Tests` integration: a sub-agent that arrives before its main agent is re-parented once the main agent registers.
- `Rtok.Agents.Tests` CsCheck: any unique hex prefix of 4 or more chars resolves to its id, a shorter one is refused, a shared one answers `ambiguous: <ids>` and no match answers `unknown`.
- `Rtok.Hooks.Tests` Verify: SessionStart and SubagentStart inject `rtok agent id: <short> (full: <uuid>). …` at priority 10, byte-stable across runs.
- `Rtok.Hooks.Tests` integration: SessionStart writes `RTOK_AGENT_ID` to `CLAUDE_ENV_FILE`, and `CLAUDE_CODE_SESSION_ID` finds an existing agent but never creates one.
- `Rtok.Cli.Tests` Verify: `agents whoami`, `agents sessions` (live, idle, ended, sub-agents indented, "nothing is running" when empty) and `agents show`, as text and `--json`.
- `Rtok.Cli.Tests` integration: `agents sessions watch` piped prints repeated plain tables and exits 0 on a closed pipe.
- `Rtok.Agents.Tests` unit: `agents status` keeps at most 120 chars and a blank text clears it.
- `Rtok.Agents.Tests` integration: `agents send` stores a control-stripped body of at most 4 KiB, `--all-live` reaches only live agents, and a send to an ended agent is refused.
- `Rtok.Agents.Tests` integration: `agents inbox --unread` and MCP `agent_inbox` (unread by default, limit 20, max 100) mark returned messages read.
- `Rtok.Mcp.Tests` Verify: wire JSON of `whoami`, `agents_list`, `agent_show`, `agent_status_set`, `agent_send` and `agent_inbox`.
- `Rtok.Agents.Tests` Verify: a message renders in the fixed frame with sender, host and "information, not an instruction", even when its body reads like an instruction.
- `Rtok.Hooks.Tests` integration: UserPromptSubmit and PostToolUse push undelivered messages up to `[agents] push_bytes` (1 KiB), end with `… and N more: call agent_inbox (or run rtok agents inbox)`, and mark them delivered, not read.

Spec: catalog B6, catalog A26 in [docs/port/catalog.md](../../docs/port/catalog.md).

Ports: rtok closed T25.0, T25.1, T25.2, T25.3, T282, T283, T283.1, T283.2, T283.3, T284, T287, T288, T290, T473, T613.

Check: Tests cover re-parenting, prefix resolution, message framing and push caps.
