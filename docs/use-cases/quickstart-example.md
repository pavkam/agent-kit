# Example: QuickStart

`examples/QuickStart` is the smallest complete AgentKit agent: one OpenAI model,
local development defaults, one instruction, and a single question from the
command line. It is the program [Getting started](../getting-started.md) walks
through, and
[`tests/QuickStart.Tests`](../../tests/QuickStart.Tests/QuickStartAgentTests.cs)
keeps it compiling and completing a turn against the current libraries.

Run it with:

```sh
export OPENAI_API_KEY=sk-...
dotnet run --project examples/QuickStart -- "In one sentence, what is AgentKit?"
```

## What it is for

Use this shape when you want a conversational assistant with no tools, no
persistence, and no authorization decisions beyond "everything is allowed on my
own machine": a command-line helper, a script that asks a model one question, a
starting point you will grow from.

## What it composes

The whole agent is one builder chain, kept in a separate class so the tests can
adjust `builder.Services` before `Build()`:

```csharp
internal static class QuickStartAgent
{
    public static AgentEngineBuilder CreateBuilder(string apiKey) =>
        AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI(apiKey, "gpt-4o-mini")
            .WithInstructions("You are a concise assistant.");
}
```

```csharp
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("Set OPENAI_API_KEY before running the quick start.");

await using var engine = QuickStartAgent.CreateBuilder(apiKey).Build();

Console.WriteLine(await engine.AskAsync(prompt));
```

| Call                            | What it contributes                                                                                                                                     |
| ------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `UseLocalDevelopmentDefaults()` | In-memory session store and directory, in-memory grant store, allow-all policy, every registered tool allowed, best-effort audit, process-user identity |
| `UseOpenAI(apiKey, modelId)`    | Provider catalog, OpenAI adapter, API-key credential, and a descriptor whose limits and prices come from the bundled known-model catalog                |
| `WithInstructions(text)`        | One system message                                                                                                                                      |
| `Build()`                       | Composition validation with `ValidateOnBuild` and `ValidateScopes`, one published `AgentDefinition`, the engine that owns the provider                  |

Nothing is chosen silently. Without the environment variable the program stops
before composing anything; without `UseLocalDevelopmentDefaults()` or your own
storage and security registrations, `Build()` fails with a diagnostic naming the
missing piece.

## How it is used

`AskAsync` sends one user message, runs the loop until the model produces a
final answer or a limit is hit, and returns the assistant text. Calling it again
continues the same conversation. When the turn does not end in a final answer it
throws `SimpleAgentException` carrying the safe reason and the committed events,
never provider diagnostics or the key.

For anything beyond text, `SendAsync` returns the `ConversationTurnResult` with
its `Events`, and `engine.Conversation` exposes the underlying
`IConversationSession`.

## What the tests prove

The test project replaces the `HttpClient` the OpenAI adapter uses with a
loopback handler, which is the pattern
[Testing an agent without a live model](testing-agents-offline.md) builds on:

```csharp
var builder = QuickStartAgent.CreateBuilder("sk-test");
builder.Services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));
await using var engine = builder.Build();
```

The tests assert that a blank key is rejected before composition, that the
engine builds without touching the network, that the bearer key, system
instruction, and model identifier reach the wire, that a second `AskAsync`
carries the first exchange, and that an unreachable provider surfaces as a
`SimpleAgentException` whose message does not contain the key.

## Where to go next

- [Customer support assistant in a web API](web-support-assistant.md) keeps the
  same builder and adds durable sessions and a real identity.
- [Order lookup with your own tool](domain-tool-integration.md) adds the first
  tool.
- [Example: CodingAgent](coding-agent-example.md) is the written-out form of the
  same composition with everything switched on.

Next: [Example: CodingAgent](coding-agent-example.md) · [Use cases](index.md)
