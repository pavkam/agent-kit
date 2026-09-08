# AgentKit.Providers.OpenAI.Tests

Focused tests for
[AgentKit.Providers.OpenAI](../../src/AgentKit.Providers.OpenAI/README.md).

**Component purpose:** connect AgentKit conversational models to OpenAI through
Chat Completions.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [OpenAIEmbeddingModelEndToEndTests](OpenAIEmbeddingModelEndToEndTests.cs)
- [OpenAILlmModelEndToEndTests](OpenAILlmModelEndToEndTests.cs)
- [OpenAIProviderDefaultsTests](OpenAIProviderDefaultsTests.cs)
- [ProviderCredentialCoexistenceTests](ProviderCredentialCoexistenceTests.cs)
- [ServiceExtensionsTests](ServiceExtensionsTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance. Provider pipeline tests use controlled fixtures; `EndToEndTests`
does not mean a live provider account is required.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Providers.OpenAI.Tests/AgentKit.Providers.OpenAI.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Providers.OpenAI](../../src/AgentKit.Providers.OpenAI/README.md) —
  implementation and registration entry points.
- [Component specification](../../docs/architecture/model-and-embedding-providers.md)
  — intended contract and ownership.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
