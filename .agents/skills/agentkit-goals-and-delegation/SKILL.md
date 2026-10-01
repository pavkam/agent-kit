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
  coordination, delegation policies, joins, the local dispatcher, and the
  session-backed store. Store leaves are `AgentKit.Goals.InMemory`, `.Json`, and
  `.Sqlite`; they compile the shared reducer and planner from the source-only
  `AgentKit.Goals.Storage.Shared` and `.Durable` folders and all run the shared
  conformance suite. Change the reducer once, never per adapter.
- Local dispatch commits a durable child intent; `AgentKit.Goals.Hosting`, an
  application leaf over the `AgentKit` facade, drains it through the same
  engine's public run surface. Never capture the runner in a dispatcher
  constructor cycle, construct a nested engine, or locate services ambiently.
  Waiting parents release worker occupancy (parked by session), not spent child
  budget.
- The worker claims a child with one atomic transition that records the attempt
  before anything runs; that claim is what makes a duplicate signal, a rescan,
  or a second worker harmless. The signal is a lossy hint and the durable scan
  is truth. A running attempt left by a previous incarnation settles as failed
  with unknown effects and is never silently rerun.
- The `task` tool builds one canonical `DelegationRequest` for
  `IDelegationCoordinator` and authorizes nothing itself. The composition
  validator in the facade proves the registrations a goal profile names; keep it
  descriptor-only and never activate a store or worker from it.
- Preserve goal, attempt, delegation, agent, session, run, operation, and causal
  identities across every transition.
- Delegation narrows authority, data, resources, context, budget, and deadline.
  It cannot broaden the parent's authority or mutate the parent's history.
- Route agent communication through normal input admission with typed routing
  and idempotency metadata (`IAgentMessageChannel`); prose is content, not
  control state, and message text never gains instruction authority.
- Capture join criteria and result ordering. Ordinal joins ignore completion
  timing; fastest-valid joins persist a durable inbox winner and replay it.
  Child prose remains untrusted after schema validation; verify exact evidence.
- Bound depth, child count, concurrent attempts, messages, usage, cost, and
  elapsed time. Cancellation must settle or durably hand off every attempt.

Test lifecycle transitions, idempotent creation, lease ownership, narrowed
authority, deterministic joins, cancellation, and replay through public
contracts.
