# Extensions, hooks, and middleware

**Status:** Normative

**Architecture:** [Hooks and extensions](../architecture/extensions.md)

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

- a dedicated hook interface in `AgentKit.Abstractions` or the owning feature's
  contract assembly;
- a dedicated `EventArgs`-derived class;
- one closed point definition binding that interface, argument type, point
  identity, validator, mutability class, and failure invariant;
- invocation frequency and lifecycle scope;
- read-only and writable properties;
- validation after mutation;
- ordering and reentrancy rules;
- cancellation, deadline, and failure behavior; and
- whether typed short-circuiting is permitted.

A shared `AgentHookEventArgs` base MAY expose only the universally valid point
identity, dispatch identity, causal correlation, timestamp, and deadline. It
MUST NOT require `AgentId`, `SessionId`, `RunId`, `TurnId`, an agent or run
view, or any other fact that may not exist when an earlier stage dispatches. It
MUST NOT expose a mutable engine, unrestricted history, credential store, or
general service provider.

Boundary-specific derived arguments MUST carry the domain identities and
immutable views that their stage has established. They MUST omit identities that
do not yet exist rather than use default values, sentinel values, or universally
nullable base properties. They MAY expose writable properties only for
documented transformations. Identity, causality, principal, existing security
decision or grant, durable sequence, committed state, and prior audit facts MUST
remain read-only.

Hooks mutate the permitted properties of one event-argument instance in place.
Later hooks observe the validated result of earlier hooks. The dispatcher MUST
validate after every invocation and reject invalid state before invoking the
next hook or owning operation.

## Identity and causality

Hook contracts MUST distinguish these identities:

- `HookPointId` identifies one stable, namespaced lifecycle boundary;
- `HookRegistrationId` identifies one configured registration in one profile;
- `HookDispatchId` identifies one emission of one point; and
- `HookInvocationId` identifies one registration's execution within that
  dispatch.

The same event-argument instance is passed through a dispatch, so its shared
base MUST carry `HookDispatchId`, not an individual `HookInvocationId`. The
dispatcher MUST create immutable per-invocation context for each registration
execution and use its `HookInvocationId` for diagnostics, reentrancy, and paired
unwind correlation. Registration identity MUST remain stable across equivalent
catalog captures. Dispatch and invocation identities MUST come from injected
generators.

Causal operation correlation is independent of point, registration, dispatch,
and invocation identity. It MUST be available even at stages where an agent,
session, or run identity has not yet been established.

## Registration and dispatch

Applications MAY register any number of implementations for each hook interface.
Registrations are additive and have stable `HookRegistrationId` values. Reusing
the same identity in one point, profile, and catalog is a composition error
unless the registration API explicitly names a replacement.

`AgentKit.Abstractions` defines a typed generic dispatch-kernel contract.
`AgentKit.Hooks` supplies its first-party implementation and `AddAgentHooks`
registration. The kernel MUST accept a closed
`HookPointDefinition<THook, TEventArgs>` that binds the dedicated hook
interface, event-argument type, point identity, validator, mutability class, and
minimum failure behavior. It MUST NOT accept a caller-selected string and
`object` payload or infer those semantics during dispatch.

The owning feature MUST expose one immutable closed point definition, including
the typed invocation delegate, for each point. It MAY hide the kernel behind a
dedicated closed dispatcher as a convenience. Whether it uses that adapter or
calls the kernel directly, the owning runtime supplies its compile-time point
definition rather than assembling an open generic call from arbitrary runtime
values. A custom kernel MUST pass the same ordering, mutation, failure,
cancellation, scope, and diagnostics conformance suites.

Before resolving a hook, the kernel MUST verify that the closed definition,
dispatch context, and event arguments carry the same `HookPointId`, that the
dispatch context and event arguments carry the same `HookDispatchId`, and that
the captured registrations were validated against that definition. A mismatch
MUST fail before any hook invocation.

A third-party feature package MAY add a namespaced typed point by defining its
own dedicated interface, event arguments, point definition, validator,
registration extension, optional closed adapter, and conformance cases. It MUST
be able to do so without editing the central kernel or adding a central method
or type switch. Its contract assembly MAY depend on `AgentKit.Abstractions`; the
core abstractions and `AgentKit.Hooks` MUST NOT depend on the third-party
package. Point definitions are additive, and composition MUST reject one
`HookPointId` bound to incompatible closed types or invariants.

