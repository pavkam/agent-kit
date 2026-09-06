# AgentKit architecture

**Status:** Project architecture

AgentKit is a set of independently selectable .NET 10 libraries for composing
agentic applications. A built `AgentEngine` is the process-level engine: one
immutable composition root can catalog, host, and run many immutable agent
definitions concurrently. It is not a mutable object representing one agent. The
AgentKit package is the facade, not the runtime. It builds that engine from
contracts and feature registrations supplied by the host.

The architecture follows six rules:

1. Every meaningful behavior has one owner.
2. Components depend on provider-neutral contracts, not sibling implementations.
3. Policy is evaluated before I/O or side effects.
4. Durable facts and live activity are represented separately.
5. Every behaviorally meaningful choice is explicit in DI, validated options, an
   immutable agent definition, or bounded run options.
6. Domain identities are dedicated validated types, never interchangeable raw
   strings or integers.

The specifications in [concepts](../concepts/index.md) define normative
behavior. These documents describe how that behavior is divided into components
and packages. The [project structure](project-structure.md) records the concrete
solution layout.

## Package direction

| Layer                            | Responsibility                                                                           |
| -------------------------------- | ---------------------------------------------------------------------------------------- |
| Applications and hosts           | Select feature packages, configuration, policy, and lifecycle                            |
| Feature and integration packages | Implement loops, context, compaction, sessions, tools, providers, storage, and protocols |
| AgentKit                         | Expose the AgentEngine facade, builder, hosted registration, and composition validation  |
| AgentKit.Abstractions            | Define provider-neutral contracts, values, events, and results                           |

Dependencies point down this table. AgentKit references AgentKit.Abstractions
and the required Microsoft.Extensions abstractions. It does not pull in a loop,
session implementation, provider, permission engine, storage implementation, or
tool transitively.

Concrete packages normally reference AgentKit.Abstractions. A leaf integration
may reference the implementation package whose stable extension surface it
adapts, but no foundation or implementation package references a concrete leaf.

## Composition

`AgentEngine` is the immutable process-level facade. `AgentEngineBuilder` is the
mutable composition surface returned by `AgentEngine.CreateBuilder`. The builder
exposes its service collection, so every feature uses ordinary ASP.NET-style
dependency-injection registration. The resulting topology is:

```text
AgentEngine (one built root provider and engine-wide catalogs)
├── AgentDefinition A (immutable keyed selections and behavioral defaults)
│   ├── RunScope A1 (RunId, SessionId, mutable run-owned state)
│   └── RunScope A2 (isolated and concurrently executable)
├── AgentDefinition B
│   └── RunScope B1
└── additional definitions and run scopes
```

Engine-wide catalogs contain validated registrations and immutable snapshots for
agent definitions, models/providers, tool sources, hooks, policies, stores,
channels, and optional host-access profiles. An `AgentDefinition` selects those
capabilities by typed or stable keyed references and adds its instructions,
toolsets, limits, and policy defaults. Starting or resuming work creates a new
run-owned DI scope and `RunId`; no mutable "current agent" lives on the engine,
definition, a singleton, or ambient state.

AgentKit supports two ownership modes through the same registrations:

- In standalone mode, the builder creates the service provider and the built
  engine owns its disposal.
- In hosted mode, AddAgentKit registers AgentEngine into an existing service
  collection and the host owns the provider and its lifecycle.

Engine-wide validation requires one agent-definition catalog and validator, one
`TimeProvider`, and an effective `IIdentifierGenerator<TIdentifier>` for every
identifier the selected composition creates. Each published `AgentDefinition`
must then resolve exactly one effective keyed loop, input coordinator, output
publisher, session coordinator/store selection, context assembler, hook
dispatcher/catalog, security authority/policy profile, approval broker, model
selector, and model request executor, plus a compatible conversational model.
Engine-wide model and service catalogs may contain many implementations; the
definition's selection may not be missing or ambiguous. Dynamic catalog reloads
pass the same validation before publication. `TimeProvider.System` and the
cryptographically strong generic identifier generator are documented,
replaceable foundation defaults. Tools, skills, memory, embeddings, reranking,
goals, MCP, and additional context contributors are optional. A registered
optional capability must still be complete; a tool registration, for example,
cannot build without its execution and security pipeline. A feature whose
descriptor performs network I/O requires a configured network implementation; a
process-backed tool or stdio transport requires a process implementation.

