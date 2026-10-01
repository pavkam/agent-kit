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

| Call                                                                                                  | Registers                                                                                                                                                                                                                                                                                                                                                                                                                     |
| ----------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `UseLocalDevelopmentDefaults()`                                                                       | `AddInMemorySecurityGrantStore`, `AddInMemoryApprovalStore`, `AddInMemorySecurityDecisionStore`, `AddAllowAllSecurityPolicy`, best-effort audit delivery, `AddInMemorySessionStore`, `AddInMemorySessionDirectory`, `AddAgentTools`, and a basic-assurance local identity                                                                                                                                                     |
| `UseSqliteSessions(path)`                                                                             | `AddSqliteSessionStore` and `AddSqliteSessionDirectory` at that file (created on demand) and a durable session profile; see [Storing conversations](../../docs/guides/storage.md)                                                                                                                                                                                                                                             |
| `UseWorkspace(root)`                                                                                  | keyed `AddOperatingSystemFileSystem` over `root` plus `AddReadTool`, `AddWriteTool`, `AddListTool`, `AddGlobTool`, `AddSearchTool`, `AddEditTool`, `AddPatchTool` and the one toolset the definition selects; see [Working with files](../../docs/guides/file-system.md)                                                                                                                                                      |
| `UseOpenAI(apiKey, modelId)`                                                                          | `AddAgentProviders`, `AddOpenAI`, `AddOpenAIApiKeyCredential`, `AddOpenAIKnownLlmModel` under the alias `assistant`, with limits and prices from the bundled known-model catalog                                                                                                                                                                                                                                              |
| `UseAnthropic(apiKey, modelId)`                                                                       | The same shape for Anthropic: `AddAnthropic`, `AddAnthropicApiKeyCredential`, `AddAnthropicKnownLlmModel`                                                                                                                                                                                                                                                                                                                     |
| `UseOllama(modelId)`                                                                                  | `AddOllama`, a placeholder credential a local server ignores (or the key you pass), `AddOllamaLlmModel(descriptor)` and `AddModelDescriptors` with the adapter defaults; models are not in the catalog                                                                                                                                                                                                                        |
| `UseOpenRouter(apiKey, modelId)`                                                                      | `AddOpenRouter`, `AddOpenRouterApiKeyCredential`, `AddOpenRouterLlmModel(descriptor)` and `AddModelDescriptors` with the adapter defaults                                                                                                                                                                                                                                                                                     |
| `UseAzureOpenAI(endpoint, apiKey, deploymentId, modelId)`                                             | `AddAzureOpenAI` at the endpoint, `AddAzureOpenAIApiKeyCredential`, `AddAzureOpenAILlmModel(descriptor)` and `AddModelDescriptors`; a known OpenAI model's limits and prices are overlaid on the deployment                                                                                                                                                                                                                   |
| Any provider sugar call (`UseOpenAI`, `UseAnthropic`, `UseOllama`, `UseOpenRouter`, `UseAzureOpenAI`) | `AddAgentNetwork`, because every provider attempt sends through `INetworkTransport` under per-attempt egress grants; `UseOllama` additionally allows `http` and private addresses so a local server is reachable                                                                                                                                                                                                              |
| Any first sugar call                                                                                  | `AddAgentProviders`, `AddAgentSession`, `AddAgentContext`, `AddAgentOutput`, `AddAgentLoop`, `AddAgentIO` + `AddSessionBackedInputQueue`, `AddAgentHooks`, `AddAgentTools`, `AddAgentBudgets` with the default budget profile and an in-memory ledger, `AddAgentPermissions` + `AddSecurityAuthority`, one lazily built `AgentDefinition` source with its security and run-profile publications, and `AddConversationSession` |
| `WithSqliteDurability(path)` / `WithJsonDurability(dir)`                                              | `WithDurability()`'s profile, handlers, and policy over the SQLite journal, lease manager, and backend (or the JSON journal with in-memory leases and backend) at the explicit path, initialized when first resolved                                                                                                                                                                                                          |
| `WithDurability()`                                                                                    | `AddAgentDurability`, the in-memory journal, lease manager, and backend, the default recovery policy, handlers for every first-party boundary, and one durability profile enabling all seven boundaries on every hosted definition; see [Durable execution](../../docs/architecture/durable-execution.md#simple-composition)                                                                                                  |

Calls chain in any order before `Build()`; the plan is read lazily when the
provider is built. The definition selects toolsets, not individual tools: the
workspace and delegation sugar publish and select their own, `WithTools(keys)`
selects any toolset you registered with `AddToolset`, and a run advertises to
the model exactly the tools those toolsets name, with their exact captured
descriptors. A registered tool outside every selected toolset is never shown, so
the model cannot spend a turn on a call that is certain to be rejected. Nothing
is chosen silently: storage, authority, and identity are external facts, so
`UseLocalDevelopmentDefaults` is an explicit, named opt-in, and `Build()` fails
with a diagnostic naming what is missing (the engine's own composition
diagnostic for storage and security, a plan diagnostic for the model or
identity).

## When the sugar runs out

`builder.Services` is the same collection every registration lands on, so the
escape hatch is the ordinary AgentKit API:

- **Tools:**
  `builder.Services.AddOperatingSystemFileSystem(key, o => o.Roots.Add(...))`
  with `AddReadTool(o => { o.ProfileKey = key; o.HostRootPath = root; })` and a
  `WithTools(ReadFileTool.DefaultToolset.Key)` selection, and the model sees
  `read_file` on the next turn.
- **Another provider or an unknown model:** register the provider package's
  services on `builder.Services`, then `builder.UseModel(alias)`. The
  `Use<Provider>` methods share the alias `assistant`, so a builder calls one of
  them; a second throws immediately and names the first.
- **Durable sessions:** skip `UseLocalDevelopmentDefaults`, register
  `AddSqliteSessionStore`/`AddSqliteSessionDirectory` plus your security
  services, and call `WithIdentity`.
- **Crash recovery:** `WithDurability()` journals every first-party boundary to
  process-local in-memory adapters, which are explicitly ephemeral: the
  boundaries, checkpoints, and recovery decisions are real and inspectable, but
  nothing survives the process. `WithSqliteDurability(databasePath)` and
  `WithJsonDurability(directoryPath)` select the SQLite or JSON adapters at an
  explicit absolute path (never implied) so a restarted process can read the
  records a lost one left; one composition selects one store, and a second,
  different choice throws. JSON keeps leases and backend ownership in-process
  and its root must be canonical (no symbolic links). Register your own keyed
  adapters and `AddDurabilityProfile` on `builder.Services` for cross-process
  ownership.
- **Real security:** register your own `ISecurityPolicy` implementations and an
  audit sink instead of the local defaults.
- **Several agents:** `AddAgent(agentId, o => ...)` publishes another definition
  on the same engine; drive it with `engine.GetAgentAsync(agentId)` and
  `Agent.SendAsync`. `WithDelegation()` adds the `task` tool so agents can hand
  work to one another; the child runs on the same engine in its own session and
  only its bounded answer flows back.
- **Spending caps:**
  `WithBudget(o => { o.MaxToolCalls = 10; o.MaxCostUsd = 0.05m; })` makes every
  run reserve against hard limits; a refused reservation ends the turn naming
  the exhausted dimension. Accounting is in-memory unless you register a SQLite
  ledger first.
- **Durable content:** `WithArtifacts()` registers the artifact coordinator over
  an explicitly ephemeral in-memory store and selects it in every definition;
  register SQLite, JSON, or file-system artifact stores and your own profile for
  durable content.
- **Long conversations:** `WithCompaction()` registers the extractive compactor
  and a compaction profile that every definition selects; when the history nears
  the model's declared context window the loop checkpoints older entries and
  rebuilds the request from the summary.
- **Hooks:** `builder.Services.AddBeforeToolInvocationHook<MyHook>()` (or the
  run-started and before-model-request variants) runs your hook at that
  boundary; the dispatcher is already registered.
- **Structured answers:** `WithOutput<T>(schemaJson)` requires every final
  answer to be JSON matching the schema and deserializable to `T`, adds the
  instruction that tells the model so, and `AskAsync<T>` returns the value. A
  rejected answer is sent back for repair within `maximumRepairAttempts`; the
  accepted value is also on `ConversationTurnResult.Output`.

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