The stable API MUST NOT expose `DispatchAsync(string, object)`, arbitrary
event-name/object registration, or a universal generic hook invocation interface
that erases the named boundary. Shared registration metadata is not a license to
erase the dedicated invocation contract.

Profile selection and catalog capture MUST use only identities established at
their stage. A catalog captured before an agent, session, or run exists MUST NOT
require or invent that identity. The catalog is immutable for its documented
engine, agent, run, turn, or operation scope; in-flight dispatch never observes
partial registration reload.

## Ordering

Ordering constraints are resolved only among registrations for the same hook
point, profile, and captured catalog. A reference MUST NOT order a hook in a
different point, profile, or catalog version.

`Before` and `After` are soft constraints. If the referenced registration is
present in the same resolution scope, the resolver MUST add the corresponding
edge. If it is absent, the constraint is a no-op. `DependsOn` is a hard
constraint; an API that calls it `Requires` has the same semantics. Its target
MUST exist in the same resolution scope and MUST run first, otherwise catalog
validation fails.

`First` and `Last` are singleton anchors, not priority bands. At most one
registration in a resolution scope MAY claim each anchor. The `First` anchor
MUST precede every other registration and the `Last` anchor MUST follow every
other registration. Multiple claims, self-references, the same target named in
contradictory relations, one registration claiming both anchors, an explicit
edge contradicting an anchor, and direct or transitive cycles MUST fail
composition or catalog capture before the owning effect begins. A resolver MUST
NOT silently discard a conflicting edge.

Registration order is the deterministic tie-breaker after all edges. Mutating
hooks MUST execute sequentially. Before hooks execute in resolved order. Paired
after and error hooks unwind in reverse order. Independent notification hooks
use documented forward order.

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

Adding a new hook point is an additive public contract with its own interface,
event arguments, closed definition and adapter, validator, registration
extension, and conformance cases.

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

Patch types use tri-state semantics where omission, clear-the-whole-value,
replace-the-value, and remove-one-map-key differ. After-tool patches are
fieldwise; untrusted nested values are never deep-merged implicitly. A
replacement tool argument is schema-validated, canonicalized, fingerprinted, and
reauthorized before invocation.

An asynchronous before-end hook returns a proposal. The runtime reacquires the
session mutation line and revalidates operation state and the exact input plan
before committing it; new externally admitted input outranks a stale hook
follow-up. Structural hooks define whether the first decisive result wins and
reject contradictory result-plus-decline shapes.

## Scope and reentrancy

Immutable thread-safe hooks MAY be singleton. Mutable hooks MUST be scoped to a
declared engine, agent, run, turn, or operation lifetime, or transient. A
singleton MUST NOT capture state from a shorter lifetime. Scoped state MUST NOT
leak between concurrent scope instances.

Every invocation carries registration, point, dispatch, and invocation
identities, causal operation, and depth. A hook MUST NOT re-enter its own point
implicitly. Explicit reentrancy requires a bounded policy and cycle detection.
Async-local or static state MUST NOT be the authoritative reentrancy mechanism.

Hooks receive cancellation and share the operation deadline unless configured
with a smaller bound. They MUST NOT detach untracked tasks; required hook work
is part of settlement.

Transform chains receive immutable values. Later handlers see earlier validated
replacements, but the canonical message object is never mutated in place. Input
handlers may return a typed handled outcome; session-before structural points
declare decisive-result ordering; context replacements use an immutable cloned
list. Public listeners have an explicit order relative to extension handlers and
an explicit awaited/isolation contract.

Failure policy is point-specific and declared in the closed hook definition.
Read-only observational extension errors MAY be diagnosed and isolated when the
point policy allows; failures that affect tool execution MUST propagate. A
single accidental catch-all is invalid. A provider hook can change only typed
provider-owned request fields; arbitrary wire-object mutation or shared mutable
header dictionaries are not portable extension contracts. A post-tool hook
cannot flip a denied, unexecuted, failed, or uncertain call into success or
replace authoritative usage evidence.