## Configuration and default ownership

DI selects implementations, lifetimes, additive contributors, and keyed
catalogs. Validated package options select process-wide mechanics and safe
defaults. Immutable agent definitions select reusable per-agent behavior. Run
options contain only bounded dynamic overrides and are captured in the run's
effective configuration snapshot.

Defaults belong to the package that implements them and are registered only when
the application calls that package's `Add...` method. They use `TryAdd` or an
equally explicit collision rule and always have a public replacement path. The
facade may register its documented foundation defaults for time, identifier
generation, definition validation/cataloging, and lifecycle ownership; it never
secretly pulls a loop, provider, store, security implementation, host-access
implementation, exporter, tool, or protocol package. Missing behavior is a
composition error, not a cue to inspect environment variables, use ambient
state, or instantiate a concrete fallback.

## Typed identity model

Core identities include `AgentId`, `SessionId`, `RunId`, `TurnId`, `MessageId`,
`ToolCallId`, `GoalId`, and `OperationId`. Component documents add semantically
distinct types such as `SecurityAuditRecordId`, `McpSessionId`, `McpRequestId`,
`EvaluationRunId`, `FileOperationId`, `NetworkOperationId`, and
`ProcessOperationId`. Each is a dedicated `readonly record struct` in its owning
contract package; none is an alias for another ID merely because their wire
representation matches.

The canonical API pattern is defined once in
[Composition and configuration](composition-and-configuration.md): each ID is a
`readonly record struct`; construction, parsing, and deserialization reject
empty, default, malformed, or non-canonical values; and serialization preserves
one canonical representation. Framework-created values come from the appropriate
closed `IIdentifierGenerator<TIdentifier>` registration and its `Create()`
operation. Foundation registrations provide thread-safe production generators,
while tests replace them with deterministic sequences. Deserialization preserves
an existing ID and never generates a new one as repair.

## Components

| Component                                                         | Owns                                                                                             |
| ----------------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| [Foundation contracts](foundation-contracts.md)                   | AgentKit.Abstractions values, interfaces, stable errors, deterministic primitives, and results   |
| [Project structure](project-structure.md)                         | Package boundaries, dependency direction, common files, and mirrored tests                       |
| [Composition and configuration](composition-and-configuration.md) | AgentEngine, its builder, dependency injection, validation, configuration, and lifetimes         |
| [Agent runtime](agent-runtime.md)                                 | The replaceable loop, turn coordination, limits, cancellation, and settlement                    |
| [Budgets and limits](budgets.md)                                  | Hierarchical limits, atomic reservations, accounting, and typed exhaustion                       |
| [Messages and history](messages-and-history.md)                   | The immutable conversation model, durable history rules, validation, and repair                  |
| [Input and output](input-and-output.md)                           | Input admission, queues, live streams, final-result publication, and channel adapters            |
| [Structured output](structured-output.md)                         | Output definitions, extraction, validation, repair decisions, retries, and conversion            |
| [Context](context.md)                                             | Context assembly plus the separate AgentKit.Context.Compaction implementation boundary           |
| [Context compaction](context-compaction.md)                       | Semantic cuts, summarization strategies, validation, reduction, and durable activation           |
| [Execution identity and tenancy](identity.md)                     | Trusted identity normalization, propagation, delegation chains, and tenant isolation             |
| [Model and embedding providers](model-and-embedding-providers.md) | Capability discovery, selection, wire adaptation, streaming, and embeddings                      |
| [Tools](tools.md)                                                 | Tool runtime and feature packages for discovery, validation, scheduling, invocation, and results |
| [Security and human control](permissions-and-human-control.md)    | System-wide authority, policy, approvals, bounded grants, deferral, and enforcement              |
| [Sessions](sessions.md)                                           | Session coordination, append-only state, branching, and replaceable storage                      |
| [Durable execution](durable-execution.md)                         | Checkpoints, recovery, leases, fencing, and durable backend adaptation                           |
| [Memory and retrieval](memory-and-retrieval.md)                   | Durable memory, documents, vectors, retrieval, provenance, and deletion                          |
| [Goals and delegation](goals-and-delegation.md)                   | Goal state, attempts, delegation, agent communication, and joins                                 |
| [Hooks and extensions](extensions.md)                             | Typed lifecycle hooks, allowed mutation, ordering, isolation, and dispatch                       |
| [Observability](observability.md)                                 | Events, traces, metrics, logs, audit, correlation, and redaction                                 |
| [MCP](mcp.md)                                                     | Protocol lifecycle, primitive adaptation, transports, and remote capability policy               |
| [File system](file-system.md)                                     | Replaceable file and directory operations used by the framework and tools                        |
| [Testing and evaluation](testing-and-evaluation.md)               | Mirrored tests, shared conformance, deterministic fakes, datasets, and evaluation reports        |
| [Network access](network.md)                                      | Replaceable DNS, connections, redirects, request transport, and data-egress enforcement          |
| [Process execution](process-execution.md)                         | Replaceable process creation, sandboxing, cancellation, output, and grant enforcement            |
| [Artifact and content storage](artifacts.md)                      | Durable bounded content, integrity, retention, backend selection, and secure references          |

