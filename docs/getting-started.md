# Getting started

This walkthrough runs the smallest complete AgentKit agent, explains what each
line composes, and shows where tools, other providers, durable storage, and your
own security policies plug in.

The finished program lives in the repository as
[`examples/QuickStart`](../examples/QuickStart/README.md) and is exercised by
its own tests, so it always compiles and completes a turn against the current
libraries.

## Current status

AgentKit targets .NET 10 and is currently versioned `0.1.0-alpha.2` in
[Directory.Build.props](../Directory.Build.props). Work from a source checkout
when evaluating these instructions. The repository's version number does not
establish availability on a public package feed.

The path below is the direct, in-process one: one conversation with one agent,
composed by `AgentKit.Simple` over `AgentKit.Conversations`. The same
`AgentEngine` hosts further agents and many concurrent sessions through
`Agent.SendAsync`; queue-backed input admission is still tracked in the
[run envelope and admission workstream](workstreams/run-envelope-and-admission.md).

## Set up the repository

Install the SDK selected by [global.json](../global.json), currently .NET
10.0.203 with compatible patch roll-forward. Node.js 22 or later and GNU Make
are needed for repository tooling.

```sh
git clone https://github.com/pavkam/agent-kit.git
cd agent-kit
make restore
make build
```

## Run the quick start

```sh
export OPENAI_API_KEY=sk-...
dotnet run --project examples/QuickStart -- "In one sentence, what is AgentKit?"
```

Credentials are never fabricated as defaults: without the variable the program
stops before composing anything.

## The whole agent

```csharp
await using var engine = AgentEngine.CreateBuilder()
    .UseLocalDevelopmentDefaults()
    .UseOpenAI(apiKey, "gpt-4o-mini")
    .WithInstructions("You are a concise assistant.")
    .Build();

Console.WriteLine(await engine.AskAsync("In one sentence, what is AgentKit?"));
```

`AskAsync` sends one user message, runs the agent loop until it produces a final
answer or hits a limit, and returns the assistant's text. Call it again on the
same agent to continue the conversation. When a turn ends without a final answer
it throws `SimpleAgentException` carrying the safe reason and the committed
events; it never carries provider diagnostics or your key.

## What each line composes

This is the real `AgentEngineBuilder` and the real `AgentEngine`; the
`AgentKit.Simple` package adds the `Use*`/`With*` calls and `AskAsync` as
extensions. There is no second runtime: each call is sugar over the public
registrations you could write by hand on `builder.Services`:

| Call                            | Registers                                                                                                                                                                                                                                                                                                                   |
| ------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `UseLocalDevelopmentDefaults()` | In-memory session store and directory, in-memory grant store, a standalone security profile with best-effort audit, an allow-all policy, every registered tool allowed, and a basic-assurance identity for the process user                                                                                                 |
| `UseOpenAI(apiKey, modelId)`    | The provider catalog, the OpenAI adapter, the API-key credential, and a catalog descriptor whose context window, output limit, capabilities, and list prices come from the bundled [known-model catalog](../src/AgentKit.Providers/README.md#known-model-catalog)                                                           |
| `WithInstructions(text)`        | One system message, in call order                                                                                                                                                                                                                                                                                           |
| `Build()`                       | The engine, with one published `AgentDefinition` and its run-profile publication; the first sugar call had already registered session coordination, context assembly, output processing, the turn loop, the tool runtime, security, and the conversation. The provider is built with `ValidateOnBuild` and `ValidateScopes` |

`UseLocalDevelopmentDefaults` is named for what it is. Nothing survives the
process, every request is permitted, and the identity is the process user. It is
the right choice for a local tool and the wrong one for a service; `Build()`
fails with a diagnostic naming the missing registration if you skip it without
supplying your own.

## Grow the agent

Each of these is one more line on the same builder; each has its own guide.

- **Let it work on files.** `.UseWorkspace("/path/to/project")` adds a sandboxed
  file system and the read, list, glob, search, write, and edit tools over it.
  See [Working with files](guides/file-system.md).
- **Keep conversations across restarts.**
  `.UseSqliteSessions("/var/lib/myapp/sessions.db")` moves sessions to SQLite;
  `engine.Conversation.ListAsync` and `OpenAsync` find and resume them. See
  [Storing conversations](guides/storage.md).
- **Decide what it may do.** Add your own `ISecurityPolicy` on
  `builder.Services`; deny wins over the local allow-all default, and
  `RequireApproval` puts a human in the loop. See
  [Permissions and approvals](guides/permissions.md).
- **Get a typed answer.** `.WithOutput<Triage>(schemaJson)` makes every final
  answer JSON that validates against the schema, and
  `await engine.AskAsync<Triage>(question)` returns it deserialized. Invalid
  answers are sent back to the model for repair before the turn fails.
- **Stream the answer.** Pass an `IConversationEventObserver` to
  `engine.SendAsync(text, observer)` to receive text and reasoning deltas, tool
  starts and results, and usage before the call returns.
- **Use another provider.** `UseAnthropic(apiKey, modelId)`,
  `UseOllama(modelId)`, `UseOpenRouter(apiKey, modelId)`, and
  `UseAzureOpenAI(endpoint, apiKey, deploymentId, modelId)` replace `UseOpenAI`
  one for one. For any other provider, or a model outside the known-model
  catalog, register that provider's services on `builder.Services` (each has an
  `Add<Provider>KnownLlmModel` where the catalog covers it) and select the alias
  with `UseModel`. See [Composing an application](guides/composition.md).
- **Host several agents.** `.AddAgent(agentId, o => o.Instructions.Add(...))`
  publishes a second definition on the same engine. Drive it with
  `var agent = await engine.GetAgentAsync(agentId)` and
  `agent.SendAsync(new AgentSendRequest(engine.Identity, text, sessionId))`;
  each turn names the session it ran in, and different sessions run
  concurrently.

To see the behavior this walkthrough relies on under test, run:

```sh
dotnet test --project tests/AgentKit.Simple.Tests --configuration Release --timeout 300s
```
