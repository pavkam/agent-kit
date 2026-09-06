# Extensions, hooks, and middleware

**Status:** Normative  
**Depends on:** [Architecture](architecture-and-dependency-boundaries.md),
[configuration](configuration-and-overrides.md)

## Purpose

Capabilities and middleware compose cross-cutting behavior without granting
arbitrary access to mutable loop internals. They extend named boundaries; they
are not a universal plugin escape hatch.

## Extension forms

- **Capability:** declarative contribution of instructions, tools, model
  settings, native provider features, or lifecycle behavior.
- **Middleware:** around/next composition for one narrow operation.
- **Hook:** before or after notification/transformation at a named boundary.
- **Event sink:** immutable observation with no state mutation authority.
- **Strategy:** complete replacement of one policy or operation contract.

An extension MUST use the narrowest form that fits. Generic “on anything” hooks
are forbidden in the stable public API.

## Named boundaries

AgentKit MAY expose typed boundaries for run start/end/error, node/turn start
and end, input admission, history processing, context assembly, model selection,
request translation, provider send/response, stream events, tool preparation,
tool validation, permission, invocation, result normalization, output
validation, compaction, and event publication.

Each boundary MUST document allowed inputs, permitted replacement fields,
ordering, cancellation, failure policy, and whether it runs once per run,
request, attempt, or tool call.

## Composition order

Registration order is the deterministic tie-breaker. Capabilities MAY declare
ordering constraints such as outermost, innermost, before, after, wraps,
wrapped-by, or requires. Composition MUST topologically sort these constraints
and fail startup with a useful cycle/missing-dependency diagnostic.

For ordered middleware `A, B, C`:

```text
before: A -> B -> C
operation
after:  C -> B -> A
wrap:   A(B(C(operation)))
```

The same rule applies to errors unwinding through wrappers.

## Merge behavior

Capability contributions use explicit algebra: instructions append, model
settings merge by key, toolsets combine by stable identity, native tools collect
subject to provider constraints, and scalars replace only where named.

Hooks returning replacements MUST use typed immutable values. They may replace
only documented fields. Tool call ID, principal, permission outcome, durable
sequence, and already committed state are never mutable extension fields.

## Isolation

Stateful capabilities MUST produce a per-run instance through `ForRun` or a
scoped factory. Shared definitions are immutable and thread-safe. Per-run state
must not leak into another concurrent run.

Extensions receive typed context views and explicit dependencies. They MUST NOT
receive `IServiceProvider`, private loop state, credential stores, or unbounded
raw history unless their specific contract requires and authorizes it.

## Failure policy

Transforming/security-critical middleware fails its operation by default.
Best-effort observers are isolated and diagnosed. An extension may not swallow
cancellation, policy denial, budget exhaustion, or protocol failure and convert
it into success.

Hook time, allocations, exceptions, and changes SHOULD be observable. Hooks
share the relevant operation deadline unless a smaller bound is configured.

## Hot reload

Extension code loading requires trusted configuration and normally occurs at
host composition, not mid-run. Declarative capability configuration may reload
at turn boundaries after full validation. Existing run-scoped instances remain
on their captured version until the documented boundary.

## Acceptance scenarios

- Ordering constraints produce a stable topology and cycles fail at startup.
- After hooks unwind in reverse order when the operation throws.
- Per-run capability state is isolated under concurrency.
- An observer failure cannot mutate or fail the run.
- A replacement hook cannot change call identity or prior permission outcome.
- Untrusted project configuration cannot load executable middleware.

## Upstream evidence

- Pydantic AI defines capability merge, per-run isolation, ordering constraints,
  and nested hooks under
  [`capabilities`](https://github.com/pydantic/pydantic-ai/tree/c0e4d824eaa0401d4481d401e5b3894ab32ab59d/pydantic_ai_slim/pydantic_ai/capabilities).
- Pi's extension runner exposes lifecycle, request, context, tool, and session
  hooks in
  [`extensions/types.ts`](https://github.com/badlogic/pi-mono/blob/9767ba275f3e9a5ee0f5c5342249b629ab1b2282/packages/coding-agent/src/core/extensions/types.ts).

## Related specifications

- [Provider request pipeline](provider-request-pipeline.md)
- [Observability and audit](observability-and-audit.md)
- [Public API and dependency injection](public-api-and-dependency-injection.md)