## Concept coverage

Every normative concept has an architectural owner. Several concepts cross
components, but one component owns each decision so dependencies remain
directed.

| Concept specification                                                                           | Architectural solution and policy owner                                                                                            |
| ----------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| [Design principles](../concepts/design-principles.md)                                           | Cross-cutting invariants in this index, enforced by composition validation and conformance tests                                   |
| [Architecture and dependency boundaries](../concepts/architecture-and-dependency-boundaries.md) | [Project structure](project-structure.md), including separate project and service DAG validation                                   |
| [Agent definition and run context](../concepts/agent-definition-and-run-context.md)             | [Composition and configuration](composition-and-configuration.md) owns immutable definitions, run scopes, and compiled plans       |
| [Agent loop state machine](../concepts/agent-loop-state-machine.md)                             | [Agent runtime](agent-runtime.md) owns transitions and coordination, not collaborator policy                                       |
| [Run lifecycle and settlement](../concepts/run-lifecycle-and-settlement.md)                     | Agent runtime coordinates; [sessions](sessions.md) own active-run exclusion; [I/O](input-and-output.md) publishes terminal results |
| [Public API and dependency injection](../concepts/public-api-and-dependency-injection.md)       | Composition facade and [project structure](project-structure.md) own registration, validation, lifetimes, and replacement          |
| [Message and content model](../concepts/message-and-content-model.md)                           | [Messages and history](messages-and-history.md) owns immutable values; no implementation package is invented for records           |
| [Streaming and event protocol](../concepts/streaming-and-event-protocol.md)                     | [I/O](input-and-output.md) owns fan-out; providers parse candidates; sessions persist only semantic events                         |
| [Input admission and message queues](../concepts/input-admission-and-message-queues.md)         | I/O owns admission/promotion; session-selected queue storage owns durable ordering                                                 |
| [History validation and repair](../concepts/history-validation-and-repair.md)                   | Messages define contracts; [context](context.md) owns the first-party history pipeline over session reads                          |
| [Context assembly and instructions](../concepts/context-assembly-and-instructions.md)           | Context owns contributors, trust, selection, budgeting, and manifests                                                              |
| [Sessions, persistence, and branching](../concepts/sessions-persistence-and-branching.md)       | Sessions own coordination and store selection; concrete stores are leaves                                                          |
| [Context compaction](../concepts/context-compaction.md)                                         | [Context compaction](context-compaction.md) owns safe reduction and activation without calling context assembly                    |
| [Memory, retrieval, and storage](../concepts/memory-retrieval-and-storage.md)                   | [Memory and retrieval](memory-and-retrieval.md) separates memory, documents, vectors, retrieval, and semantic operations           |
| [Artifact and content storage](../concepts/artifact-and-content-storage.md)                     | [Artifacts](artifacts.md) own durable bytes and references outside history or memory                                               |
| [Model providers and capabilities](../concepts/model-providers-and-capabilities.md)             | [Providers](model-and-embedding-providers.md) own catalogs, selection, operations, capabilities, and adapter leaves                |
| [Provider request pipeline](../concepts/provider-request-pipeline.md)                           | Providers own attempts and wire adapters; [network](network.md) owns protected transport effects                                   |
| [Configuration and overrides](../concepts/configuration-and-overrides.md)                       | Composition owns immutable merge snapshots, definition sources, reload boundaries, and validation                                  |
| [Execution identity and tenancy](../concepts/execution-identity-and-tenancy.md)                 | [Identity](identity.md) owns trusted-ingress normalization; security consumes identity but owns authorization                      |
| [Structured output](../concepts/structured-output.md)                                           | [Structured output](structured-output.md) owns validation and returns retry decisions to the loop                                  |
| [Tools and toolsets](../concepts/tools-and-toolsets.md)                                         | [Tools](tools.md) own catalog, resolution, schemas, snapshots, and feature packages                                                |
| [Tool-call lifecycle](../concepts/tool-call-lifecycle.md)                                       | Tools own validation through terminal recording; sessions provide durable append contracts                                         |
| [Tool scheduling and concurrency](../concepts/tool-scheduling-and-concurrency.md)               | Tool scheduler owns barrier segments and deterministic publication                                                                 |
| [Tool errors, retries, and results](../concepts/tool-errors-retries-and-results.md)             | Tools own retry safety and normalization; large results may become artifact references                                             |
| [Permissions, approvals, and trust](../concepts/permissions-approvals-and-trust.md)             | [Security](permissions-and-human-control.md) owns policy, approval, grants, enforcement, revocation, and audit                     |
| [Deferred operations and human-in-the-loop](../concepts/deferred-and-human-in-the-loop.md)      | Security owns approval deferral; sessions persist it; I/O admits resolution; durability may resume it                              |
| [MCP integration](../concepts/mcp-integration.md)                                               | [MCP](mcp.md) owns protocol lifecycle and adapters without bypassing tool, retrieval, or security contracts                        |
| [Usage limits and budgets](../concepts/usage-limits-and-budgets.md)                             | [Budgets](budgets.md) own hierarchy and atomic reservations; consumers own their estimates and responses                           |
| [Cancellation, timeouts, and resilience](../concepts/cancellation-timeouts-and-resilience.md)   | Agent runtime owns cancellation composition; each effect owner owns safe retry and uncertainty—there is no generic replay layer    |
| [Extensions, hooks, and middleware](../concepts/extensions-hooks-and-middleware.md)             | [Hooks](extensions.md) own typed dispatch, ordering, mutation validation, and reentrancy bounds                                    |
| [Durable execution and recovery](../concepts/durable-execution-and-recovery.md)                 | [Durable execution](durable-execution.md) owns checkpoints, leases, fencing, recovery, and backend adaptation                      |
| [Observability and audit](../concepts/observability-and-audit.md)                               | [Observability](observability.md) owns immutable sinks and exporters; it never becomes a control dependency                        |
| [Goals and multi-agent delegation](../concepts/goals-and-multi-agent-delegation.md)             | [Goals and delegation](goals-and-delegation.md) own goal state, scoped child work, joins, and communication                        |
| [Error taxonomy](../concepts/error-taxonomy.md)                                                 | Stable error values live in Abstractions; each boundary maps its own failures before observation—no central error manager          |
| [Testing and evaluation](../concepts/testing-and-evaluation.md)                                 | [Testing and evaluation](testing-and-evaluation.md) own mirrored tests, conformance, fault injection, and behavioral evals         |
| [Research provenance](../concepts/research-provenance.md)                                       | Evidence informs specifications; it has no runtime service or package dependency                                                   |

