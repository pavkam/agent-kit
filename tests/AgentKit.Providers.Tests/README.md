# AgentKit.Providers.Tests

Focused tests for [AgentKit.Providers](../../src/AgentKit.Providers/README.md).

**Component purpose:** catalog configured models, validate capabilities, and
select or resolve model implementations.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [DefaultEmbeddingModelResolverTests](DefaultEmbeddingModelResolverTests.cs)
- [DefaultLlmModelResolverTests](DefaultLlmModelResolverTests.cs)
- [DefaultModelCapabilityValidatorTests](DefaultModelCapabilityValidatorTests.cs)
- [DefaultModelCatalogTests](DefaultModelCatalogTests.cs)
- [DefaultModelSelectorTests](DefaultModelSelectorTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Providers.Tests/AgentKit.Providers.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Providers](../../src/AgentKit.Providers/README.md) — implementation
  and registration entry points.
- [Component specification](../../docs/architecture/model-and-embedding-providers.md)
  — intended contract and ownership.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
