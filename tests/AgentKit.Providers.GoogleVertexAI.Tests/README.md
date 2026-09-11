# AgentKit.Providers.GoogleVertexAI.Tests

Focused tests for
[AgentKit.Providers.GoogleVertexAI](../../src/AgentKit.Providers.GoogleVertexAI/README.md).

**Component purpose:** connect AgentKit conversational models to Google Vertex
AI through its generateContent surface.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [GoogleVertexAIAuthorizationHeaderFactoryTests](Authorization/GoogleVertexAIAuthorizationHeaderFactoryTests.cs)
- [GoogleVertexAIEmbeddingModelTests](GoogleVertexAIEmbeddingModelTests.cs)
- [GoogleVertexAIEmbeddingRequestTranslatorTests](Translation/GoogleVertexAIEmbeddingRequestTranslatorTests.cs)
- [GoogleVertexAIEmbeddingResponseParserTests](Parsing/GoogleVertexAIEmbeddingResponseParserTests.cs)
- [GoogleVertexAILlmModelTests](GoogleVertexAILlmModelTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance. Provider pipeline tests use controlled fixtures and do not require
a live provider account.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Providers.GoogleVertexAI.Tests/AgentKit.Providers.GoogleVertexAI.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Providers.GoogleVertexAI](../../src/AgentKit.Providers.GoogleVertexAI/README.md)
  — implementation and registration entry points.
- [Component specification](../../docs/architecture/model-and-embedding-providers.md)
  — intended contract and ownership.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