## Contract and registration map

| Boundary                             | Neutral selection/contract shape                                                                        | First-party registration and replacement                                                                                                                                                                                                    |
| ------------------------------------ | ------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Engine and agents                    | `IAgentDefinitionCatalog`, immutable `AgentDefinition`, run-owned scope                                 | AgentKit foundation; engine-wide singular catalog/validator and additive definition sources, all replaceable                                                                                                                                |
| Runnable spine                       | Keyed loop, I/O, context, compaction, output, hooks, session, security, provider, and budget selections | `AddAgentLoop`, `AddAgentIO`, `AddAgentContext`, `AddAgentContextCompaction`, `AddAgentOutput`, `AddAgentHooks`, `AddAgentSession`, `AddAgentPermissions`, `AddAgentProviders`, `AddAgentBudgets`; one effective selection per enabled axis |
| Models and tools                     | Keyed model operations; additive tool providers with catalog snapshots                                  | Concrete provider and `AgentKit.Tools.*` package registrations; keys are unique and selections explicit                                                                                                                                     |
| State and recovery                   | Engine-wide catalogs with keyed per-agent session, memory, vector, and durability selections            | `AgentKit.Session`, `AgentKit.Memory`, and provider-neutral `AgentKit.Durability` / `AddAgentDurability`; concrete durability backends remain `AgentKit.Durability.<BackendName>` leaves                                                    |
| Observation                          | Additive keyed `IRunEventSink` and `ISecurityAuditSink`                                                 | AgentKit.Observability.OpenTelemetry or custom sinks; no hidden exporter                                                                                                                                                                    |
| MCP                                  | Singular client/session factory, additive keyed endpoints and primitive adapters                        | `AddMcpClient`, `AddMcpStdioEndpoint`, `AddMcpHttpEndpoint`, and `AddMcpServer`                                                                                                                                                             |
| Host file/network/process boundaries | Narrow keyed capability profiles with one effective selection per consumer                              | `AddOperatingSystemFileSystem`, `AddAgentNetwork`, and `AddAgentProcesses`; in-memory/scripted packages are explicit alternatives                                                                                                           |
| Identity and artifacts               | Immutable execution identity plus keyed artifact coordinator/store contracts                            | `AddAgentIdentity` at trusted ingress and `AddAgentArtifacts` with explicit backend leaves                                                                                                                                                  |
| Evaluation                           | Singular `IEvaluationRunner`, keyed additive evaluators/stores/exporters                                | `AddAgentEvaluation`; optional and uses only public engine surfaces                                                                                                                                                                         |

