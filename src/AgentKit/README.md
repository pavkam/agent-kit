# AgentKit

Compose a process-level engine that hosts immutable agent definitions and
isolated runs.

Use the facade to build a standalone engine or register one in a .NET host. Your
application selects the loop, providers, stores, tools, and policies separately.

## Use this project

For one conversation with one agent, you do not need this facade:
[`AgentKit.Simple`](../AgentKit.Simple/README.md) builds one in a few lines over
[`AgentKit.Conversations`](../AgentKit.Conversations/README.md). Reach for
`AgentEngine` when a process hosts a catalog of several agent definitions:

```csharp
var builder = AgentEngine.CreateBuilder();
// register loop, context, output, session, security, provider, and tool packages on builder.Services
builder.Services.AddAgent(definition);

await using var engine = builder.Build();
var resolution = await engine.GetAgentAsync(definition.Id, cancellationToken);
var agent = ((ResolvedAgent) resolution).Agent;

// One turn: creates a session for this identity, records the message, runs the agent.
var first = await agent.SendAsync(new AgentSendRequest(identity, "Hello"), cancellationToken);
// Continue the same session; a concurrent turn on it is rejected or queued per the session profile.
var second = await agent.SendAsync(new AgentSendRequest(identity, "And then?", first.SessionId), cancellationToken);
```

One engine hosts every published definition. `Agent.RunAsync<TOutput>` and
`StreamAsync<TOutput>` open the named session, revalidate the pinned definition,
and admit the turn. `SendAsync` is the same admission with the existing
`AgentLoopResult` return: it creates a session when the caller does not name
one. A `Reject` profile returns `AgentRunRejected<T>` from the typed methods and
`AgentAdmissionRejectedException` from `SendAsync`, with no store append. A
`Wait` profile serializes the second caller until the first releases the
in-process gate. Durable lane admission still follows that gate.

`AddEngineDelegationChannel()` registers the engine-backed
`ITaskDelegationChannel`: with `AgentKit.Goals`'s broker and the `task` tool,
one hosted agent can delegate a bounded task to another, which runs as one turn
in its own session and returns a summary with its real session and run
identities.

In a .NET host, `AddAgentKit()` registers the same engine and validation into
the host's `IServiceCollection` and leaves provider disposal to the host. Start
with `AddAgentKit`, `AddAgent`, and `AddAgentRunProfilePublication` in
[ServiceExtensions.cs](ServiceExtensions.cs); the XML documentation lists
required collaborators, lifetimes, and duplicate-registration behavior.
Queue-backed admission and run attachment by `RunId` are tracked in the
[implementation ledger](../../docs/implementation-progress.md#component-coverage).

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Engine lifetimes and required
collaborators are described in the
[composition guide](../../docs/guides/composition.md).

## Build validation

Standalone builds and `AgentKitServiceProviderFactory` validate core service
registrations before activating application services. Register exactly one
unkeyed engine, agent-definition catalog, run-profile publication reader,
security-profile selector, security grant store, clock, run identity generator,
and operation identity generator. Replace a default with `Replace` or
`RemoveAll` followed by an explicit registration; appending a second unkeyed
implementation is rejected. Keyed alternatives remain independent.

`IAgentLoop` is the one exception to "singular and unkeyed": it is deliberately
keyed and scoped, so different agent definitions can select different loops
(`AgentDefinition.LoopKey`, defaulting to `AgentLoopComponentDefaults.LoopKey`
when unset). Composition requires at least one keyed `IAgentLoop` registration
and, once the catalog is ready, that every published definition's exact selected
key actually resolves; two registrations sharing one key are ambiguous.

Missing and ambiguous services produce stable composition diagnostics, including
when Microsoft DI constructor validation is disabled. Feature-only hosts using
the provider factory do not need the engine's services. Full keyed run-plan
compilation and activation remain tracked in the implementation ledger.

## Related projects

- [AgentKit.Abstractions](../AgentKit.Abstractions/README.md) — implement
  AgentKit extensions against provider-neutral contracts and typed domain
  values.
- [AgentKit.Loop](../AgentKit.Loop/README.md) — coordinate turns, context
  preparation, model attempts, tool calls, and terminal outcomes.
- [AgentKit.Session](../AgentKit.Session/README.md) — coordinate session
  lifecycle, run ownership, branching, and store routing.
- [AgentKit.Providers](../AgentKit.Providers/README.md) — catalog configured
  models, validate capabilities, and select or resolve model implementations.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Tests](../../tests/AgentKit.Tests/README.md) — focused behavior and
  registration tests.
- [Component specification](../../docs/architecture/composition-and-configuration.md)
  — intended ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
