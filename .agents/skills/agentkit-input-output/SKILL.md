---
name: agentkit-input-output
description:
  "Implement or debug AgentKit input admission, work queues, live event streams,
  structured final output, backpressure, and channel adapters. Use for I/O
  boundary behavior; not for loop state transitions or session-store internals."
---

# AgentKit Input and Output

Read [AGENTS.md](../../../AGENTS.md). Keep admission, durable queue truth, live
publication, and final results distinct even when one facade exposes them.

1. Put provider-neutral receipts, delivery classes, queue entries, stream
   events, subscriptions, and terminal result values in `AgentKit.Abstractions`.
   Put the first-party coordinator in `AgentKit.IO`; keep channel adapters in
   leaf packages.
2. Admit input only after authorization, bounds, validation, and durable
   recording succeed. Stable admission and idempotency identities must make an
   exact replay return the existing receipt and conflicting content fail.
3. Preserve steering versus follow-up delivery. Promote input only at explicit
   safe boundaries using a captured cutoff and deterministic order; never splice
   new content into an in-flight provider request or tool call/result pair.
4. Define capacity, ordering, backpressure, redelivery, expiry, poison handling,
   and cancellation for every queue. Persist durable queue state through session
   contracts rather than creating a second source of conversational truth.
5. Publish typed provisional events with stable sequence and correlation.
   Bounded fan-out must distinguish required consumers from best-effort ones and
   make dropped or coalesced output observable.
6. Return one typed terminal result after settlement. Structured output remains
   provisional until local validation succeeds; validation retries consume a
   separate budget.
7. Keep subscription disposal separate from run cancellation unless the public
   contract explicitly grants the subscription ownership. Channel disconnects
   must not silently abandon durable work.
8. Test admission conflicts, concurrent cutoff races, queue capacity and
   ordering, redelivery, fan-out backpressure, abandoned consumers, event loss
   markers, structured-output failures, cancellation, and exactly one terminal
   result using `TimeProvider` and controllable scheduling gates.

HTTP, console, UI, and messaging adapters translate protocols. They do not
bypass admission, session authorization, the loop, or settlement.
