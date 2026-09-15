# AgentKit.Network.InMemory

Run deterministic network-resolution and response scenarios with security
checks.

Use this adapter to test network consumers without external services. Scripted
responses exercise the boundary contract without proving the behavior of a
production network stack.

## Use this project

Start with `AddAgentNetworkInMemory` in
[ServiceExtensions.cs](ServiceExtensions.cs). Read the overloads and XML
documentation for required collaborators, lifetimes, and duplicate-registration
behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Network](../AgentKit.Network/README.md) — resolve network
  destinations and send bounded HTTP requests through security enforcement.
- [AgentKit.Tools.Web](../AgentKit.Tools.Web/README.md) — fetch web content
  through bounded network operations and content projection.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Network.InMemory.Tests](../../tests/AgentKit.Network.InMemory.Tests/README.md)
  — focused behavior and registration tests.
- [Component specification](../../docs/architecture/network.md) — intended
  ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
