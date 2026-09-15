# AgentKit.Session

Session discovery uses `ISessionCoordinator.ListAsync` over the singular,
explicitly selected `ISessionDirectory`. Pages are ordered by `SessionId`, use
an exclusive typed cursor, and remain scoped to the exact tenant, principal, and
agent. The coordinator authorizes the directory scan; it never probes registered
stores. Directories that cannot enumerate return a typed unavailable result
through the additive default interface implementation.

Coordinate session lifecycle, run ownership, branching, and store routing.

Use this package with an explicitly selected session store. It owns coordination
through session contracts while the backend determines persistence and
consistency behavior.

## Use this project

Start with `AddAgentSession`, `AddSessionStore`, `AddSessionEventSink` in
[ServiceExtensions.cs](ServiceExtensions.cs). Read the overloads and XML
documentation for required collaborators, lifetimes, and duplicate-registration
behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Session.InMemory](../AgentKit.Session.InMemory/README.md) — keep
  session records and directory state in memory for ephemeral applications.
- [AgentKit.Context](../AgentKit.Context/README.md) — assemble provider-ready
  context while preserving message trust and tool-call correlation.
- [AgentKit.IO](../AgentKit.IO/README.md) — provide input-promotion policy and a
  broker for bounded human questions.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Session.Tests](../../tests/AgentKit.Session.Tests/README.md) —
  focused behavior and registration tests.
- [Component specification](../../docs/architecture/sessions.md) — intended
  ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
