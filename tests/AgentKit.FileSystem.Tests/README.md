# AgentKit.FileSystem.Tests

Focused tests for
[AgentKit.FileSystem](../../src/AgentKit.FileSystem/README.md).

**Component purpose:** access files through a root-bounded implementation of the
filesystem contract.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [SandboxedFileSystemEditTests](SandboxedFileSystemEditTests.cs)
- [SandboxedFileSystemPatchTests](SandboxedFileSystemPatchTests.cs)
- [SandboxedFileSystemSearchTests](SandboxedFileSystemSearchTests.cs)
- [SandboxedFileSystemTests](SandboxedFileSystemTests.cs)
- [ServiceExtensionsTests](ServiceExtensionsTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.FileSystem.Tests/AgentKit.FileSystem.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.FileSystem](../../src/AgentKit.FileSystem/README.md) —
  implementation and registration entry points.
- [Component specification](../../docs/architecture/file-system.md) — intended
  contract and ownership.
- [AgentKit.Test.Shared](../AgentKit.Test.Shared/README.md) — shared test
  fixtures and observation helpers.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
