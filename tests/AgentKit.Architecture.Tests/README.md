# Architecture test coverage

This project evaluates every project reference below `src` under the same
configuration that built the test assembly. It checks graph identity and
immutability, rejects missing or duplicate targets and directed cycles, and
enforces the documented inward dependency rules for `AgentKit.Abstractions`, the
`AgentKit` facade, shared `AgentKit.Observability`, and the named behavioral
runtime packages.

The checker deliberately does not validate leaf-to-leaf protocol ownership, the
runtime constructor/factory dependency graph, or XML documentation inside other
projects. Those boundaries require separate architecture decisions and checks;
this suite does not turn current provider edges into exceptions.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Architecture.Tests/AgentKit.Architecture.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Abstractions](../../src/AgentKit.Abstractions/README.md) — shared
  consumer contracts.
- [AgentKit](../../src/AgentKit/README.md) — engine composition and lifecycle.
- [AgentKit.Conformance](../AgentKit.Conformance/README.md) — reusable
  behavioral evidence.
- [Testing guide](../../docs/testing/index.md) — test layers and repository
  commands.
- [Project catalog](../../docs/packages/index.md) — all source and test
  projects.
