# AgentKit.Network

Resolve network destinations and send bounded HTTP requests through security
enforcement.

Use this host boundary for outbound operations that need explicit destination
checks, limits, and authorization. Tool and provider policies must still supply
the intended effect and authority.

## Use this project

Start with `AddAgentNetwork` in [ServiceExtensions.cs](ServiceExtensions.cs).
Read the overloads and XML documentation for required collaborators, lifetimes,
and duplicate-registration behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Network.InMemory](../AgentKit.Network.InMemory/README.md) — run
  deterministic network-resolution and response scenarios with security checks.
- [AgentKit.Tools.Web](../AgentKit.Tools.Web/README.md) — fetch web content
  through bounded network operations and content projection.
- [AgentKit.Permissions](../AgentKit.Permissions/README.md) — evaluate security
  requests and manage bounded grants, profiles, and audit dispatch.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Network.Tests](../../tests/AgentKit.Network.Tests/README.md) —
  focused behavior and registration tests.
- [Component specification](../../docs/architecture/network.md) — intended
  ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