Singular defaults use `TryAdd` and an explicit replacement path. Additive
registrations retain deterministic order. Keyed registrations use stable unique
keys and an injected catalog/selector, never `IServiceProvider` as a locator.
Calling the same package registration twice is idempotent when configuration is
identical; a conflicting duplicate fails validation unless that API documents a
deliberate merge.

## Runtime flow

A normal run for one of the engine's agent definitions moves through the
components in a deliberate order:

1. The facade validates registrations and builds an immutable AgentEngine.
2. Trusted ingress supplies an immutable execution identity; the engine resolves
   and validates the requested `AgentId`/definition against its immutable
   catalogs, generates typed operation identities, and creates an isolated run
   scope from the selected components.
3. The budget authority creates the run scope and its bounded child budgets.
4. The I/O component authorizes, validates, admits, and promotes input at a safe
   boundary.
5. The session provides a stable history version for the active branch.
6. Context contributors gather instructions, skills, tools, memory, goals, and
   runtime facts into a bounded request view.
7. The provider runtime reserves budget, selects a compatible model, then a
   concrete provider package performs one model request.
8. The output stream exposes typed provisional events while the loop builds a
   candidate response.
9. The output processor validates or returns a bounded repair/retry decision to
   the loop. An accepted assistant response is committed to the session.
10. Requested tools are resolved against the request's catalog snapshot,
    authorized by the security authority, recorded, and executed with a bounded
    grant enforced again by the effecting file, network, or process component.
11. Tool results are committed in deterministic source order and the loop
    decides whether another turn is required. Oversized or binary results may be
    externalized through the artifact coordinator before their references are
    committed.
12. The run produces one typed terminal outcome and settles all required work.

Goals, deferrals, recovery, and queued follow-up input can start later runs, but
they do not weaken these boundaries.

## Cross-cutting invariants

- Every run, turn, request, message, input, tool call, goal, and durable
  operation has a stable typed identity and causal relationship; raw strings or
  integers never cross domain boundaries as identity.
- One engine may run many agent definitions concurrently; definitions and
  catalogs are immutable, while mutable work is isolated to its run scope.
- Every behaviorally meaningful default is documented, package-owned, safely
  replaceable, and captured in the effective configuration or catalog snapshot.
- Provider output is a proposal until validated and committed.
- Model text, retrieved content, tool metadata, and remote protocol metadata do
  not grant authority.
- Tool calls are durably recorded before side effects begin.
- Every accepted tool call and run reaches exactly one terminal result.
- Configuration, catalogs, and context are immutable snapshots while an
  operation is in flight.
- Hook mutation is limited by dedicated event arguments, validated after each
  hook, and can never widen security authority.
- Time-dependent framework behavior uses the injected TimeProvider.
- Random selection, jitter, identifiers, and fingerprints use injected
  deterministic primitives with replaceable production defaults.
- Both project references and closed runtime service dependencies form directed
  acyclic graphs; factories and lazy resolution cannot hide reverse edges.
- Replacement implementations preserve observable behavior through shared
  conformance suites.
- OpenAI compatibility is a reusable tested wire profile, never a substitute for
  concrete provider identity.
