# AgentKit.Providers.Groq.Tests

Focused tests for
[AgentKit.Providers.Groq](../../src/AgentKit.Providers.Groq/README.md).

**Component purpose:** connect AgentKit conversational models to Groq through
its OpenAI-compatible chat surface.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [GroqLlmModelTests](GroqLlmModelTests.cs)
- [GroqProviderDefaultsTests](GroqProviderDefaultsTests.cs)
- [ServiceExtensionsTests](ServiceExtensionsTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance. Provider pipeline tests use controlled fixtures and do not require
a live provider account.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Providers.Groq.Tests/AgentKit.Providers.Groq.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Providers.Groq](../../src/AgentKit.Providers.Groq/README.md) —
  implementation and registration entry points.
- [Component specification](../../docs/architecture/model-and-embedding-providers.md)
  — intended contract and ownership.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
