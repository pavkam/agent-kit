# AgentKit.Providers.Anthropic.Tests

Focused tests for
[AgentKit.Providers.Anthropic](../../src/AgentKit.Providers.Anthropic/README.md).

**Component purpose:** connect AgentKit conversational models to Anthropic
Claude through Messages.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [AnthropicAuthorizationHeaderFactoryTests](Authorization/AnthropicAuthorizationHeaderFactoryTests.cs)
- [AnthropicLlmModelEndToEndTests](AnthropicLlmModelEndToEndTests.cs)
- [AnthropicMessageStreamParserBufferedTests](Parsing/AnthropicMessageStreamParserBufferedTests.cs)
- [AnthropicMessageStreamParserStreamingTests](Parsing/AnthropicMessageStreamParserStreamingTests.cs)
- [AnthropicMessageTranslatorTests](Translation/AnthropicMessageTranslatorTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance. Provider pipeline tests use controlled fixtures; `EndToEndTests`
does not mean a live provider account is required.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Providers.Anthropic.Tests/AgentKit.Providers.Anthropic.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Providers.Anthropic](../../src/AgentKit.Providers.Anthropic/README.md)
  — implementation and registration entry points.
- [Component specification](../../docs/architecture/model-and-embedding-providers.md)
  — intended contract and ownership.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
