# AgentKit.Providers.Ollama.Tests

Focused tests for
[AgentKit.Providers.Ollama](../../src/AgentKit.Providers.Ollama/README.md).

**Component purpose:** connect AgentKit conversational models to Ollama through
its OpenAI-compatible endpoint.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [OllamaEmbeddingModelTests](OllamaEmbeddingModelTests.cs)
- [OllamaLlmModelTests](OllamaLlmModelTests.cs)
- [OllamaProviderDefaultsTests](OllamaProviderDefaultsTests.cs)
- [ServiceExtensionsTests](ServiceExtensionsTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance. Provider pipeline tests use controlled fixtures and do not require
a live provider account.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Providers.Ollama.Tests/AgentKit.Providers.Ollama.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Providers.Ollama](../../src/AgentKit.Providers.Ollama/README.md) —
  implementation and registration entry points.
- [Component specification](../../docs/architecture/model-and-embedding-providers.md)
  — intended contract and ownership.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
