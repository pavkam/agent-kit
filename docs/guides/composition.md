# Composing an application

Your host chooses AgentKit libraries, registers their services, and supplies
configuration. An engine captures that composition and exposes immutable agent
handles.

This guide explains the design and current entry points. Full runnable-graph
integration for the multi-agent engine is still tracked in the
[implementation ledger](../implementation-progress.md#component-coverage).
[Getting started](../getting-started.md) walks through a complete single-agent
composition you can run today.

## Start from the working shape

The direct, in-process path is one conversation with one agent through
`AgentKit.Conversations`. Every concern is a separate registration, and each one
is replaceable:

```csharp
var services = new ServiceCollection();

services.AddInMemorySecurityGrantStore();                       // security
services.AddStandaloneSecurityProfile(
    agentId, definitionRevision, configurationVersion, securityProfileKey, authorityKey,
    configurePermissions: o => o.AuditDelivery = SecurityAuditDelivery.BestEffort);
services.AddAllowAllSecurityPolicy();

services.AddAgentSession();                                     // session state
services.AddInMemorySessionStore();
services.AddInMemorySessionDirectory(new ComponentId("app.session"));

services.AddAgentContext();                                     // turn loop
services.AddAgentOutput();
services.AddAgentLoop();
services.AddAgentTools();

services.AddAgentProviders();                                   // model
services.AddOpenAI();
services.AddOpenAIApiKeyCredential(apiKey);
services.AddOpenAILlmModel(alias, modelId, OpenAIProviderDefaults.DefaultCapabilities);
services.AddModelDescriptors(new ModelDescriptorSourceId("app"), [descriptor]);

services.AddConversationSession(options => { /* identities, profile, alias, instructions, limits */ });

await using var provider = services.BuildServiceProvider(
    new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
var conversation = provider.GetRequiredService<IConversationSession>();
var result = await conversation.SendAsync("Hello", cancellationToken);
```

[`examples/QuickStart`](../../examples/QuickStart/QuickStartAgent.cs) is this
program in full; [`examples/CodingAgent`](../../examples/CodingAgent/README.md)
grows it with SQLite sessions, an approval broker, and eight tools. The rest of
this guide explains what each block owns and how the multi-agent `AgentEngine`
facade generalizes it.

## Understand the four lifetimes

| Object                      | What it represents                                  | What you share                                                                       |
| --------------------------- | --------------------------------------------------- | ------------------------------------------------------------------------------------ |
| `AgentEngine`               | A built process-level composition and agent catalog | One engine can host several agents and concurrent runs.                              |
| `AgentDefinition` / `Agent` | An immutable configuration / engine-bound handle    | Share the handle; mutable run state lives elsewhere.                                 |
| Session                     | Conversation records, branching, and coordination   | Reuse a session when continuing its conversation, subject to its concurrency policy. |
| Run                         | One admitted unit of work and its execution scope   | A new admission gets a new `RunId`; recovery preserves an open run's identity.       |

The
[composition specification](../architecture/composition-and-configuration.md)
defines catalog publication, validation, and ownership. The
[run lifecycle](../concepts/run-lifecycle-and-settlement.md) distinguishes
semantic completion, cancellation, and settlement.

## Select the required components

The [AgentKit facade](../../src/AgentKit/README.md) provides
`AgentEngine.CreateBuilder()` for standalone composition and `AddAgentKit()` for
host-managed registration. Both use an `IServiceCollection`. They register
foundation services, while your host selects the behavior packages.

| Concern                | First-party package                                     | Application responsibility                                                |
| ---------------------- | ------------------------------------------------------- | ------------------------------------------------------------------------- |
| Turns and continuation | [Loop](../../src/AgentKit.Loop/README.md)               | Select limits, continuation, and run behavior.                            |
| Request context        | [Context](../../src/AgentKit.Context/README.md)         | Configure instructions, sources, trust, and bounds.                       |
| Model selection        | [Providers](../../src/AgentKit.Providers/README.md)     | Register compatible models and their concrete adapters.                   |
| Output                 | [Output](../../src/AgentKit.Output/README.md)           | Select output definitions and a supported schema engine.                  |
| Session state          | [Session](../../src/AgentKit.Session/README.md)         | Select profiles, coordination, and a store.                               |
| Input and publication  | [IO](../../src/AgentKit.IO/README.md)                   | Connect admission, queued inputs, human interaction, and output channels. |
| Security               | [Permissions](../../src/AgentKit.Permissions/README.md) | Supply authority, approval, audit, and policy bindings.                   |
| Spending and limits    | [Budgets](../../src/AgentKit.Budgets/README.md)         | Set run profiles and reserve bounded capacity.                            |
| Lifecycle hooks        | [Hooks](../../src/AgentKit.Hooks/README.md)             | Select a profile and register permitted observers or mutations.           |

The complete list of required engine-wide services and per-agent selections is
in
[build validation](../architecture/composition-and-configuration.md#build-validation).
Registering these packages alone is not proof of a complete composition:
selected profiles, catalogs, bindings, and declared component dependencies must
agree.

## Put configuration at its owner

Use DI to choose implementations and lifetimes. Use typed options for package
mechanics, immutable agent definitions for reusable agent behavior, and bounded
run options for invocation-specific choices. A run override must respect the
limits established by its definition.

For providers, choose a branded package from the
[catalog](../packages/index.md#model-providers). Bind endpoints, credentials,
model identifiers, and supported operations explicitly. Conversation and
embedding registrations are independent, even when one vendor supplies both.
`AgentKit.Providers.OpenAICompatible` contains shared protocol machinery; its
name does not establish a particular vendor's capabilities.

## Add tools and host access deliberately

The [tool runtime](../../src/AgentKit.Tools/README.md) owns catalog and
invocation mechanics. Individual [tool packages](../packages/index.md#tools)
contribute features such as reading a file, editing text, or asking a human a
question. Host packages own filesystem, network, and process effects.

A model requests a tool call; the runtime validates and authorizes it before
execution. Registering a tool does not grant access to its backing resources.
Configure the corresponding host boundary, limits, and security policy.

For example, file-reading behavior connects the contracts in
[Abstractions](../../src/AgentKit.Abstractions/README.md), the
[Read tool](../../src/AgentKit.Tools.Read/README.md), a selected
[filesystem](../../src/AgentKit.FileSystem/README.md), and a
[security authority](../../src/AgentKit.Permissions/README.md). These are
collaborators selected by the host, not automatic registrations.

## Own startup and shutdown

Build the provider with `ValidateOnBuild` and `ValidateScopes` enabled so a
missing or ambiguous registration fails at startup, not in the middle of a run.
When your application owns the provider, wrap the conversation and the provider
together so one `Dispose` releases both:

```csharp
var provider = services.BuildServiceProvider(
    new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
using var conversation = new OwnedConversationSession(
    provider.GetRequiredService<IConversationSession>(), provider);
```

A standalone `AgentEngine` owns the service provider it builds and is
asynchronously disposed. A host-managed engine leaves provider disposal to the
host. Current host-managed composition uses
[`AgentKitServiceProviderFactory`](../../src/AgentKit/AgentKitServiceProviderFactory.cs)
to capture the service descriptors required by composition validation.

In-memory stores are useful for tests and short-lived applications; they do not
provide recovery after process loss. Review
[session persistence](../architecture/sessions.md) and
[durable execution](../architecture/durable-execution.md) before designing
recovery-sensitive work.

Continue with the [project catalog](../packages/index.md) for component APIs, or
the [testing guide](../testing/index.md) to inspect evidence for a behavior.
