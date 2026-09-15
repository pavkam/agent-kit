# AgentKit.Tools.WebSearch

Search the web through an explicitly configured search provider.

Use this tool for bounded provider-backed search results. Supply the search
service and authority; the package does not invent a search account or endpoint.

## Use this project

Start with `AddWebSearchTool` in [ServiceExtensions.cs](ServiceExtensions.cs).
Read the overloads and XML documentation for required collaborators, lifetimes,
and duplicate-registration behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Tools](../AgentKit.Tools/README.md) — catalog, validate, authorize,
  and invoke application tools.
- [AgentKit.Tools.Web](../AgentKit.Tools.Web/README.md) — fetch web content
  through bounded network operations and content projection.
- [AgentKit.Network](../AgentKit.Network/README.md) — resolve network
  destinations and send bounded HTTP requests through security enforcement.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Tools.WebSearch.Tests](../../tests/AgentKit.Tools.WebSearch.Tests/README.md)
  — focused behavior and registration tests.
- [Component specification](../../docs/architecture/tools.md) — intended
  ownership and contracts.
- [Coding-harness tools](../../docs/profiles/coding-harness/coding-harness-built-in-tools.md)
  — the application profile for these features.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
