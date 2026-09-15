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
var agent = await engine.GetAgentAsync(definition.Id, cancellationToken);
```

In a .NET host, `AddAgentKit()` registers the same engine and validation into
the host's `IServiceCollection` and leaves provider disposal to the host. Start
with `AddAgentKit`, `AddAgent`, and `AddAgentRunProfilePublication` in
[ServiceExtensions.cs](ServiceExtensions.cs); the XML documentation lists
required collaborators, lifetimes, and duplicate-registration behavior. Complete
keyed run-plan compilation for the engine is tracked in the
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
operation identity generator and loop. Replace a default with `Replace` or
`RemoveAll` followed by an explicit registration; appending a second unkeyed
implementation is rejected. Keyed alternatives remain independent.

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
