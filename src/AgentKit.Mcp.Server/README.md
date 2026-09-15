# AgentKit.Mcp.Server

Expose reflected tool classes through an MCP server.

Use this integration to compose server tools with the official MCP server
builder. Select transport and protocol-version policy explicitly; tool contract
versions remain separate from wire revisions.

## Use this project

Start with `AddAgentKitMcpServer` in
[ServiceExtensions.cs](ServiceExtensions.cs). Read the overloads and XML
documentation for required collaborators, lifetimes, and duplicate-registration
behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Mcp](../AgentKit.Mcp/README.md) — describe reflected MCP tool
  contracts and keep protocol and tool-version identities explicit.
- [AgentKit.Mcp.Client](../AgentKit.Mcp.Client/README.md) — expose remote MCP
  tools through reflected, typed client surfaces.
- [AgentKit.Tools](../AgentKit.Tools/README.md) — catalog, validate, authorize,
  and invoke application tools.

Direct project references: [AgentKit.Mcp](../AgentKit.Mcp/README.md). Other
related projects above are composition collaborators, not necessarily
dependencies.

## Tests and reference

- [AgentKit.Mcp.Server.Tests](../../tests/AgentKit.Mcp.Server.Tests/README.md) —
  focused behavior and registration tests.
- [Component specification](../../docs/architecture/mcp.md) — intended ownership
  and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
