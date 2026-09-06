# Testing and evaluation

**Status:** Normative verification strategy  
**Depends on:** All behavioral specifications

## Purpose

Replaceability is a behavioral claim. Every stabilized contract needs a shared
conformance suite, deterministic fakes, and adversarial scenarios before a
second integration makes the bug matrix fashionable.

## Test layers

Package and conformance tests prove the
[public replacement contracts](public-api-and-dependency-injection.md);
evaluations measure a complete composition without bypassing them.

1. **Value tests** verify immutable types, validation, canonicalization, and
   serialization migrations.
2. **Contract conformance** runs one reusable behavioral suite against every
   implementation.
3. **Runtime composition tests** exercise the loop with scripted collaborators
   through public APIs and DI.
4. **Protocol tests** use loopback transports and sanitized fixtures for exact
   request/stream/error behavior.
5. **Durability tests** inject crashes and replay checkpoints.
6. **Evaluation datasets** measure agent behavior and regressions across models.
7. **Opt-in live tests** verify real integrations under explicit credentials,
   time, and cost bounds.

Unit and conformance gates MUST NOT require live credentials or services.

## Conformance suite shape

Each swappable contract SHOULD publish an abstract xUnit v3 suite or a fixture
package. The fixture factory exposes only legitimate public observation seams.
The same suite runs against the default and every adapter.

Suites MUST derive cases from the contract's inputs, outputs, ordering,
concurrency, cancellation, errors, unsupported capability, ownership, and
disposal—not from private methods or class layout.

Capability-based skipping is permitted only when an implementation explicitly
reports the capability unsupported before use. A surprise
`NotSupportedException` is a failure.

## Required suites

The required suites mirror the lifecycle boundaries: the
[agent loop](agent-loop-state-machine.md),
[provider pipeline](provider-request-pipeline.md),
[tool pipeline](tool-call-lifecycle.md), and
[session store](sessions-persistence-and-branching.md) each own observable
behavior that every implementation must preserve.

- Agent loop lifecycle and terminal-result conformance.
- Input queue admission, conflict, cutoff, order, capacity, and redelivery.
- Message serialization/version/unknown-part round-trip.
- Provider capability, request, fragmented-stream, usage, and error mapping.
- Tool discovery, validation, security, approval, execution, and correlation.
- Tool scheduler source order, barriers, limits, cancellation, and late result.
- Session/store concurrency, pagination, idempotency, branch, and migration.
- Memory/vector isolation, compatibility, deletion, and provenance.
- Hook ordering, sequential mutation, reverse unwind, validation, isolation,
  reentrancy, replacement bounds, short-circuiting, and failure.
- Security request canonicalization, approval and grant binding, single-use
  consumption, expiry, revocation, and denial before protected effects.
- File-system, network, and process grant enforcement plus deterministic fake
  parity.
- MCP lifecycle, capability, correlation, transport cleanup, and content.
- DI replacement, keyed composition, scope validation, and disposal.

## Determinism

Deterministic tests enforce the same injected-time and replay rules required by
[durable recovery](durable-execution-and-recovery.md).

Tests MUST inject `TimeProvider`, ID source, randomness, scheduling gates, and
transport. They SHOULD avoid wall-clock sleeps. Concurrency cases use barriers
and controllable tasks to enumerate significant interleavings.

Stream parsers MUST be tested at every meaningful fragmentation boundary,
including multi-byte text, JSON escapes, interleaved tool arguments, missing and
duplicate terminals, cancellation, and consumer abandonment.

Property-based and model-based tests SHOULD cover state machines, sequence
monotonicity, idempotency, merge algebra, parser fragmentation, and tool-result
exactly-once invariants.

## Fault injection

Inject failure before and after every durability boundary: admission, promotion,
provider send, response terminal, call record, side effect, result record,
message append, compaction activation, and settlement. Assert recovery follows
the evidence table in the durable execution spec.

Security tests prove denial precedes effects, approvals and grants bind exact
scope and audience, single-use grants cannot be spent concurrently, low-level
host components enforce again, untrusted history cannot execute, configuration
trust is enforced, MCP metadata cannot grant authority, and diagnostics redact
seeded secrets.

## Evaluations

`AgentKit.Evaluation` MAY provide the first-party dataset runner, evaluator
composition, comparison, and report model. It MUST operate through public
AgentEngine, event, session, and diagnostic contracts rather than friend access
to runtime internals. External result stores and report exporters remain leaf
integrations.

One evaluation runner is bound through DI to exactly one built `AgentEngine`.
`RunAsync` accepts a plan, not an arbitrary engine, and the runner MUST NOT pair
the bound engine with a separately injected definition catalog. Every case MUST
carry an authenticated `ExecutionIdentity` and the expected `SessionProfileKey`.
Before creating a session through the engine's public API, the runner verifies
that the selected agent definition uses that profile; identity or profile
mismatch fails before provider, tool, or storage effects.

Evaluation cases MUST version input, expected criteria, fixtures/tools, agent
definition, model settings, and evaluator. Results record provider/model,
configuration commit/version, usage/cost, latency, run trace, and evaluator
version.

Prefer deterministic code evaluators for schema, exact behavior, safety, and
tool effects. Model judges MAY assess semantic quality but require calibrated
rubrics, blinded ordering where relevant, repeat runs, and uncertainty.

Evals complement contract tests; they MUST NOT decide whether message ordering,
security enforcement, hook behavior, or persistence is correct.

## Live tests

Live tests are explicitly enabled, cost/time bounded, isolated from production
resources, and skipped clearly when credentials are absent. They supplement
protocol fixtures and SHOULD detect provider drift, not paper over missing unit
coverage.

## Repository gates

Implementations MUST pass focused tests first, then repository format, lint,
build, and test gates. A regression test should fail for the expected reason
before the fix and pass afterward.

## Acceptance criteria

- A new adapter can consume relevant shared suites without copying them.
- Cancellation is exercised at every await/state boundary.
- Parser output is invariant under legal fragmentation.
- Crash matrices prove no duplicate unsafe tool side effect.
- Eval reports are reproducible enough to compare versions honestly.
- Secret canaries never appear in test telemetry artifacts.

## Upstream evidence

- Pydantic AI supplies `TestModel`, `FunctionModel`, override fixtures, and eval
  tooling documented in [testing](https://ai.pydantic.dev/testing/) and
  [evals](https://ai.pydantic.dev/evals/).
- The three researched projects' own tests were used as supporting evidence,
  while observable implementation and official docs remained primary. Exact
  revisions are in [research provenance](research-provenance.md).

## Related specifications

- [Architecture and dependency boundaries](architecture-and-dependency-boundaries.md)
- [Durable execution and recovery](durable-execution-and-recovery.md)
- [Public API and dependency injection](public-api-and-dependency-injection.md)
