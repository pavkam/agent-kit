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

## No-compatibility guard

`ObsoleteSurfaceTests` enforces the `AGENTS.md` rule that AgentKit keeps no
backwards-compatibility surface. It scans every `.cs` and `.verified.txt` file
below `src`, `tests`, and `examples` (excluding its own `ObsoleteSurface*`
files) for `[Obsolete]` attributes, `Legacy[A-Z]` identifiers,
`#pragma warning disable` of `CS0612`/`CS0618`, and
`compatibility-created`/`unpinned` remarks. `ObsoleteSurfaceBaseline.txt` is
empty and may only shrink; any new occurrence fails the guard.
`ObsoleteSurfaceAllowList.txt` lists occurrences that describe an external or
domain fact, each with a written reason, and an entry that no longer matches
fails too. Neither file may be loosened to make a change pass.

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
