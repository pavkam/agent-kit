# AgentKit.Providers.AzureOpenAI.Tests

Focused tests for
[AgentKit.Providers.AzureOpenAI](../../src/AgentKit.Providers.AzureOpenAI/README.md).

**Component purpose:** connect AgentKit conversational models to Azure OpenAI
through GA v1 Chat Completions.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [AzureOpenAIAuthorizationHeaderFactoryTests](Authorization/AzureOpenAIAuthorizationHeaderFactoryTests.cs)
- [AzureOpenAIEmbeddingModelEndToEndTests](AzureOpenAIEmbeddingModelEndToEndTests.cs)
- [AzureOpenAILlmModelEndToEndTests](AzureOpenAILlmModelEndToEndTests.cs)
- [AzureOpenAIProviderDefaultsTests](AzureOpenAIProviderDefaultsTests.cs)
- [ServiceExtensionsTests](ServiceExtensionsTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance. Provider pipeline tests use controlled fixtures; `EndToEndTests`
does not mean a live provider account is required.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Providers.AzureOpenAI.Tests/AgentKit.Providers.AzureOpenAI.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Providers.AzureOpenAI](../../src/AgentKit.Providers.AzureOpenAI/README.md)
  — implementation and registration entry points.
- [Component specification](../../docs/architecture/model-and-embedding-providers.md)
  — intended contract and ownership.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
