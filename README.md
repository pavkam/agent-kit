# AgentKit

AgentKit is a composable .NET 10 framework for building agentic applications.
Agents, orchestration loops, model and embedding providers, tools, permissions,
memory, storage, queues, and goals are replaceable components composed through
dependency injection.

The `AgentKit` package is a dependency-light facade. `AgentEngine` and its
builder compose explicitly selected feature packages without pulling in a loop,
provider, store, policy, or tool transitively. The same service registrations
work in standalone engines and standard .NET hosts.

The repository currently contains the build foundation and architectural
contracts for the framework. Public APIs will be added behind focused
abstractions with reusable conformance tests.

Provider support is split deliberately. `AgentKit.Providers` will supply the
provider-neutral catalog and selector; `AgentKit.Providers.OpenAICompatible`
will supply reusable wire machinery; and concrete packages such as
`AgentKit.Providers.OpenAI`, `AgentKit.Providers.OpenRouter`, and
`AgentKit.Providers.ZAi` will own their actual capabilities and registration.
OpenAI and OpenRouter include independent embedding registrations; OpenRouter
also includes reranking.

## Requirements

- .NET SDK 10.0.203 or a compatible 10.0 patch selected by `global.json`
- Node.js 22 or later for repository documentation tooling
- GNU Make

## Development

```bash
make restore
make format
make lint
make build
make test
```

Read [AGENTS.md](AGENTS.md) before changing architecture or behavior.
Repository-specific workflows live in [`.agents/skills/`](.agents/skills/). The
[architecture index](docs/architecture/index.md) describes the component
boundaries, and [project structure](docs/architecture/project-structure.md)
records the planned solution layout.

## Status

AgentKit is in its foundation phase. Package contracts and runtime behavior are
not stable yet.

## License

AgentKit is licensed under the [MIT License](LICENSE).
