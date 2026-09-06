# Extensions

**Role:** Add or replace behavior at named boundaries without exposing mutable
runtime internals.

AgentKit has no universal plugin interface. Plugin is a composition property: an
implementation becomes a plugin by satisfying one or more narrow contracts and
being registered through dependency injection.

Extension contracts live beside the component boundaries in
AgentKit.Abstractions. There is no required AgentKit.Extensions implementation
package. Each feature package owns the middleware, hooks, contributors, or
strategies it supplies and registers them through its ServiceExtensions.

## Extension forms

The architecture distinguishes:

- capabilities that contribute instructions, tools, model settings, native
  features, or lifecycle behavior;
- middleware that wraps one named operation;
- hooks that observe or transform one documented boundary;
- event sinks that observe immutable activity; and
- strategies that replace one complete policy or operation.

An extension uses the narrowest form that fits. Generic callbacks with access to
everything are not a stable extensibility model.

## Named boundaries

Extension points may exist around run lifecycle, input admission, history
processing, context assembly, model selection, provider translation and send,
stream processing, tool preparation and execution, permissions, output
validation, compaction, and event publication.

Each boundary documents allowed inputs, replaceable fields, ordering, frequency,
cancellation, deadline, and failure policy. Extensions cannot change stable
identity, prior permission decisions, durable sequence, or committed state.

## Ordering and isolation

Registration order is the deterministic tie-breaker. Explicit before, after,
outer, inner, and dependency constraints form a validated ordering graph.
Missing dependencies and cycles fail during composition.

Shared extension definitions are immutable and thread-safe. Mutable capability
state is created per run. Extensions receive typed context views and explicit
dependencies, never the general service provider or unrestricted loop state.

## Failure and reload

Security-critical or transforming extensions fail their operation by default.
Best-effort observers are isolated and diagnosed. No extension can swallow
cancellation, denial, budget exhaustion, or protocol failure and turn it into
success.

Executable extension loading requires trusted host configuration and normally
occurs during composition. Validated declarative settings may reload at named
turn boundaries. Existing run-scoped instances continue with their captured
version until that boundary.

Skills primarily enter through the context component. If a skill package also
provides tools, middleware, or another implementation, those pieces register
through the same narrow contracts and receive no authority merely because they
arrived together.

## Related concept specifications

- [Extensions, hooks, and middleware](../concepts/extensions-hooks-and-middleware.md)
- [Architecture and dependency boundaries](../concepts/architecture-and-dependency-boundaries.md)
- [Public API and dependency injection](../concepts/public-api-and-dependency-injection.md)
