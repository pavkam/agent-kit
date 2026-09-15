# AgentKit

Build agentic applications in C# with explicit control over models, tools,
permissions, and runtime behavior.

[![CI](https://img.shields.io/badge/CI-GitHub_Actions-2088FF?logo=githubactions&logoColor=white)](https://github.com/pavkam/agent-kit/actions/workflows/ci.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](global.json)
[![Status: alpha](https://img.shields.io/badge/status-alpha-orange)](docs/getting-started.md#current-status)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue)](LICENSE)
[![Issues](https://img.shields.io/badge/issues-GitHub-181717?logo=github)](https://github.com/pavkam/agent-kit/issues)

AgentKit is a collection of .NET 10 libraries composed through standard
dependency injection. Choose the components your application needs, configure
their behavior, and replace them through provider-neutral contracts.

**AgentKit is in alpha.** Implementations and tests are present across the
runtime, providers, tools, and storage boundaries, but the complete architecture
is still being implemented. Public APIs can change. Start from this repository
and check the [current status](docs/getting-started.md#current-status) before
depending on a feature.

## Why AgentKit

- **Compose with familiar .NET tools.** Use `IServiceCollection`, typed options,
  logging, and standard host lifecycle conventions.
- **Choose providers by capability.** Conversational models and embeddings have
  separate contracts. Tool calling, streaming, and structured output are
  explicit capabilities of each configured model.
- **Keep control of effects.** Tools, network requests, filesystem access, and
  process execution have dedicated permission and enforcement boundaries.
- **Share an engine across agents.** The engine hosts immutable agent
  definitions; session and run state have separate owners.
- **Inspect the behavior you depend on.** Component specifications, focused
  tests, reusable conformance suites, and public API snapshots live with the
  code.

## Start here

You need the .NET SDK selected by [global.json](global.json), Node.js 22 or
later, and GNU Make to work with the repository.

```sh
git clone https://github.com/pavkam/agent-kit.git
cd agent-kit
make restore
make build
```

Then set an OpenAI key and run the smallest complete agent:

```sh
export OPENAI_API_KEY=sk-...
dotnet run --project examples/QuickStart -- "In one sentence, what is AgentKit?"
```

The whole agent is a handful of lines:

```csharp
await using var engine = AgentEngine.CreateBuilder()
    .UseLocalDevelopmentDefaults()          // in-memory state, allow-all policy, local identity: named, never implicit
    .UseOpenAI(apiKey, "gpt-4o-mini")       // adapter, credential, and catalog descriptor in one call
    .WithInstructions("You are a concise assistant.")
    .Build();

Console.WriteLine(await engine.AskAsync("In one sentence, what is AgentKit?"));
```

That is the real `AgentEngineBuilder` and `AgentEngine`; `AgentKit.Simple` adds
the `Use*`/`With*` calls and `AskAsync` as extensions. There is no second
runtime: each call is sugar over the public DI registrations of the loop,
session, security, and provider packages, and `builder.Services` is the same
`IServiceCollection` they land on, so tools, other providers, durable storage,
and your own security policies are ordinary registrations away. The
[getting-started guide](docs/getting-started.md) explains each line and the
escape hatches; the [CodingAgent](examples/CodingAgent/README.md) example is the
long form of the same composition with SQLite sessions, an approval broker, and
eight tools.

## Choose your components

| You need to…                              | Start with                                                                                                                                    |
| ----------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| Build your first agent                    | [AgentKit.Simple](src/AgentKit.Simple/README.md)                                                                                              |
| Compose and host several agents           | [AgentKit](src/AgentKit/README.md)                                                                                                            |
| Implement an extension                    | [AgentKit.Abstractions](src/AgentKit.Abstractions/README.md)                                                                                  |
| Coordinate turns and continuation         | [Loop](src/AgentKit.Loop/README.md), [Context](src/AgentKit.Context/README.md), and [Output](src/AgentKit.Output/README.md)                   |
| Drive one conversational turn in one call | [Conversations](src/AgentKit.Conversations/README.md)                                                                                         |
| Select a model or provider                | [Provider catalog](src/AgentKit.Providers/README.md) and [provider packages](docs/packages/index.md#model-providers)                          |
| Add application tools                     | [Tool runtime](src/AgentKit.Tools/README.md) and [tool packages](docs/packages/index.md#tools)                                                |
| Control access and spending               | [Permissions](src/AgentKit.Permissions/README.md), [Identity](src/AgentKit.Identity/README.md), and [Budgets](src/AgentKit.Budgets/README.md) |
| Manage conversation and generated content | [Session](src/AgentKit.Session/README.md) and [Artifacts](src/AgentKit.Artifacts/README.md)                                                   |
| Connect MCP tools                         | [MCP client](src/AgentKit.Mcp.Client/README.md) and [MCP server](src/AgentKit.Mcp.Server/README.md)                                           |

The [complete project catalog](docs/packages/index.md) covers every source and
test project, with descriptions and links. The facade has no implicit loop,
provider, session store, or tool dependency; your application chooses them.

## Find your way around

| You want to…                              | Read                                                            |
| ----------------------------------------- | --------------------------------------------------------------- |
| Run your first agent                      | [Getting started](docs/getting-started.md)                      |
| Understand how the pieces fit             | [Composing an application](docs/guides/composition.md)          |
| Pick packages and follow related projects | [Project catalog](docs/packages/index.md)                       |
| Navigate the documentation                | [Documentation home](docs/index.md)                             |
| Check provider wire contracts             | [Provider reference](docs/providers/index.md)                   |
| Understand a behavioral contract          | [Concept specifications](docs/concepts/index.md)                |
| Design an extension                       | [Architecture](docs/architecture/index.md)                      |
| Build a coding assistant                  | [Coding-harness profile](docs/profiles/coding-harness/index.md) |
| Run or extend the tests                   | [Testing guide](docs/testing/index.md)                          |

## Contributing

Bug reports, documentation fixes, and focused contributions are welcome. Read
[Contributing](CONTRIBUTING.md) for setup, testing, and the pull request
workflow, and follow the [Code of Conduct](CODE_OF_CONDUCT.md) in project
spaces.

For questions and reproducible bugs, use the
[issue tracker](https://github.com/pavkam/agent-kit/issues). Include the commit
you tried, your .NET SDK version, and a small reproduction.

AgentKit is licensed under the [MIT License](LICENSE).
