# AgentKit.Mcp.Tests

Focused tests for [AgentKit.Mcp](../../src/AgentKit.Mcp/README.md).

**Component purpose:** describe reflected MCP tool contracts and keep protocol
and tool-version identities explicit.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [McpProtocolVersionTests](McpProtocolVersionTests.cs)
- [McpToolAttributeTests](McpToolAttributeTests.cs)
- [McpToolContractTests](McpToolContractTests.cs)
- [McpToolNameTests](McpToolNameTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Mcp.Tests/AgentKit.Mcp.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Mcp](../../src/AgentKit.Mcp/README.md) — implementation and
  registration entry points.
- [Component specification](../../docs/architecture/mcp.md) — intended contract
  and ownership.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
