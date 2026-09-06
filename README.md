# AgentKit

AgentKit is a composable .NET 10 framework for building agentic applications.
Agents, orchestration loops, model and embedding providers, tools, permissions,
memory, storage, queues, and goals are replaceable components composed through
dependency injection.

The repository currently contains the build foundation and architectural
contracts for the framework. Public APIs will be added behind focused
abstractions with reusable conformance tests.

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
Repository-specific workflows live in [`.agents/skills/`](.agents/skills/).

## Status

AgentKit is in its foundation phase. Package contracts and runtime behavior are
not stable yet.

## License

AgentKit is licensed under the [MIT License](LICENSE).
