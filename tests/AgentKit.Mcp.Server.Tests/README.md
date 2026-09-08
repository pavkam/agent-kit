# AgentKit.Mcp.Server.Tests

Focused tests for
[AgentKit.Mcp.Server](../../src/AgentKit.Mcp.Server/README.md).

**Component purpose:** expose reflected tool classes through an MCP server.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [McpClientServerIntegrationTests](McpClientServerIntegrationTests.cs)
- [ServiceExtensionsTests](ServiceExtensionsTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Mcp.Server.Tests/AgentKit.Mcp.Server.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Mcp.Server](../../src/AgentKit.Mcp.Server/README.md) —
  implementation and registration entry points.
- [Component specification](../../docs/architecture/mcp.md) — intended contract
  and ownership.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
