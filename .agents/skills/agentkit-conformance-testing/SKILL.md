---
name: agentkit-conformance-testing
description:
  "Create or run reusable AgentKit contract suites for interchangeable
  implementations. Use when adding an implementation or changing a public
  abstraction's behavior; not for composed-agent quality evaluation."
---

# AgentKit Conformance Testing

Follow
[Testing and evaluation](../../../docs/architecture/testing-and-evaluation.md)
and its
[normative verification strategy](../../../docs/concepts/testing-and-evaluation.md).
When changing C#, also read the
[modern C# rules](../references/modern-csharp.md).

## Decision guide

1. Extract observable invariants from the owning component's public contract:
   results, ordering, concurrency, cancellation, errors, capabilities, lifetime,
   disposal, and side-effect certainty.
2. Build a reusable suite around a fixture that creates the subject through its
   public DI surface and exposes only legitimate observation seams.
3. Run the same suite against the first-party implementation and every adapter.
   A capability may skip only behavior declared unsupported before use.
4. Use deterministic clocks, identities, randomness, scheduling, transports,
   stores, and external-service fakes. Required conformance never needs live
   credentials, the public network, real processes, or the developer filesystem.
5. Exercise arbitrary stream fragmentation, interleaving, cancellation, consumer
   abandonment, concurrency races, duplicate operations, and partial failure
   wherever the contract permits them.
6. Keep implementation- and wire-specific tests beside each package. Shared
   protocol-family suites supplement, but never replace, concrete package
   capability, options, error, and registration tests.
7. Prove protected operations are denied before effects and that grants are
   revalidated at the effecting boundary.
8. Observe a focused regression fail for the intended reason, then pass; run the
   shared suite for every affected implementation.
9. Keep live verification opt-in, isolated, credential-aware, and bounded by
   time and cost. Public API snapshots complement behavioral conformance.

Use `agentkit-evaluation` when the question is whether a fully composed agent
does useful work. Conformance answers whether replaceable components honor the
same contract.
