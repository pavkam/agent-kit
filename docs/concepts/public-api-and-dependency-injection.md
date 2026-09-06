# Public API and dependency injection

**Status:** Normative API direction  
**Depends on:** [Architecture](architecture-and-dependency-boundaries.md),
[agent and run context](agent-definition-and-run-context.md)

## Purpose

The public API must make safe behavior easy, replacement explicit, and
unsupported behavior discoverable. These sketches reserve semantic roles, not
final names or signatures.

## Facade and builder

`AgentEngine` is the immutable public facade. `AgentEngineBuilder` is a separate
mutable composition object:

```csharp
public sealed class AgentEngine : IAsyncDisposable
{
    public static AgentEngineBuilder CreateBuilder();
}

public sealed class AgentEngineBuilder
{
    public IServiceCollection Services { get; }
    public AgentEngine Build();
}
```

The exact operational methods remain subject to API review, but the ownership
shape is fixed. A standalone engine owns the provider built by its builder. An
engine resolved from an external host uses the host's scopes and MUST NOT
dispose the host provider.

`AddAgentKit` registers the same facade and composition validation into an
existing `IServiceCollection`. Feature packages operate on that collection, so
they work with the standalone builder, ASP.NET Core, worker hosts, and custom
containers using the Microsoft DI contracts.

## Execution surface

```csharp
public interface IAgentRunner
{
    Task<AgentRunResult<TOutput>> RunAsync<TOutput>(
        AgentDefinition agent,
        AgentInput input,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<IAgentRunStream<TOutput>> StreamAsync<TOutput>(
        AgentDefinition agent,
        AgentInput input,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

A session-oriented API MAY separate `AdmitAsync`, `WakeAsync`, `RunAsync`,
`InterruptAsync`, and `ResumeAsync`. It MUST return admission receipts and make
join/queue/reject semantics explicit when a session is active.

## Results

```csharp
public sealed record AgentRunResult<TOutput>(
    RunId RunId,
    SessionId SessionId,
    ConversationId? ConversationId,
    AgentRunOutcome Outcome,
    TOutput? Output,
    MessageCursor PreviousCursor,
    ImmutableArray<AgentMessage> NewMessages,
    RunUsage Usage,
    ImmutableArray<DeferredToolRequest> DeferredRequests,
    ExtensionData Metadata);
```

`AgentRunOutcome` is a discriminated result for success, idle completion,
deferred, cancelled, limit reached, policy halt, and failed. Null output alone
MUST NOT encode these differences.

Streaming completion returns the same result. The stream owns a subscription;
disposing it MUST have documented run-cancellation behavior.

## Core contracts

`AgentKit.Abstractions` SHOULD stabilize narrow contracts only after their
specification and conformance suite:

- `IAgentLoop`, `IInputCoordinator`, `IInputQueue`, `IOutputPublisher`,
  `ISessionExecutor`;
- `IContextAssembler`, `IHistoryProcessor`, `ICompactor`;
- `IModelCatalog`, `IModelSelector`, `IModelRequestExecutor`, `IChatModel`,
  `IEmbeddingModel`, `IReranker`;
- `IToolProvider`, `IToolResolver`, `IToolInvoker`, `IToolScheduler`;
- `IToolPermissionPolicy`, `IApprovalBroker`, `IToolAuditSink`;
- `ISessionStore`, memory/document/vector/retrieval contracts;
- `IRunEventSink`, `IUsageBudget`, and clock/ID/random abstractions; and
- capability/middleware contracts for demonstrated extension axes.

Interfaces MUST stay smaller than default implementations. Discovery, selection,
policy, execution, state, and observation remain separate.

## .NET API rules

- Target .NET 10 and C# 14.
- Use immutable records/readonly values; services with identity/lifecycle are
  classes.
- One named type per file with matching name.
- Public async APIs accept `CancellationToken` and never block.
- Use `IAsyncEnumerable<T>` only for actual streaming/pagination.
- Document threading, ownership, disposal, cancellation, exceptions, and
  ordering in XML comments.
- Validate arguments before observable mutation.
- Preserve unknown provider data through typed extension values, not `object`.
- Prefer discriminated result hierarchies over boolean/error string tuples.

## Registration surface

```csharp
builder.Services.AddAgentLoop();
builder.Services.AddAgentContext();
builder.Services.AddAgentIO();
builder.Services.AddAgentSession();
builder.Services.AddInMemorySessionStore();
builder.Services.AddAgentPermissions();
builder.Services.AddAgentProviders();
builder.Services.AddOpenAI(options => { ... });
builder.Services.AddReadTool();
```

Exact names are provisional. Registration methods MUST return
`IServiceCollection`, never build/resolve a provider, validate options, and
document:

- singular versus additive registration;
- key/name collision behavior;
- `TryAdd` default and explicit replacement path;
- idempotency of repeated calls;
- service lifetimes and disposal owner; and
- whether configuration is captured or monitored.

Multi-provider concepts SHOULD be keyed/named and selected by an injected
catalog/selector. Runtime code MUST NOT receive `IServiceProvider` as a locator.

`Build()` or hosted validation MUST require one effective loop, input
coordinator, output publisher, session coordinator, session store, context
assembler, permission policy, model catalog, model selector, model request
executor, at least one conversational model, and a `TimeProvider`.
`TimeProvider.System` is the replaceable default. Missing or ambiguous required
services fail before the first run.

Tools, skills, memory, embeddings, reranking, goals, MCP, evaluation, and
additional contributors are optional. When one is registered, validation MUST
include its required collaborators.

Concrete provider packages MUST expose one package-level entry point such as
`AddOpenAI`, `AddOpenRouter`, or `AddZAi`, then register named conversational,
embedding, reranking, or other operations independently. One vendor package MAY
supply several operations without merging their contracts or replacement paths.

## Lifetimes

Immutable definitions/descriptors and thread-safe catalogs MAY be singleton. Run
context, mutable capability instances, and operation budgets are scoped to an
explicit run scope. Transient adapters are disposed by the container owner.
Session state is loaded from its store, not held forever in a singleton.

Registration MUST pass scope validation and disposal-once tests. A singleton
must not capture a scoped run service.

All framework-owned time reads, delays, deadlines, and timestamps use the
injected `TimeProvider`. External timestamps, such as provider or filesystem
metadata, retain their source and are not rewritten to match the local clock.

## Options and validation

Typed options use `Microsoft.Extensions.Options`. Impossible limits, missing
model/store keys, duplicate aliases, unsafe retry combinations, and unsupported
configured capabilities SHOULD fail at host startup. Dynamic per-run values use
explicit strategies rather than mutating options monitors.

Credentials use dedicated providers or platform credential abstractions. They
MUST NOT appear in option display, validation messages, or configuration
snapshots.

## Compatibility

Public contract evolution is additive by default. Serialization schemas and
durable operation names have explicit versions independent of assembly version.
A breaking contract change requires compatibility review, migration plan, and
updated conformance packages.

## Acceptance scenarios

- An application replaces each singular default through public DI APIs.
- Multiple named models/toolsets compose with deterministic collision behavior.
- Scope validation catches a singleton capturing run state.
- Disposing a standalone engine disposes its owned provider exactly once.
- Disposing a hosted engine does not dispose the host provider.
- Options validation fails before the first run.
- A result distinguishes deferred, limited, cancelled, and failed without text
  parsing.

## Related specifications

- [Configuration and overrides](configuration-and-overrides.md)
- [Testing and evaluation](testing-and-evaluation.md)
- [Research provenance](research-provenance.md)
