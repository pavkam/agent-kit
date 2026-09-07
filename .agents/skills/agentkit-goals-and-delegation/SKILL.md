---
name: agentkit-goals-and-delegation
description:
  "Design, implement, or debug AgentKit goals, attempts, delegation,
  agent-to-agent communication, joins, and durable handoffs. Use for multi-agent
  work state; not ordinary loop turns or input queues."
---

# AgentKit Goals and Delegation

Read [AGENTS.md](../../../AGENTS.md), the
[goals architecture](../../../docs/architecture/goals-and-delegation.md), and
the
[normative delegation specification](../../../docs/concepts/goals-and-multi-agent-delegation.md).
When changing C#, also read the
[modern C# rules](../references/modern-csharp.md).

When delegation is exposed as a coding-harness task/subagent tool, also read the
[built-in tool profile](../../../docs/profiles/coding-harness/coding-harness-built-in-tools.md)
for catalog snapshots, placement, nested call budgets, and result publication.

## Boundary

- Goals and attempts are durable versioned domain state, never prompt prefixes.
  Contracts live in AgentKit.Abstractions; AgentKit.Goals owns first-party
  coordination and delegation policies.
- Local dispatch commits a durable child intent; a host-owned worker drains it
  through the same engine's public run surface. Never capture the runner in a
  dispatcher constructor cycle, construct a nested engine, or locate services
  ambiently. Waiting parents release worker occupancy, not spent child budget.
- Preserve goal, attempt, delegation, agent, session, run, operation, and causal
  identities across every transition.
- Delegation narrows authority, data, resources, context, budget, and deadline.
  It cannot broaden the parent's authority or mutate the parent's history.
- Route agent communication through normal input admission with typed routing
  and idempotency metadata; prose is content, not control state.
- Capture join criteria and result ordering. Ordinal joins ignore completion
  timing; fastest-valid joins persist a durable inbox winner and replay it.
  Child prose remains untrusted after schema validation; verify exact evidence.
- Bound depth, child count, concurrent attempts, messages, usage, cost, and
  elapsed time. Cancellation must settle or durably hand off every attempt.

Test lifecycle transitions, idempotent creation, lease ownership, narrowed
authority, deterministic joins, cancellation, and replay through public
contracts.
