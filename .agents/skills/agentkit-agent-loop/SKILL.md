---
name: agentkit-agent-loop
description:
  "Implement or debug AgentKit run state, continuation, cancellation, terminal
  outcomes, and settlement. Use for AgentKit.Loop control flow; not messages,
  input queues, goals, provider translation, tool execution, or persistence."
---

# AgentKit Agent Loop

Follow [Agent runtime](../../../docs/architecture/agent-runtime.md), the
[loop state machine](../../../docs/concepts/agent-loop-state-machine.md), and
[run settlement](../../../docs/concepts/run-lifecycle-and-settlement.md). When
changing C#, also read the [modern C# rules](../references/modern-csharp.md).

## Decision guide

1. Keep `AgentKit.Loop` a replaceable coordinator. It owns control flow and
   continuation decisions, while selected collaborators own admission, context,
   model selection and execution, tools, output validation, sessions, security,
   budgets, hooks, and observation.
2. Model states and allowed transitions explicitly. Validate each transition and
   produce exactly one terminal outcome followed by exactly one settlement.
3. Preserve the distinction between generation complete, turn complete, run
   complete, and run settled. The ordinary high-level operation waits for
   settlement.
4. Capture immutable run and turn snapshots. No ambient current agent, service
   locator, or mutable engine-wide run state may influence execution.
5. Ask collaborators to perform their policy decisions; do not reimplement them
   in the loop. A changed fallback model returns through context assembly before
   another attempt.
6. Apply the selected continuation policy only after required commits and typed
   output decisions. Limit exhaustion, deferral, cancellation, policy halt, and
   failure remain distinct outcomes.
7. Reserve through the run budget before concurrent work. Budget policy belongs
   to `AgentKit.Budgets`; the loop responds to its typed decisions.
8. Record cancellation source, stop new work, propagate cancellation, and drain
   only what settlement requires. Never retry uncertain effects generically.
9. Test every transition, continuation and stop branch, cancellation boundary,
   collaborator failure, partial attempt, and settlement path with deterministic
   clocks, identities, and scripted collaborators.

Use
[cancellation and resilience](../../../docs/concepts/cancellation-timeouts-and-resilience.md)
for drain and retry semantics. Route message modeling, I/O admission, goals,
structured output, and tool behavior to their owning skills.
