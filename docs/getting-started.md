# Getting started

This walkthrough runs the smallest complete AgentKit agent, explains what each
line composes, and shows where tools, other providers, durable storage, and your
own security policies plug in.

The finished program lives in the repository as
[`examples/QuickStart`](../examples/QuickStart/README.md) and is exercised by
its own tests, so it always compiles and completes a turn against the current
libraries.

## Current status

AgentKit targets .NET 10 and is currently versioned `0.1.0-alpha.1` in
[Directory.Build.props](../Directory.Build.props). Work from a source checkout
when evaluating these instructions. The repository's version number does not
establish availability on a public package feed.

The path below is the direct, in-process one: one conversation with one agent,
composed by `AgentKit.Simple` over `AgentKit.Conversations`. The `AgentEngine`
facade that hosts a catalog of several agents with queue-backed input admission
exists but its complete runnable graph is still tracked in the
[implementation ledger](implementation-progress.md#component-coverage).

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
using var agent = SimpleAgentBuilder.Create()
    .UseLocalDevelopmentDefaults()
    .UseOpenAI(apiKey, "gpt-4o-mini")
    .WithInstructions("You are a concise assistant.")
    .Build();

Console.WriteLine(await agent.AskAsync("In one sentence, what is AgentKit?"));
```

`AskAsync` sends one user message, runs the agent loop until it produces a final
answer or hits a limit, and returns the assistant's text. Call it again on the
same agent to continue the conversation. When a turn ends without a final answer
it throws `SimpleAgentException` carrying the safe reason and the committed
events; it never carries provider diagnostics or your key.

## What each line composes

There is no second runtime behind the builder. Each call is sugar over the
public registrations you could write by hand on `builder.Services`:

| Call                            | Registers                                                                                                                                                                                                                                                         |
| ------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `UseLocalDevelopmentDefaults()` | In-memory session store and directory, in-memory grant store, a standalone security profile with best-effort audit, an allow-all policy, every registered tool allowed, and a basic-assurance identity for the process user                                       |
| `UseOpenAI(apiKey, modelId)`    | The provider catalog, the OpenAI adapter, the API-key credential, and a catalog descriptor whose context window, output limit, capabilities, and list prices come from the bundled [known-model catalog](../src/AgentKit.Providers/README.md#known-model-catalog) |
| `WithInstructions(text)`        | One system message, in call order                                                                                                                                                                                                                                 |
| `Build()`                       | Session coordination, context assembly, output processing, the turn loop, the tool runtime, and the conversation; then a service provider built with `ValidateOnBuild` and `ValidateScopes`                                                                       |

`UseLocalDevelopmentDefaults` is named for what it is. Nothing survives the
process, every request is permitted, and the identity is the process user. It is
the right choice for a local tool and the wrong one for a service; `Build()`
fails with a diagnostic naming the missing registration if you skip it without
supplying your own.

## Grow the agent

`builder.Services` is the same `IServiceCollection` every registration lands on,
so everything beyond the sugar is ordinary AgentKit composition.

- **Give it tools.** Register a tool package and the host boundary it needs; the
  builder advertises every registered tool to the model:

  ```csharp
  builder.Services.AddSandboxedFileSystem(workspaceRoot);
  builder.Services.AddReadTool();
  ```

  Registering a tool does not grant access to its resources; the security policy
  still decides. The [CodingAgent](../examples/CodingAgent/README.md) example
  composes eight tools this way with a real approval flow.

- **Stream the answer.** Pass an `IConversationEventObserver` to
  `agent.SendAsync(text, observer)` to receive text and reasoning deltas, tool
  starts and results, and usage before the call returns.

- **Use another provider or an unknown model.** Register that provider's
  services on `builder.Services` and select the alias with `UseModel`:

  ```csharp
  builder.Services.AddAnthropic().AddAnthropicApiKeyCredential(key)
      .AddAnthropicLlmModel(new ModelAlias("claude"), new ModelId("claude-sonnet-4-5"));
  builder.UseModel(new ModelAlias("claude"));
  ```

  `KnownModelCatalog.Default.TryFind(providerId, modelId)` supplies the limits
  and prices for that descriptor.

- **Keep conversations across restarts.** Skip `UseLocalDevelopmentDefaults`,
  register `AddSqliteSessionStore` and `AddSqliteSessionDirectory` together with
  your security services, and call `WithIdentity`.
  `agent.Conversation.OpenAsync(sessionId)` then resumes a persisted
  conversation.

- **Host several agents.** The [composition guide](guides/composition.md)
  explains the `AgentEngine` facade, its lifetimes, and the collaborators a
  complete engine composition requires; it also shows the long-form composition
  the builder performs for you.

To see the behavior this walkthrough relies on under test, run:

```sh
dotnet test --project tests/AgentKit.Simple.Tests --configuration Release --timeout 300s
```
