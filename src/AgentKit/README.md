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

One hosted agent can delegate a bounded task to another through `AgentKit.Goals`
and the `task` tool: the delegation is a durable child goal, and the worker in
`AgentKit.Goals.Hosting` runs it as one turn of the target agent in its own
session through this engine's public surface. The facade never references the
Goals runtime or the worker; it only validates, for every definition that
selects a goal profile, that the registrations the profile names exist.

In a .NET host, `AddAgentKit()` registers the same engine and validation into
the host's `IServiceCollection` and leaves provider disposal to the host. Start
with `AddAgentKit`, `AddAgent`, and `AddAgentRunProfilePublication` in
[ServiceExtensions.cs](ServiceExtensions.cs); the XML documentation lists
required collaborators, lifetimes, and duplicate-registration behavior.
`Agent.SteerAsync` and `FollowUpAsync` admit queued input to an active run
through the session-backed input queue, `CancelAsync` records a durable abort
for a run by `RunId`, and `AttachAsync` subscribes a second caller to a run in
progress.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Engine lifetimes and required
collaborators are described in the
[composition guide](../../docs/guides/composition.md).

## Build validation

Standalone builds and `AgentKitServiceProviderFactory` validate core service
registrations before activating application services. Register exactly one
unkeyed engine, agent-definition catalog, run-scope factory, run-profile
publication reader, session directory, session store catalog and selector, hook
kernel (dispatcher, catalog, profile selector), security profile selector,
authority selector, policy catalog, approval broker, approval store, security
decision store, audit dispatcher and grant store, model catalog,
provider-profile runtime selector, budget authority and profile catalog, clock,
randomizer factory, content hasher, run identity generator, and operation
identity generator. `AddAgentKit` installs the clock, randomizer factory
(cryptographically strong), content hasher (SHA-256), and run-scope factory as
replaceable defaults. Replace a default with `Replace` or `RemoveAll` followed
by an explicit registration; appending a second unkeyed implementation is
rejected. Keyed alternatives remain independent.

Every collaborator a definition selects through `AgentDefinition.Components` is
deliberately keyed: loop, continuation policy, input coordinator, output
publisher, output processor, context assembler, model selector, and model
request executor, plus the budget profile. Different definitions can therefore
select different implementations. Once the catalog is ready, composition
requires that every published definition's exact selected key resolve to exactly
one registration under that contract; an unkeyed registration never satisfies a
keyed selection, and two registrations sharing one key are ambiguous.

Missing and ambiguous services produce stable composition diagnostics, including
when Microsoft DI constructor validation is disabled. Feature-only hosts using
the provider factory do not need the engine's services.

## Durable run admission

When the agent definition selects a durability profile that enables
`agentkit.engine.run_admission`, the engine journals each accepted run as a
recoverable operation once the session has committed the acceptance it
describes, checkpointing `InputAdmitted`. The manifest carries run, session, and
admission identities only. A profile the composition cannot resolve is logged at
admission and fails the run in the loop rather than inventing a second failure
mode that would leave an accepted lane with no run behind it.
`RunAdmissionDurableOperationHandler` owns the operation name, and `AddAgentKit`
registers it.

Engine disposal drains required run-event sinks within a bounded deadline before
it disposes a standalone provider. A re-entrant disposal triggered by the owned
provider's own teardown returns immediately, because waiting for the disposal it
is part of would never complete.

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
- [Workstreams](../../docs/workstreams/index.md) — how this component was built,
  chunk by chunk.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
