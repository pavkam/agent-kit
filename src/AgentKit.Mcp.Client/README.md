# AgentKit.Mcp.Client

Expose remote MCP tools through reflected, typed client surfaces.

Use this integration when an application consumes an MCP server. Configure
connection and transport behavior explicitly, and retain the normal security
boundary around protected operations.

## Use this project

Start with `AddMcpToolClient` in [ServiceExtensions.cs](ServiceExtensions.cs).
Read the overloads and XML documentation for required collaborators, lifetimes,
and duplicate-registration behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Mcp](../AgentKit.Mcp/README.md) — describe reflected MCP tool
  contracts and keep protocol and tool-version identities explicit.
- [AgentKit.Mcp.Server](../AgentKit.Mcp.Server/README.md) — expose reflected
  tool classes through an MCP server.
- [AgentKit.Tools](../AgentKit.Tools/README.md) — catalog, validate, authorize,
  and invoke application tools.

Direct project references: [AgentKit.Mcp](../AgentKit.Mcp/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Mcp.Client.Tests](../../tests/AgentKit.Mcp.Client.Tests/README.md) —
  focused behavior and registration tests.
- [Component specification](../../docs/architecture/mcp.md) — intended ownership
  and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
