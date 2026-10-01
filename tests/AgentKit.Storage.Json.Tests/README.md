# AgentKit.Storage.Json.Tests

Focused tests for
[AgentKit.Storage.Json](../../src/AgentKit.Storage.Json/README.md).

**Component purpose:** shared JSON and JSONL storage machinery for the `.Json`
adapters.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [JsonAtomicDocumentTests](JsonAtomicDocumentTests.cs)
- [JsonAuthenticationEvidenceTests](JsonAuthenticationEvidenceTests.cs)
- [JsonDelegationIdentityLinkTests](JsonDelegationIdentityLinkTests.cs)
- [JsonEncodingOptionsTests](JsonEncodingOptionsTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Storage.Json.Tests/AgentKit.Storage.Json.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Storage.Json](../../src/AgentKit.Storage.Json/README.md) —
  implementation and registration entry points.
- [Project structure](../../docs/architecture/project-structure.md) — intended
  contract and ownership.
- [AgentKit.Test.Shared](../AgentKit.Test.Shared/README.md) — shared test
  fixtures and observation helpers.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
