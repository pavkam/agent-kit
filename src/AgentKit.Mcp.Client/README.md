# AgentKit.Mcp.Client

Expose remote MCP tools through reflected, typed client surfaces.

Use this integration when an application consumes an MCP server. Configure
connection and transport behavior explicitly, and retain the normal security
boundary around protected operations.

## Use this project

Start with `AddMcpClient` and `AddMcpToolSource` in
[ServiceExtensions.cs](ServiceExtensions.cs) for engine-owned sessions,
transports, and `IToolProvider` discovery. `AddMcpToolClient<TTools>` is the
separate reflected-typed-client surface over a caller-supplied SDK transport.
Read the overload XML for required security, store, and catalog collaborators.

HTTP endpoints (`AddMcpHttpEndpoint`) route every exchange of the official SDK
transport through `INetworkNameResolver` and `INetworkTransport` under
per-exchange resolution and send grants; register
[AgentKit.Network](../AgentKit.Network/README.md) (or a replacement) first. The
SDK never opens a socket or follows a redirect, and requests to any origin other
than the configured endpoint's are refused. Stdio endpoints
(`AddMcpStdioEndpoint`) go through the process boundary instead; each endpoint
kind registers only its own transport factory.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Mcp](../AgentKit.Mcp/README.md) — describe reflected MCP tool
  contracts and keep protocol and tool-version identities explicit.
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

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
