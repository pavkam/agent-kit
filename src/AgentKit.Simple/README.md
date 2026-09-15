# AgentKit.Simple

The shortest path to a working agent. A fluent builder composes the real
AgentKit loop, context, output, session, security, tool, and provider packages
on an ordinary `IServiceCollection` and returns an agent you can talk to.

```csharp
using var agent = SimpleAgentBuilder.Create()
    .UseLocalDevelopmentDefaults()
    .UseOpenAI(apiKey, "gpt-4o-mini")
    .WithInstructions("You are a concise assistant.")
    .Build();

Console.WriteLine(await agent.AskAsync("In one sentence, what is AgentKit?"));
```

`AskAsync` returns the assistant's text and throws `SimpleAgentException` (with
the safe reason and the committed events) when a turn ends without a final
answer. `SendAsync` returns the full `ConversationTurnResult`; the observing
overload streams text, tool, and usage events; `agent.Conversation` is the
underlying `IConversationSession` for history and session resume.

## What each call registers

| Call                            | Registers                                                                                                                                                                                                                                                     |
| ------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `UseLocalDevelopmentDefaults()` | `AddInMemorySecurityGrantStore`, `AddStandaloneSecurityProfile` with best-effort audit, `AddAllowAllSecurityPolicy`, `AddInMemorySessionStore`, `AddInMemorySessionDirectory`, `AddAgentTools(AllowAllRegisteredTools)`, and a basic-assurance local identity |
| `UseOpenAI(apiKey, modelId)`    | `AddAgentProviders`, `AddOpenAI`, `AddOpenAIApiKeyCredential`, `AddOpenAIKnownLlmModel` under the alias `assistant`, with limits and prices from the bundled known-model catalog                                                                              |
| `Build()`                       | `AddAgentSession`, `AddAgentContext`, `AddAgentOutput`, `AddAgentLoop`, `AddAgentTools`, `AddConversationSession`, then builds the provider with `ValidateOnBuild` and `ValidateScopes`                                                                       |

Every `ITool` registered on `builder.Services` is advertised to the model with
its exact captured descriptor. Nothing is chosen silently: storage, authority,
and identity are external facts, so `UseLocalDevelopmentDefaults` is an
explicit, named opt-in and `Build()` fails with a diagnostic that names the
missing registration when it is absent.

## When the sugar runs out

`builder.Services` is the same collection every registration lands on, so the
escape hatch is the ordinary AgentKit API:

- **Tools:** `builder.Services.AddSandboxedFileSystem(root).AddReadTool();` and
  the model sees `read_file` on the next turn.
- **Another provider or an unknown model:** register the provider package's
  services on `builder.Services`, then `builder.UseModel(alias)`.
- **Durable sessions:** skip `UseLocalDevelopmentDefaults`, register
  `AddSqliteSessionStore`/`AddSqliteSessionDirectory` plus your security
  services, and call `WithIdentity`.
- **Real security:** register your own `ISecurityPolicy` implementations and an
  audit sink instead of the local defaults.

`UseLocalDevelopmentDefaults` means what it says. Nothing survives the process,
every request is permitted, and the identity is the process user. A service, a
multi-tenant host, or anything handling untrusted input does not call it.

## Where this sits

This is an application-tier composition leaf: it references the runtime and
provider packages it composes and is itself referenced only by applications. It
is not a second runtime and adds no `AddSimpleAgent` registration; a host that
already owns an `IServiceCollection` registers the individual packages and
`AddConversationSession`, exactly as `Build()` does.

Target: **.NET 10**.
[`examples/QuickStart`](../../examples/QuickStart/README.md) is this README as a
runnable program; [Getting started](../../docs/getting-started.md) walks through
it.

## Related projects

- [AgentKit.Conversations](../AgentKit.Conversations/README.md) — the one-call
  conversational turn the agent is built on.
- [AgentKit.Providers](../AgentKit.Providers/README.md) — the model catalog,
  including the known-model catalog `UseOpenAI` reads.
- [AgentKit](../AgentKit/README.md) — the multi-agent engine facade.

## Tests and reference

- [AgentKit.Simple.Tests](../../tests/AgentKit.Simple.Tests/README.md) — builder
  guards, composition diagnostics, tool advertising, and end-to-end turns
  against a loopback provider.
- [Composition guide](../../docs/guides/composition.md) — the lifetimes and
  required collaborators behind the sugar.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
