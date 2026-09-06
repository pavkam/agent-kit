---
name: agentkit-conformance-testing
description:
  "Create or run reusable AgentKit contract suites for interchangeable loops,
  providers, tools, permission policies, queues, memory, and storage. Use when
  adding an implementation or changing an abstraction's observable behavior."
---

# AgentKit Conformance Testing

Read [AGENTS.md](../../../AGENTS.md). A swappable implementation is only
credible when the same observable contract is executable against all of them.

1. Extract invariants from the public contract: inputs, outputs, ordering,
   lifecycle, cancellation, concurrency, errors, unsupported capabilities, and
   disposal. Do not assert private methods, field layout, or implementation
   types.
2. Build an abstract or parameterized suite around a factory/fixture that
   creates the system under test and exposes only legitimate observation seams.
   Keep provider/store-specific tests beside the shared suite.
3. Run the suite against the default implementation and each integration.
   Capability-based tests may skip only when the implementation explicitly
   reports the capability as unsupported; a thrown `NotSupportedException`
   discovered mid-test is not capability negotiation. OpenAI-compatible
   implementations run both the shared wire-family suite and their branded
   package's capability and DI-registration suite.
4. Use deterministic fakes for clocks, IDs, randomness, transport, queues, and
   external services. Provider protocol tests use loopback HTTP/stream handlers
   or sanitized fixtures, never required live credentials.
5. For streams, generate every meaningful fragmentation boundary, interleaving,
   early completion, malformed event, cancellation point, and consumer
   abandonment. Verify exactly one terminal outcome and correct disposal.
6. For storage/queues, cover ordering, duplicate/idempotent operations,
   optimistic concurrency, pagination, redelivery, expiry, isolation, partial
   failure, and cancellation.
7. For tools and permissions, prove denial happens before side effects and that
   call IDs, arguments, approval scope, result/error, and audit records stay
   correlated.
8. For DI, resolve through the public registration API and prove defaults can be
   replaced, multiple implementations compose as documented, scopes are valid,
   independent provider operations compose as documented, and disposal happens
   once.
9. Keep optional live integration tests separate, explicitly enabled, bounded by
   time/cost, and diagnostic. They supplement rather than replace protocol
   conformance tests.
10. Watch a new regression test fail for the expected reason, then pass after
    the change. Run the shared suite for every affected implementation.

Prefer reusable behavioral evidence over provider-by-provider copies. A test
suite that merely checks interface shape is not conformance.
