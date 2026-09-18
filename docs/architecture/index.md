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

This architecture and its linked [concept specifications](../concepts/index.md)
form one normative design. Component pages own contracts, package placement, and
composition; concept pages specify their observable behavior. The
[project structure](project-structure.md) records the required solution layout.
Implementation status does not change these requirements.

## Authority and change rules

The component and concept ownership tables below identify the decision owner.
Examples, application profiles, source code, tests, and API snapshots consume
that decision; none silently overrides it. A conflict between the owning
component and its concept is a specification defect to fix in both places, not
permission to choose whichever implementation is easier.

Contract sketches reserve semantic roles and minimum data, including the prose
constraints around them. They omit implementation bodies and exhaustive XML
documentation. They are not evidence that a service exists or that an API has
passed compatibility review. Once a public API ships, changing a sketch also
requires the compatibility and migration review defined by
[foundation contracts](foundation-contracts.md#evolution-and-testing).

An architectural change must update its owner, affected concept, composition and
dependency rules, and acceptance scenarios together. Changes to ownership also
update repository guidance and the routed skill. Requirements must state their
enforcement boundary and failure outcome; claims such as atomic, durable,
deterministic, and exactly once apply only within that stated scope. An
implementation that differs is tracked and corrected against this design.

## Package direction

| Layer                            | Responsibility                                                                           |
| -------------------------------- | ---------------------------------------------------------------------------------------- |
| Applications and hosts           | Select feature packages, configuration, policy, and lifecycle                            |
| Feature and integration packages | Implement loops, context, compaction, sessions, tools, providers, storage, and protocols |
| AgentKit                         | Expose the AgentEngine facade, builder, hosted registration, and composition validation  |
| AgentKit.Abstractions            | Define provider-neutral contracts, values, events, and results                           |

The table summarizes roles; the exact edges and explicit infrastructure and
application-leaf exceptions are in [project structure](project-structure.md).
AgentKit references AgentKit.Abstractions, the shared AgentKit.Observability
infrastructure, and the required Microsoft.Extensions abstractions. It does not
pull in a loop, session implementation, provider, permission engine, storage
implementation, or tool transitively.

Concrete packages normally reference AgentKit.Abstractions. A leaf integration
may reference the implementation package whose stable extension surface it
adapts, but no foundation or implementation package references a concrete leaf.

Optional [application profiles](../profiles/index.md) compose these owners for a
product class. They consume architecture; they do not define new core owners or
justify product-level god packages.

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
run-owned DI scope. New work allocates a `RunId`; recovery of an open run
preserves its identity and acquires a new drive scope and fencing generation. No
mutable "current agent" lives on the engine, definition, a singleton, or ambient
state.

AgentKit supports two ownership modes through the same registrations:

- In standalone mode, the builder creates the service provider and the built
  engine owns its disposal.
- In hosted mode, AddAgentKit registers AgentEngine into an existing service
  collection and the host owns the provider and its lifecycle.

The canonical cardinality, readiness, and activation requirements are in
[build validation](composition-and-configuration.md#build-validation).
Engine-wide catalogs and selectors are singular; a definition resolves one
effective choice for each required keyed axis. Output processing and budgets are
part of that spine. Compaction, tools, memory, goals, MCP, and additional
semantic operations remain optional, with complete validation when configured. A
network or process-backed capability must declare its corresponding host
boundary.

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

| Component                                                         | Owns                                                                                                |
| ----------------------------------------------------------------- | --------------------------------------------------------------------------------------------------- |
| [Foundation contracts](foundation-contracts.md)                   | AgentKit.Abstractions values, interfaces, stable errors, deterministic primitives, and results      |
| [Project structure](project-structure.md)                         | Package boundaries, dependency direction, explicit storage leaves, common files, and mirrored tests |
| [Composition and configuration](composition-and-configuration.md) | AgentEngine, its builder, dependency injection, validation, configuration, and lifetimes            |
| [Agent runtime](agent-runtime.md)                                 | The replaceable loop, turn coordination, limits, cancellation, and settlement                       |
| [Budgets and limits](budgets.md)                                  | Hierarchical limits, atomic reservations, accounting, and typed exhaustion                          |
| [Messages and history](messages-and-history.md)                   | Immutable conversation truth, role trust, result projections, validation, and repair                |
| [Input and output](input-and-output.md)                           | Input admission, queues, live streams, final-result publication, and channel adapters               |
| [Structured output](structured-output.md)                         | Output definitions, keyed schema engines, preflight, validation, repair, retries, and conversion    |
| [Context](context.md)                                             | Context contributors, instruction trust, bounded assembly, selection, and request manifests         |
| [Context compaction](context-compaction.md)                       | Semantic cuts, summarization strategies, validation, reduction, and durable activation              |
| [Execution identity and tenancy](identity.md)                     | Trusted identity normalization, propagation, delegation chains, and tenant isolation                |
| [Model and embedding providers](model-and-embedding-providers.md) | Provider catalogs, endpoint/account binding, capabilities, wire adaptation, and semantic operations |
| [Tools](tools.md)                                                 | Tool runtime, terminal records, model projections, and focused feature packages                     |
| [Security and human control](permissions-and-human-control.md)    | System-wide authority, policy, approvals, bounded grants, deferral, and enforcement                 |
| [Sessions](sessions.md)                                           | Session coordination, append-only state, branching, and replaceable storage                         |
| [Durable execution](durable-execution.md)                         | Checkpoints, recovery, leases, fencing, and durable backend adaptation                              |
| [Memory and retrieval](memory-and-retrieval.md)                   | Durable memory, documents, vectors, retrieval, provenance, and deletion                             |
| [Goals and delegation](goals-and-delegation.md)                   | Goal state, attempts, delegation, agent communication, and joins                                    |
| [Hooks and extensions](extensions.md)                             | Stage-valid typed hooks, additive points, ordering, failure policy, isolation, and dispatch         |
| [Observability](observability.md)                                 | Events, traces, metrics, logs, audit, correlation, and redaction                                    |
| [MCP](mcp.md)                                                     | Protocol eras, reflected tool contracts, primitive adaptation, transports, and remote policy        |
| [File system](file-system.md)                                     | Bounded reads, explicit atomic writes, and replaceable file/directory capability contracts          |
| [Testing and evaluation](testing-and-evaluation.md)               | Mirrored tests, shared conformance, deterministic fakes, datasets, and evaluation reports           |
| [Network access](network.md)                                      | Replaceable DNS, connections, redirects, request transport, and data-egress enforcement             |
| [Process execution](process-execution.md)                         | Replaceable process creation, sandboxing, cancellation, output, and grant enforcement               |
| [Artifact and content storage](artifacts.md)                      | Durable bounded content, integrity, retention, backend selection, and secure references             |

## Concept coverage

Every normative concept has an architectural owner. Several concepts cross
components, but one component owns each decision so dependencies remain
directed.

| Concept specification                                                                           | Architectural solution and policy owner                                                                                                  |
| ----------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| [Design principles](../concepts/design-principles.md)                                           | Cross-cutting invariants in this index, enforced by composition validation and conformance tests                                         |
| [Architecture and dependency boundaries](../concepts/architecture-and-dependency-boundaries.md) | [Project structure](project-structure.md), including project/service DAG validation and explicit storage-adapter policy                  |
| [Agent definition and run context](../concepts/agent-definition-and-run-context.md)             | [Composition and configuration](composition-and-configuration.md) owns immutable definitions, run scopes, and compiled plans             |
| [Agent loop state machine](../concepts/agent-loop-state-machine.md)                             | [Agent runtime](agent-runtime.md) owns transitions and coordination, not collaborator policy                                             |
| [Run lifecycle and settlement](../concepts/run-lifecycle-and-settlement.md)                     | Agent runtime coordinates; [sessions](sessions.md) own per-lane operation exclusion and serialized mutation; I/O publishes terminals     |
| [Public API and dependency injection](../concepts/public-api-and-dependency-injection.md)       | Composition facade and [project structure](project-structure.md) own registration, validation, lifetimes, and replacement                |
| [Message and content model](../concepts/message-and-content-model.md)                           | [Messages and history](messages-and-history.md) owns immutable values, non-elevating roles, and bounded tool-result projections          |
| [Streaming and event protocol](../concepts/streaming-and-event-protocol.md)                     | [I/O](input-and-output.md) owns fan-out; providers parse candidates; sessions persist only semantic events                               |
| [Input admission and message queues](../concepts/input-admission-and-message-queues.md)         | I/O owns admission/promotion; session-selected queue storage owns durable ordering                                                       |
| [History validation and repair](../concepts/history-validation-and-repair.md)                   | Messages define contracts; [context](context.md) owns the first-party history pipeline over session reads                                |
| [Context assembly and instructions](../concepts/context-assembly-and-instructions.md)           | Context owns contributors, trust, selection, budgeting, and manifests                                                                    |
| [Sessions, persistence, and branching](../concepts/sessions-persistence-and-branching.md)       | Sessions own coordination and store selection; concrete stores are leaves                                                                |
| [Context compaction](../concepts/context-compaction.md)                                         | [Context compaction](context-compaction.md) owns safe reduction and activation without calling context assembly                          |
| [Memory, retrieval, and storage](../concepts/memory-retrieval-and-storage.md)                   | [Memory and retrieval](memory-and-retrieval.md) separates memory, documents, vectors, retrieval, and semantic operations                 |
| [Artifact and content storage](../concepts/artifact-and-content-storage.md)                     | [Artifacts](artifacts.md) own durable bytes and references outside history or memory                                                     |
| [Model providers and capabilities](../concepts/model-providers-and-capabilities.md)             | [Providers](model-and-embedding-providers.md) own catalogs, service/endpoint/account binding, operation capabilities, and adapter leaves |
| [Provider request pipeline](../concepts/provider-request-pipeline.md)                           | Providers own loss-aware translation, credentials, attempts, and parsing; [network](network.md) owns protected transport effects         |
| [Configuration and overrides](../concepts/configuration-and-overrides.md)                       | Composition owns immutable merge snapshots, definition sources, reload boundaries, and validation                                        |
| [Execution identity and tenancy](../concepts/execution-identity-and-tenancy.md)                 | [Identity](identity.md) owns trusted-ingress normalization; security consumes identity but owns authorization                            |
| [Structured output](../concepts/structured-output.md)                                           | [Structured output](structured-output.md) owns validation and returns retry decisions to the loop                                        |
| [Tools and toolsets](../concepts/tools-and-toolsets.md)                                         | [Tools](tools.md) own catalog, resolution, schemas, snapshots, and separate read/write feature packages                                  |
| [Tool-call lifecycle](../concepts/tool-call-lifecycle.md)                                       | Tools own validation through authoritative terminal recording and bounded projection; sessions provide durable append contracts          |
| [Tool scheduling and concurrency](../concepts/tool-scheduling-and-concurrency.md)               | Tool scheduler owns barrier segments and deterministic publication                                                                       |
| [Tool errors, retries, and results](../concepts/tool-errors-retries-and-results.md)             | Tools own retry safety, full terminal results, captured projection policy, and loss-aware message outcomes                               |
| [Permissions, approvals, and trust](../concepts/permissions-approvals-and-trust.md)             | [Security](permissions-and-human-control.md) owns policy, approval, grants, enforcement, revocation, and audit                           |
| [Deferred operations and human-in-the-loop](../concepts/deferred-and-human-in-the-loop.md)      | Security owns approval deferral; sessions persist it; I/O admits resolution; durability may resume it                                    |
| [MCP integration](../concepts/mcp-integration.md)                                               | [MCP](mcp.md) owns protocol lifecycle and adapters without bypassing tool, retrieval, or security contracts                              |
| [File-system access and bounds](../concepts/file-system-access-and-bounds.md)                   | [File system](file-system.md) owns path identity, bounded byte access, atomic write dispositions, and grant re-enforcement at the effect |
| [Network access and egress](../concepts/network-access-and-egress.md)                           | [Network access](network.md) owns canonical destinations, egress authorization, streaming bounds, and protected transport effects        |
| [Process execution and sandboxing](../concepts/process-execution-and-sandboxing.md)             | [Process execution](process-execution.md) owns executable resolution, sandbox enforcement, output streaming, and termination certainty   |
| [Usage limits and budgets](../concepts/usage-limits-and-budgets.md)                             | [Budgets](budgets.md) own hierarchy and atomic reservations; consumers own their estimates and responses                                 |
| [Cancellation, timeouts, and resilience](../concepts/cancellation-timeouts-and-resilience.md)   | Agent runtime owns cancellation composition; each effect owner owns safe retry and uncertainty—there is no generic replay layer          |
| [Extensions, hooks, and middleware](../concepts/extensions-hooks-and-middleware.md)             | [Hooks](extensions.md) own typed additive points, stage identity, ordering strength, monotonic failure, rollback, and reentrancy         |
| [Durable execution and recovery](../concepts/durable-execution-and-recovery.md)                 | [Durable execution](durable-execution.md) owns checkpoints, leases, fencing, recovery, and backend adaptation                            |
| [Observability and audit](../concepts/observability-and-audit.md)                               | [Observability](observability.md) owns immutable sinks and exporters; it never becomes a control dependency                              |
| [Goals and multi-agent delegation](../concepts/goals-and-multi-agent-delegation.md)             | [Goals and delegation](goals-and-delegation.md) own goal state, scoped child work, joins, and communication                              |
| [Error taxonomy](../concepts/error-taxonomy.md)                                                 | Stable error values live in Abstractions; each boundary maps its own failures before observation—no central error manager                |
| [Testing and evaluation](../concepts/testing-and-evaluation.md)                                 | [Testing and evaluation](testing-and-evaluation.md) own mirrored tests, conformance, fault injection, and behavioral evals               |

## Application-profile coverage

The [coding-harness profile](../profiles/coding-harness/index.md) maps its
workspace, mutation, terminal, language-service, snapshot, resource,
control-plane, frontend, and MCP requirements to the owners above. Those
requirements apply only to applications claiming profile conformance and never
expand AgentKit's mandatory runtime spine.

## Contract and registration map

| Boundary                             | Neutral selection/contract shape                                                                                  | First-party registration and replacement                                                                                                                                                                                                    |
| ------------------------------------ | ----------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Engine and agents                    | `IAgentDefinitionCatalog`, immutable `AgentDefinition`, run-owned scope                                           | AgentKit foundation; engine-wide singular catalog/validator and additive definition sources, all replaceable                                                                                                                                |
| Runnable spine                       | Keyed loop, I/O, context, output, hooks, session, security, provider, and budget selections; opt-in compaction    | `AddAgentLoop`, `AddAgentIO`, `AddAgentContext`, `AddAgentContextCompaction`, `AddAgentOutput`, `AddAgentHooks`, `AddAgentSession`, `AddAgentPermissions`, `AddAgentProviders`, `AddAgentBudgets`; one effective selection per enabled axis |
| Provider operations                  | Keyed operation/model registrations with captured endpoint/service-surface and credential/account profiles        | Concrete provider packages own independently keyed profiles and bind each operation explicitly; shared wire families own mechanics, not provider policy                                                                                     |
| Tools                                | Additive providers, explicit capture/invoker leases, source-version snapshots, and authoritative terminal records | `AgentKit.Tools` owns executor normalization of raw invocation evidence; capture lifetimes, recorder, and projection policy are explicit and replaceable                                                                                    |
| State and recovery                   | Engine-wide catalogs with keyed per-agent session, memory, vector, and durability selections                      | Runtimes register no concrete store; applications explicitly select `.InMemory`, `.Sqlite`, `.Json`, or another backend leaf whose declared capabilities satisfy the profile                                                                |
| Observation                          | Shared Microsoft diagnostics plus additive keyed `IRunEventSink` and `ISecurityAuditSink`                         | AgentKit.Observability names/registration, then AgentKit.Observability.OpenTelemetry or custom sinks; no hidden exporter                                                                                                                    |
| MCP                                  | Shared reflection/version contracts, typed clients, keyed endpoints, and primitive adapters                       | `AddMcpToolClient`, `AddAgentKitMcpServer`, `AddMcpStdioEndpoint`, and `AddMcpHttpEndpoint`                                                                                                                                                 |
| Host file/network/process boundaries | Narrow capability profiles with one effective selection per consumer; network baseline is one engine-wide pair    | File writes require explicit atomic disposition and separately authorized parent creation; in-memory/scripted packages are explicit alternatives                                                                                            |
| Identity and artifacts               | Immutable execution identity plus keyed artifact coordinator/store contracts                                      | `AddAgentIdentity` at trusted ingress and `AddAgentArtifacts` with explicit backend leaves                                                                                                                                                  |
| Evaluation                           | Singular `IEvaluationRunner`, keyed additive evaluators/stores/exporters                                          | `AddAgentEvaluation`; optional and uses only public engine surfaces                                                                                                                                                                         |

Static application tool discovery uses explicit immutable source publications
and exact borrowed invoker bindings. Each discovery owns a fresh capture;
replacement under the exact typed source key affects later composition without
redirecting retained providers or leases. Catalog selection and merge policy
stay separate from discovery registration.

Materialized `IToolRegistrationCatalog` selection now captures explicit toolset
publications and exact provider bindings once at composition. Public typed
registration supports static and dynamic providers with explicit replacement.
Selection preserves authored order, includes shared sources once, and rejects
missing toolsets or policy families before discovery. Empty selection exposes no
fallback; retained views keep their original bindings without runtime container
lookup or transferred disposal ownership.

Catalog merge policy now receives complete ordered candidate and collision
evidence. The first-party default rejects collisions; configured policies can
select existing contributions subject to complete source, descriptor, policy,
and alias revalidation. Merge preserves authored order and empty sources and
owns no captures. Canonical schema/model-capability preflight and migration from
the legacy catalog/loop path remain open.

Complete source acquisition now has an explicit internal discovery owner. It
retains late and malformed captures for cleanup, freezes publications before
merge/preflight, and transfers source ownership once without live metadata
rereads. Cleanup starts every owner before awaiting any and preserves ordered
failures. Schema/model-capability preflight and canonical catalog/loop migration
remain open.

Retained tool-source and catalog captures close acquisition and drain pending
acquisitions and invoker leases before owned cleanup. Their
[lifetime contract](tools.md#discovery-resolution-validation-and-selection)
preserves borrowed host ownership and shares cleanup failure without retrying
it.

Singular defaults use `TryAdd` and an explicit replacement path. Additive
registrations retain deterministic order. Keyed registrations use stable unique
keys and an injected catalog/selector, never `IServiceProvider` as a locator.
Calling the same package registration twice is idempotent when configuration is
identical; a conflicting duplicate fails validation unless that API documents a
deliberate merge.

Canonical tool schemas now have a separate local `IToolSchemaEngine` compilation
boundary and immutable `ICompiledToolSchema` validation handles. Contracts live
in abstractions; the bounded first-party draft 2020-12 subset lives in Tools and
uses explicit profiles, resource/work bounds, typed outcomes, and replaceable
registration. Full canonical evidence is retained; unsupported keywords reject.
Provider/model translation preflight and catalog/loop integration remain open.

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
7. The provider runtime reserves budget and selects a compatible operation whose
   model, endpoint/service-surface profile, and credential/account profile are
   already captured; a concrete provider package then performs one request.
8. The output stream exposes typed provisional events while the loop builds a
   candidate response.
9. The output processor validates or returns a bounded repair/retry decision to
   the loop. An accepted assistant response is committed to the session.
10. Requested tools are resolved against the request's catalog snapshot,
    authorized by the security authority, accepted-recorded, and executed with a
    bounded grant enforced again by the effecting file, network, or process
    component. Every identified request reaches one authoritative terminal
    record, including pre-invocation rejection.
11. The captured projection policy produces bounded `ToolResultPart` values,
    which are committed in deterministic source order before the loop decides
    whether another turn is required. Projection/publication retry never repeats
    the tool effect; oversized or binary content may become an authorized
    artifact reference.
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
- Model text, retrieved content, tool metadata, runtime notices, and remote
  protocol metadata do not grant authority or system/developer instruction
  precedence.
- Every tool call admitted to invocation is durably recorded before its side
  effect; every bounded, identified call reaches exactly one authoritative
  terminal result and bounded correlated projection.
- A tool-result projection preserves requested alias, resolved identity when
  available, exact source status, uncertainty, loss, and captured policy
  version; it never substitutes for or reconstructs the terminal record.
- Each provider operation captures independently keyed endpoint/service-surface,
  credential/account, and operation/model bindings. Missing usage remains
  unknown, and unexpected response candidates are never discarded silently.
  Credential-profile references remain classified execution/audit evidence, not
  assistant-message metadata.
- Configuration, catalogs, and context are immutable snapshots while an
  operation is in flight.
- Hook contracts expose only identities established at their lifecycle stage.
  Point definitions are typed and additive; ordering strength and failure-policy
  precedence are explicit, isolated failures leak no partial mutation, and no
  hook can widen security authority.
- Time-dependent framework behavior uses the injected TimeProvider.
- Random selection, jitter, identifiers, and fingerprints use injected
  deterministic primitives with replaceable production defaults.
- Both project references and closed runtime service dependencies form directed
  acyclic graphs; factories and lazy resolution cannot hide reverse edges.
- Replacement implementations preserve observable behavior through shared
  conformance suites.
- OpenAI compatibility is a reusable tested wire profile, never a substitute for
  concrete provider identity.
