# AgentKit.Output.Tests

Focused tests for [AgentKit.Output](../../src/AgentKit.Output/README.md).

**Component purpose:** resolve output definitions and validate, repair, or
deserialize terminal candidates.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [BoundedHashStreamTests](BoundedHashStreamTests.cs)
- [BoundedJsonSerializerTests](BoundedJsonSerializerTests.cs)
- [DefaultOutputProcessorTests](DefaultOutputProcessorTests.cs)
- [InMemoryOutputDefinitionRegistryTests](InMemoryOutputDefinitionRegistryTests.cs)
- [StructuralOutputSchemaEngineTests](StructuralOutputSchemaEngineTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Output.Tests/AgentKit.Output.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Output](../../src/AgentKit.Output/README.md) — implementation and
  registration entry points.
- [Component specification](../../docs/architecture/structured-output.md) —
  intended contract and ownership.
- [AgentKit.Conformance](../AgentKit.Conformance/README.md) — shared behavioral
  suites.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
