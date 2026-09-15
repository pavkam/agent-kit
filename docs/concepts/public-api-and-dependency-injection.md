# Public API and dependency injection

**Status:** Normative API direction

**Architecture:**
[Composition and configuration](../architecture/composition-and-configuration.md),
[Project structure](../architecture/project-structure.md)

**Depends on:** [Architecture](architecture-and-dependency-boundaries.md),
[agent and run context](agent-definition-and-run-context.md)

## Purpose

The public API must make safe behavior easy, replacement explicit, and
unsupported behavior discoverable. These sketches reserve semantic roles, not
final names or signatures.

## Facade and builder

The [composition architecture](../architecture/composition-and-configuration.md)
assigns this API shape to the dependency-light AgentKit package.

`AgentEngine` is the immutable public facade. `AgentEngineBuilder` is a separate
mutable composition object. Their canonical signatures—including multi-agent
catalog resolution, session creation, typed caller identity, run and stream
operations, and ownership—are defined once in the
[composition architecture](../architecture/composition-and-configuration.md#process-level-execution-surface).

The exact operational methods remain subject to API review, but the ownership
shape is fixed. A standalone engine owns the provider built by its builder. An
engine resolved from an external host uses the host's scopes and MUST NOT
dispose the host provider.

The engine is the complete process-level composition and may catalog and run
many agents. An `Agent` is an immutable concurrency-safe handle bound to one
validated definition and catalog version. Neither the engine nor an agent holds
mutable session/run state or exposes the service provider; each invocation
creates an isolated run scope.

`AddAgentKit` registers the same facade and composition validation into an
existing `IServiceCollection`. Feature packages operate on that collection, so
they work with the standalone builder, ASP.NET Core, worker hosts, and custom
containers using the Microsoft DI contracts.

## Execution surface

Session-oriented operations expose the distinction between
[admission and promotion](input-admission-and-message-queues.md), while run
methods state which [completion boundary](run-lifecycle-and-settlement.md) they
await.

```csharp
namespace AgentKit;

public interface IAgentRunner
{
    Task<AgentRunResult<TOutput>> RunAsync<TOutput>(
        AgentRunRequest request,
        CancellationToken cancellationToken = default);

    Task<AgentRunStreamStartResult<TOutput>> StreamAsync<TOutput>(
        AgentRunRequest request,
        CancellationToken cancellationToken = default);
}
```

A session-oriented API MAY separate `AdmitAsync`, `WakeAsync`, `RunAsync`,
`InterruptAsync`, and `ResumeAsync`. It MUST return admission receipts and make
join/queue/reject semantics explicit when a session is active.

## Results

Result outcomes use the [stable error taxonomy](error-taxonomy.md) and preserve
newly committed messages rather than reconstructing history from display text.
The canonical `AgentRunResult<TOutput>` and discriminated `AgentRunOutcome`
shapes are defined once in the
[output architecture](../architecture/input-and-output.md#normative-minimal-output-contracts).

`AgentRunOutcome` is a discriminated result for success, idle completion,
deferred, cancelled, limit reached, policy halt, and failed. Null output alone
MUST NOT encode these differences.

Pre-admission rejection has no run identity. An accepted run produces a
`AgentRunFinished<TOutput>` envelope containing its semantic outcome and
separate settlement outcome; clean success requires both to succeed. Stream
creation is a typed started-or-rejected result, and a started stream's
completion returns the same finished envelope. The stream owns a subscription;
disposing it MUST have documented run-cancellation behavior.

## Core contracts

`AgentKit.Abstractions` SHOULD stabilize narrow contracts only after their
specification and conformance suite:

- `IAgentDefinitionCatalog` and additive definition-source contracts;
- `IAgentLoop`, `IInputCoordinator`, `IInputQueue`, `IOutputPublisher`,
  `ISessionCoordinator`;
- `IContextAssembler`, `IHistoryProcessor`, `ICompactor`;
- `IModelCatalog`, `IModelSelector`, `IModelRequestExecutor`, `ILlmModel`,
  `IEmbeddingModel`, `IReranker`;
- `IToolProvider`, `IToolResolver`, `IToolInvoker`, `IToolScheduler`,
  `IToolCallRecorder`, `IToolResultProjectionPolicyCatalog`, and
  `IToolResultProjector`;
- `ISecurityAuthority`, `ISecurityPolicy`, `IApprovalBroker`, security grant and
  audit contracts;
- `IHookDispatcher`, typed closed point definitions, dedicated hook interfaces,
  and their `EventArgs`-derived boundary types;
- narrow file-system, network, and process capability contracts;
- `ISessionStore`, memory/document/vector/retrieval contracts;
- `IRunEventSink`, `IBudgetAuthority`, `IBudgetScope`, `TimeProvider`, closed
  `IIdentifierGenerator<TIdentifier>` services, `IRandomizerFactory`, and
  `IContentHasher`; and
- capability and hook contracts for demonstrated extension axes.

Interfaces MUST stay smaller than default implementations. Discovery, selection,
policy, execution, state, and observation remain separate.

## .NET API rules

- Target .NET 10 and C# 14.
- Use immutable records/readonly values; services with identity/lifecycle are
  classes.
- Use dedicated `readonly record struct` domain identities such as `AgentId`,
  `SessionId`, `RunId`, `TurnId`, `MessageId`, and `ToolCallId`; never expose
  raw strings, GUIDs, or integers as those identities. Framework generation uses
  a replaceable `IIdentifierGenerator<TIdentifier>`.
- One named type per file with matching name.
- Public async APIs accept `CancellationToken` and never block.
- Use `IAsyncEnumerable<T>` only for actual streaming/pagination.
- Document threading, ownership, disposal, cancellation, exceptions, and
  ordering in XML comments.
- Validate arguments before observable mutation.
- Preserve unknown provider data through typed extension values, not `object`.
- Prefer discriminated result hierarchies over boolean/error string tuples.

## Registration surface

The registrations a complete single-agent composition uses today. This is what
the `AgentKit.Simple` extensions on `AgentEngineBuilder` perform; a host that
owns its own collection writes them directly:

```csharp
services.AddInMemorySecurityGrantStore();
services.AddStandaloneSecurityProfile(
    agentId, definitionRevision, configurationVersion, securityProfileKey, authorityKey);
services.AddAllowAllSecurityPolicy();

services.AddAgentSession();
services.AddInMemorySessionStore();
services.AddInMemorySessionDirectory(new ComponentId("app.session"));

services.AddAgentContext();
services.AddAgentOutput();
services.AddAgentLoop();
services.AddAgentTools();

services.AddAgentProviders();
services.AddOpenAI();
services.AddOpenAIApiKeyCredential(apiKey);
services.AddOpenAIKnownLlmModel(alias, modelId);

services.AddConversationSession(options => { /* identities, profile, alias, instructions */ });
```

Optional packages follow the same pattern (`AddAgentBudgets`, `AddAgentHooks`,
`AddInputCoordinator`, `AddReadTool`, `AddSandboxedFileSystem`, and so on), and
the multi-agent facade adds `AddAgentKit` with `AddAgent(definition)`.
Registration methods MUST return `IServiceCollection`, never build/resolve a
provider, validate options, and document:

- singular versus additive registration;
- key/name collision behavior;
- `TryAdd` default and explicit replacement path;
- idempotency of repeated calls;
- service lifetimes and disposal owner; and
- whether configuration is captured or monitored.

Runtime registrations such as `AddAgentSession`, `AddAgentPermissions`,
`AddAgentBudgets`, `AddAgentArtifacts`, and `AddAgentMemory` MUST NOT install a
concrete store. A host separately calls an explicit `.InMemory`, `.Sqlite`, or
other adapter registration and supplies its key and persistence target. The
in-memory call above is therefore an application choice, not a framework
default. Multiple adapters may coexist only through a catalog and exact
selection; registration order never selects persistence.

Multi-provider concepts SHOULD be keyed/named and selected by an injected
catalog/selector. Runtime code MUST NOT receive `IServiceProvider` as a locator.

`Build()` or hosted validation MUST require singular engine-wide composition
services: one agent-definition catalog with at least one runnable definition,
run-scope factory and validator, session directory/store selector, hook
dispatcher/profile selector, security authority selector/policy catalog,
approval broker, model catalog, budget authority, `TimeProvider`,
`IRandomizerFactory`, and `IContentHasher`, plus one effective closed
`IIdentifierGenerator<TIdentifier>` for every framework-created identity.
`TimeProvider.System` and the deterministic primitive implementations are
replaceable defaults. For every runnable definition, validation resolves exactly
one selected loop, continuation policy, input coordinator, output publisher,
output processor, context assembler, run budget profile, session coordinator/run
coordinator/profile/store, hook profile, security authority/profile, model
selector, model request executor, and at least one compatible conversational
model. Missing or ambiguous selections fail before the first run.

Every registered definition MUST resolve all of its typed keyed selections and
pass capability, scope, and policy validation. Agent definitions are additive;
conflicting `AgentId` values are rejected deterministically under the
[canonical source-precedence rules](../architecture/composition-and-configuration.md#agent-definitions-and-catalog).

Tools, skills, memory, embeddings, reranking, goals, MCP, evaluation, and
additional contributors are optional. When one is registered, validation MUST
include its required collaborators.

Concrete provider packages MUST expose one package-level entry point such as
`AddOpenAI`, `AddOpenRouter`, or `AddZAI`, then register named conversational,
embedding, reranking, or other operations independently. One vendor package MAY
supply several operations without merging their contracts or replacement paths.
Each operation registration binds one immutable descriptor to explicit keyed,
versioned endpoint/service-surface and credential/account profiles. Profile keys
are independently replaceable and collision-checked; an unkeyed credential or
options singleton whose meaning depends on registration order is invalid.

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

The facade registers `TimeProvider.System`, a cryptographically strong
randomizer factory, a SHA-256 content hasher, and explicit closed generators for
framework-owned identifiers as replaceable thread-safe singletons. A created
`IRandomizer` is operation-owned and is never injected as mutable singleton
state. Hashes carry typed algorithm and canonicalization versions; semantic
owners canonicalize their data explicitly rather than relying on a process-wide
implicit serialization. Replay and tests replace these services through their
dedicated registration methods.

## Options and validation

Typed options use `Microsoft.Extensions.Options`. Impossible limits, missing
model/store keys, duplicate aliases, unsafe retry combinations, and unsupported
configured capabilities SHOULD fail at host startup. Dynamic per-run values use
explicit strategies rather than mutating options monitors.

Every behaviorally meaningful mechanism or policy MUST be configurable through
DI, typed options, engine configuration, an agent definition, or an explicit run
override. Each first-party feature documents and registers sensible defaults
with a public replacement path. Security defaults fail closed; credentials,
remote endpoints, concrete stores, persistence targets, principals, and
authority have no fabricated defaults.

Credentials use dedicated providers or platform credential abstractions. They
MUST NOT appear in option display, validation messages, or configuration
snapshots.

Synchronous build consumes the catalog's memory-only `CurrentSnapshot` and
checks declared capabilities without network or secret access. An absent initial
snapshot fails readiness; build MUST NOT call or synchronously wait on an
asynchronous catalog read to obtain it. Hosts materialize remote initial
configuration asynchronously before readiness. Publication, admission, and each
effect revalidate their own evidence under the
[staged validation contract](../architecture/composition-and-configuration.md#build-validation).
Build cannot prove remote availability or the behavior of arbitrary factories.

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
- One engine resolves and runs multiple differently configured agents
  concurrently without scope or configuration leakage.
- A result distinguishes deferred, limited, cancelled, and failed without text
  parsing.
- An enabled storage-backed capability without an explicitly selected adapter
  fails build; choosing `.InMemory` or `.Sqlite` is visible in application
  composition.
- In-memory and SQLite adapters for one contract pass the same common
  conformance suite, while capability-specific tests prevent local durability
  from being reported as distributed ownership or cross-store atomicity.

## Related specifications

- [Configuration and overrides](configuration-and-overrides.md)
- [Testing and evaluation](testing-and-evaluation.md)