## Failure and observability

Failure modes have an explicit strictness order:
`IsolateAndDiagnose < FailOperation`. For each invocation, the effective policy
MUST be the strictest of the point invariant, host minimum, selected profile
default, current registration request, and any run-, turn-, operation-, or
dispatch-scope tightening. A narrower scope MAY tighten the policy to
`FailOperation`; it MUST NOT relax any broader source. The kernel, closed
adapter, and hook MUST NOT accept a caller-selected mode as authority to weaken
the resolved policy.

Transforming and security-relevant points MUST declare `FailOperation` as their
point invariant. Their exceptions and invalid mutations fail the owning
operation. Cancellation always propagates and is never an isolated hook failure.

`IsolateAndDiagnose` is valid only for an observation or read-only point whose
definition explicitly permits it. Operation-bearing state MUST be read-only at
that point. If the point permits bounded diagnostic annotations, each invocation
MUST stage them and commit only after successful validation, or the dispatcher
MUST snapshot and restore all permitted mutation before continuing. An isolated
hook MUST NOT leak a partial mutation, short-circuit marker, or replacement
value to later hooks or the owning operation. Its failure MUST emit bounded
diagnostics. Hook exceptions never become successful operation results.

A timeout requests cancellation; it does not prove an in-process hook stopped.
The dispatcher MUST NOT restore shared arguments and continue while the
timed-out invocation can still mutate them. It MUST await quiescence within the
cleanup bound or fail the owning operation and retain explicit ownership of the
unquiesced task and scope. It MUST NOT run later hooks over those arguments.
Hard execution or allocation limits require an enforceable host isolation
boundary; cooperative cancellation alone cannot provide them.

Diagnostics SHOULD record registration, point, dispatch, and invocation
identities, duration, outcome, effective failure policy, and names of changed
fields. Values before and after mutation are sensitive payloads and MUST follow
explicit content-capture and redaction policy.

## Acceptance scenarios

- Three registered hooks observe mutations in deterministic order.
- After hooks unwind in reverse order when the operation fails.
- A pre-agent hook has point, dispatch, causality, and timing but no fabricated
  `AgentId`, while a later run hook carries the established agent, session, and
  run identities in its derived arguments.
- One dispatch ID spans three hook executions, each with a distinct invocation
  ID and its own stable registration ID.
- A third-party package adds and dispatches a dedicated typed point through its
  closed adapter without modifying the central kernel; an incompatible type
  binding for the same point ID fails composition.
- A closed adapter cannot dispatch otherwise valid typed arguments under a
  different point or dispatch ID; no hook is resolved before the mismatch fails.
- A mutation of a read-only identity or invalid field fails before the next
  hook.
- A missing soft `Before` or `After` target is ignored, while a missing hard
  `DependsOn` target fails catalog capture even if that ID exists in another
  point or profile.
- Duplicate registration identity, multiple `First` or `Last` anchors, one
  registration claiming both anchors, self-reference, contradictory edges, and
  ordering cycles fail before dispatch.
- Per-scope hook state is isolated under concurrent runs.
- Message transformation yields a new immutable value and cannot mutate a value
  already published to another listener.
- Reentrant dispatch cannot recurse without a declared bounded policy.
- A hook cannot turn a denied operation into an allowed one.
- A changed authorized request is sent back through security evaluation.
- A request-options patch distinguishes absent, clear, replacement, and
  per-header deletion without an implicit deep merge.
- External input admitted while a before-end hook waits invalidates the hook's
  stale queue plan.
- An isolation request cannot weaken a transforming or security point's
  fail-operation invariant; a narrower scope can tighten an observation point.
- An isolated observer that throws after adding diagnostic metadata is rolled
  back, diagnosed, and cannot leak that metadata into the next hook or owning
  operation.

- A timed-out hook that ignores cancellation cannot mutate arguments observed by
  a later hook; failed cleanup retains explicit ownership.

## Related specifications

- [Provider request pipeline](provider-request-pipeline.md)
- [Permissions, approvals, and trust](permissions-approvals-and-trust.md)
- [Observability and audit](observability-and-audit.md)
- [Public API and dependency injection](public-api-and-dependency-injection.md)
