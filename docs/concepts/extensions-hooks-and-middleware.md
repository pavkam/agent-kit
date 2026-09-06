# Extensions, hooks, and middleware

**Status:** Normative  
**Depends on:** [Architecture](architecture-and-dependency-boundaries.md),
[configuration](configuration-and-overrides.md),
[permissions](permissions-approvals-and-trust.md)

## Purpose

AgentKit calls these process extension points hooks. Hooks compose cross-cutting
behavior at named boundaries without granting arbitrary access to mutable engine
internals. They coexist with complete strategy replacement, context
contributors, providers, tools, and immutable event sinks; they do not replace
those narrower contracts.

## Contract shape

Every hook point MUST define:

- a dedicated hook interface in `AgentKit.Abstractions`;
- a dedicated `EventArgs`-derived class;
- invocation frequency and lifecycle scope;
- read-only and writable properties;
- validation after mutation;
- ordering and reentrancy rules;
- cancellation, deadline, and failure behavior; and
- whether typed short-circuiting is permitted.

A shared `AgentHookEventArgs` base MAY expose stable agent, session, run,
operation, correlation, timestamp, and hook-invocation identity. It MUST NOT
expose a mutable engine, unrestricted history, credential store, or general
service provider.

Boundary-specific derived arguments MAY expose writable properties only for
documented transformations. Identity, causality, principal, existing security
decision or grant, durable sequence, committed state, and prior audit facts MUST
remain read-only.

Hooks mutate the permitted properties of one event-argument instance in place.
Later hooks observe the validated result of earlier hooks. The dispatcher MUST
validate after every invocation and reject invalid state before invoking the
next hook or owning operation.

## Registration and dispatch

Applications MAY register any number of implementations for each hook interface.
Registrations are additive and have stable hook identities. Reusing the same
identity is a startup error unless the registration API explicitly names a
replacement.

`AgentKit.Hooks` supplies the first-party dispatcher and `AddAgentHooks`
registration. Runtime components depend on the dispatcher contract from
`AgentKit.Abstractions`, not on the concrete package. A custom dispatcher MUST
pass the same ordering, mutation, failure, cancellation, scope, and diagnostics
conformance suites.

Registration order is the deterministic tie-breaker. Hooks MAY declare before,
after, first, last, and dependency constraints. Composition MUST topologically
sort them and fail startup for cycles, missing dependencies, duplicate
identities, impossible lifetime capture, or unknown hook points.

Mutating hooks MUST execute sequentially. Before hooks execute in resolved
order. Paired after and error hooks unwind in reverse order. Independent
notification hooks use documented forward order. The dispatcher captures an
immutable catalog for the run or named operation; in-flight dispatch never
observes partial registration reload.

## Hook points

AgentKit MAY expose typed hooks around:

- engine, run, and turn lifecycle;
- input admission and queued promotion;
- session load, append, branching, checkpoints, and compaction;
- history validation and context assembly;
- model discovery, selection, request preparation, send, response, and stream;
- tool discovery, validation, security authorization, invocation, and result;
- memory proposal, write, retrieval, and context exposure;
- goal creation, delegation, joining, and completion;
- output validation and publication; and
- security presentation, decision observation, approval resolution, and audit.

The stable API MUST NOT expose a generic hook that receives an arbitrary event
name and object payload. Adding a new hook point is an additive public contract
with its own event arguments and conformance cases.

## Mutation and short-circuiting

Writable values use explicit boundary types. A context hook may replace context
candidates, a model hook may replace approved request options, and a tool-result
hook may replace bounded safe result content. Replacement values MUST retain
required identity and provenance and pass the same validation as values produced
by the owning component.

Short-circuiting is allowed only when the event arguments expose an explicit
typed outcome. A general cancel flag is forbidden. A hook MUST NOT convert
cancellation, denial, approval-required, budget exhaustion, protocol violation,
or unknown side-effect state into success.

Security hooks MAY redact or clarify approval presentation, request a stricter
decision, or observe outcomes. They MUST NOT allow, widen, forge, cache,
consume, or mint a security grant. An operation changed after authorization MUST
return to the security authority before execution.

## Scope and reentrancy

Immutable thread-safe hooks MAY be singleton. Mutable hooks MUST be scoped to a
run or operation. A singleton MUST NOT capture scoped run state. Per-run state
must not leak between concurrent runs.

Every invocation carries hook identity, hook point, causal operation, and depth.
A hook MUST NOT re-enter its own point implicitly. Explicit reentrancy requires
a bounded policy and cycle detection. Async-local or static state MUST NOT be
the authoritative reentrancy mechanism.

Hooks receive cancellation and share the operation deadline unless configured
with a smaller bound. They MUST NOT detach untracked tasks; required hook work
is part of settlement.

## Failure and observability

Transforming and security-critical hook failures fail the owning operation by
default. Best-effort notification hooks MAY isolate failure, but they must emit
bounded diagnostics. Hook exceptions never become successful operation results.

Diagnostics SHOULD record hook identity, point, duration, outcome, and names of
changed fields. Values before and after mutation are sensitive payloads and MUST
follow explicit content-capture and redaction policy.

## Acceptance scenarios

- Three registered hooks observe mutations in deterministic order.
- After hooks unwind in reverse order when the operation fails.
- A mutation of a read-only identity or invalid field fails before the next
  hook.
- Duplicate hook identity and ordering cycles fail at startup.
- Per-run hook state is isolated under concurrent runs.
- Reentrant dispatch cannot recurse without a declared bounded policy.
- A hook cannot turn a denied operation into an allowed one.
- A changed authorized request is sent back through security evaluation.
- An observer failure cannot mutate or fail an unrelated operation.

## Upstream evidence

- Pydantic AI defines capability merge, per-run isolation, ordering constraints,
  and nested hooks under
  [`capabilities`](https://github.com/pydantic/pydantic-ai/tree/c0e4d824eaa0401d4481d401e5b3894ab32ab59d/pydantic_ai_slim/pydantic_ai/capabilities).
- Pi's extension runner exposes lifecycle, request, context, tool, and session
  hooks in
  [`extensions/types.ts`](https://github.com/badlogic/pi-mono/blob/9767ba275f3e9a5ee0f5c5342249b629ab1b2282/packages/coding-agent/src/core/extensions/types.ts).

## Related specifications

- [Provider request pipeline](provider-request-pipeline.md)
- [Permissions, approvals, and trust](permissions-approvals-and-trust.md)
- [Observability and audit](observability-and-audit.md)
- [Public API and dependency injection](public-api-and-dependency-injection.md)
