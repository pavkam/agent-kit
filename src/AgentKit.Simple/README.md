# AgentKit.Simple

The shortest path to a working agent: fluent `Use*`/`With*` extensions on the
real `AgentEngineBuilder` that compose the loop, context, output, session,
security, tool, and provider packages through their ordinary registrations,
publish one agent definition to the engine, and add one conversation the built
`AgentEngine` answers through `AskAsync`.

```csharp
await using var engine = AgentEngine.CreateBuilder()
    .UseLocalDevelopmentDefaults()
    .UseOpenAI(apiKey, "gpt-4o-mini")
    .WithInstructions("You are a concise assistant.")
    .Build();

Console.WriteLine(await engine.AskAsync("In one sentence, what is AgentKit?"));
```

`engine.AskAsync` returns the assistant's text and throws `SimpleAgentException`
(with the safe reason and the committed events) when a turn ends without a final
answer. `engine.SendAsync` returns the full `ConversationTurnResult`; the
observing overload streams text, tool, and usage events; `engine.Conversation`
is the underlying `IConversationSession` for history and session resume. The
engine is still the real engine: `GetAgentsAsync` lists the one published
`AgentDefinition`, and every other engine API works.

## What each call registers

| Call                            | Registers                                                                                                                                                                                                                                                              |
| ------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `UseLocalDevelopmentDefaults()` | `AddInMemorySecurityGrantStore`, `AddStandaloneSecurityProfile` with best-effort audit, `AddAllowAllSecurityPolicy`, `AddInMemorySessionStore`, `AddInMemorySessionDirectory`, `AddAgentTools(AllowAllRegisteredTools)`, and a basic-assurance local identity          |
| `UseSqliteSessions(path)`       | `AddSqliteSessionStore` and `AddSqliteSessionDirectory` at that file (created on demand) and a durable session profile; see [Storing conversations](../../docs/guides/storage.md)                                                                                      |
| `UseWorkspace(root)`            | `AddSandboxedFileSystem(root)` plus `AddReadTool`, `AddWriteTool`, `AddEditTool`, `AddGlobTool`, `AddSearchTool`, `AddListTool`; see [Working with files](../../docs/guides/file-system.md)                                                                            |
| `UseOpenAI(apiKey, modelId)`    | `AddAgentProviders`, `AddOpenAI`, `AddOpenAIApiKeyCredential`, `AddOpenAIKnownLlmModel` under the alias `assistant`, with limits and prices from the bundled known-model catalog                                                                                       |
| Any first sugar call            | `AddAgentProviders`, `AddAgentSession`, `AddAgentContext`, `AddAgentOutput`, `AddAgentLoop`, `AddAgentTools`, `AddAgentPermissions` + `AddSecurityAuthority`, one lazily built `AgentDefinition` source with its run-profile publication, and `AddConversationSession` |

Calls chain in any order before `Build()`; the plan is read lazily when the
provider is built. Every `ITool` registered on `builder.Services` is advertised
to the model, and appears in the published `AgentDefinition`, with its exact
captured descriptor. Nothing is chosen silently: storage, authority, and
identity are external facts, so `UseLocalDevelopmentDefaults` is an explicit,
named opt-in, and `Build()` fails with a diagnostic naming what is missing (the
engine's own composition diagnostic for storage and security, a plan diagnostic
for the model or identity).

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

This is an application-tier composition leaf: it references the facade and the
runtime and provider packages it composes, and is itself referenced only by
applications. It is not a second runtime and adds no `AddSimpleAgent`
registration; a host that already owns an `IServiceCollection` registers the
individual packages and `AddConversationSession`, exactly as these extensions
do. `AgentEngine.Services` (mirroring `IHost.Services`) is how the application
reaches what it registered; runtime components never receive it.

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
