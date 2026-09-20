---
name: agentkit-input-output
description:
  "Implement or debug AgentKit input admission, queued promotion, live event
  fan-out, final-result publication, and channel adapters. Not for loop state,
  structured-output validation, or session-store internals."
---

# AgentKit Input and Output

Follow [Input and output](../../../docs/architecture/input-and-output.md),
[input admission and queues](../../../docs/concepts/input-admission-and-message-queues.md),
and the
[streaming protocol](../../../docs/concepts/streaming-and-event-protocol.md).
When changing C#, also read the
[modern C# rules](../references/modern-csharp.md).

For a coding host, read
[interactive terminals](../../../docs/profiles/coding-harness/interactive-terminals-and-process-sessions.md)
for PTY fan-out and
[export, sharing, and control plane](../../../docs/profiles/coding-harness/coding-harness-export-sharing-and-control-plane.md)
for HTTP/SSE/WebSocket/RPC routing and reconnect semantics. Read
[frontend and protocol adapters](../../../docs/profiles/coding-harness/coding-harness-frontends-and-protocol-adapters.md)
for TUI, IDE, batch, RPC, or ACP-style projection behavior, including
server-realm identity and projection-incarnation fencing.

## Decision guide

1. Put provider-neutral receipts, delivery classes, queue entries, events,
   subscriptions, and terminal results in `AgentKit.Abstractions`. `AgentKit.IO`
   owns the first-party coordinators; channel adapters are leaves.
2. Authorize, bound, validate, and durably record input before returning an
   admission receipt. Equivalent idempotent replays return the existing receipt;
   conflicting content fails explicitly.
3. Preserve steering versus follow-up delivery. Promotion uses a captured cutoff
   and deterministic order only at safe boundaries. `Agent.SteerAsync` and
   `Agent.FollowUpAsync` admit that input through a fresh before-run correlation
   without starting a run; the loop still promotes it. `Agent.SteerAsync` and
   `Agent.FollowUpAsync` admit that input through a fresh before-run correlation
   without starting a run; the loop still promotes it.
4. Define queue capacity, ordering, leases or redelivery where relevant,
   retention, poison handling, and backpressure. Session contracts remain the
   durable truth; I/O does not create a second store.
5. Publish typed provisional events with stable run correlation and sequence.
   Required and best-effort consumers have explicit backpressure, coalescing,
   loss-marker, and failure behavior.
6. Subscription disposal does not cancel durable work unless the public contract
   explicitly transfers cancellation ownership.
7. Reject pre-admission failure without a fabricated RunId. Publish one finished
   result for accepted work, separating semantic outcome and settlement status.
   The loop owns state transitions and settlement; `AgentKit.Output` owns
   extraction, validation, repair decisions, and conversion.
8. Keep HTTP, console, UI, chat, and worker adapters as protocol translators.
   They cannot append arbitrary history or bypass admission, security, the loop,
   or settlement.
9. Test idempotency conflicts, promotion races, full queues, redelivery, fan-out
   pressure, abandoned consumers, event loss, cancellation, and exactly one
   final result with deterministic scheduling.

Persist the resolved lane on admission and use session-sequence promotion
cutoffs. Equivalent replay does not consume capacity. Recoverable run-event
sequences reserve durable ranges before publication; redelivery preserves their
identity and a new drive never restarts the counter.

Use
[run lifecycle and settlement](../../../docs/concepts/run-lifecycle-and-settlement.md)
when result availability crosses runtime settlement. Structured-output behavior
belongs to its dedicated architecture and skill, not this one.
