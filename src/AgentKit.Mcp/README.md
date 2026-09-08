# AgentKit.Mcp

Describe reflected MCP tool contracts and keep protocol and tool-version
identities explicit.

Use this shared layer when defining typed tool surfaces consumed by the MCP
client or server packages. Transport and connection lifecycle belong to those
integrations.

## Use this project

Start with `AddMcpToolContract` in [ServiceExtensions.cs](ServiceExtensions.cs).
Read the overloads and XML documentation for required collaborators, lifetimes,
and duplicate-registration behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable component
example, follow [Getting started](../../docs/getting-started.md). Complete
engine composition is described in the
[composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Mcp.Client](../AgentKit.Mcp.Client/README.md) — expose remote MCP
  tools through reflected, typed client surfaces.
- [AgentKit.Mcp.Server](../AgentKit.Mcp.Server/README.md) — expose reflected
  tool classes through an MCP server.
- [AgentKit.Abstractions](../AgentKit.Abstractions/README.md) — implement
  AgentKit extensions against provider-neutral contracts and typed domain
  values.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Mcp.Tests](../../tests/AgentKit.Mcp.Tests/README.md) — focused
  behavior and registration tests.
- [Component specification](../../docs/architecture/mcp.md) — intended ownership
  and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
