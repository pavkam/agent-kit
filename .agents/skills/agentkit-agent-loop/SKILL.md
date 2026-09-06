---
name: agentkit-agent-loop
description:
  "Implement or debug AgentKit messages, streaming events, orchestration loops,
  goals, work queues, tool-call turns, limits, and termination. Use for
  run-state behavior; not for provider wire translation or storage internals."
---

# AgentKit Agent Loop

Read [AGENTS.md](../../../AGENTS.md). Model the loop as an explicit, replaceable
state machine.

1. Write the states, inputs, outputs, and terminal reasons before code. Typical
   boundaries are queued, preparing context, awaiting model, streaming, awaiting
   tools, committing results, completed, cancelled, limit reached, and failed;
   use names that fit the actual contract.
2. Keep messages immutable and ordered. Represent text, images, audio, tool
   calls/results, citations, reasoning metadata, and provider extensions as
   typed content parts. Preserve IDs and unknown parts across a round trip.
3. Treat system/developer/user/assistant/tool semantics as provider-neutral
   roles or instructions with explicit downgrade behavior. Do not concatenate
   everything into a prompt string inside the core loop.
4. Let injected strategies own provider selection, context assembly, input and
   output coordination, goal planning, tool selection/execution, memory,
   stopping, and observation. The loop coordinates them; it does not rediscover
   them through a service locator.
5. Model goals with identity, status, parent/child causality, attempts, and
   outcome. Queues define ordering, lease/visibility, retries, poison handling,
   backpressure, and cancellation rather than behaving like `List<Message>`.
6. Enforce budgets for turns, tokens/usage, tool calls, elapsed time, queued
   work, and context. A limit produces a typed terminal outcome, not a generic
   provider exception.
7. Route model-requested tools through the tool executor and permission system.
   For parallel calls, preserve each call ID and choose deterministic result
   ordering independent of completion timing.
8. Propagate cancellation through every await and async stream. Distinguish user
   cancellation, timeout, provider failure, tool failure, policy denial, and
   invalid state.
9. Emit structured run events at state transitions. Observers must not be able
   to mutate state or break the run; define backpressure and observer-failure
   behavior.
10. Test with scripted providers, tools, queues, memory, `TimeProvider`, and
    deterministic IDs. Cover zero-turn completion, multi-tool turns, partial
    streams, cancellation at each state, limits, retries, queue redelivery,
    observer failure, and resume/replay when supported.

Avoid a single concrete "while true" loop that owns every subsystem.
`AgentKit.Loop` must remain replaceable using the same abstractions it consumes;
queued admission and stream fan-out belong to `AgentKit.IO`, not the loop.
