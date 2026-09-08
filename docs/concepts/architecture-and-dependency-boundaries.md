# Architecture and dependency boundaries

**Status:** Normative

**Architecture:** [Project structure](../architecture/project-structure.md)

**Depends on:** [Design principles](design-principles.md)

## Package topology

The concrete package names and allowed edges live in the
[project structure](../architecture/project-structure.md); this specification
defines the rules those edges must satisfy.

```text
applications / hosts
        │
        ├──────────────> AgentKit facade ─────┐
        │                                     │
        └──────────────> AgentKit.* features ─┤
                                              v
                                   AgentKit.Abstractions
                                              │
                                              v
                                     BCL + Extensions abstractions
```

`AgentKit.Abstractions` MUST contain immutable domain values and narrow
provider-neutral contracts. It MUST NOT reference `AgentKit`, provider SDKs,
storage clients, protocol SDKs, or hosting frameworks beyond stable
`Microsoft.Extensions.*` abstractions justified by the public contract.

`AgentKit` MUST be the dependency-light facade containing `AgentEngine`,
`AgentEngineBuilder`, hosted registration, composition validation, and lifecycle
ownership. It MAY depend on `AgentKit.Abstractions` and stable
`Microsoft.Extensions.*` abstractions. It MUST NOT depend on a loop, context
assembler, session implementation, provider, policy implementation, store, tool,
transport, MCP, or hosting package.

`AgentEngine` is the complete process-level composition, not one agent. It MUST
host a versioned catalog of immutable agent definitions and safely run multiple
agents and sessions concurrently through isolated run scopes. An engine-bound
`Agent` handle MUST NOT own mutable run or session state.

First-party implementations MUST live in focused packages such as
`AgentKit.Loop`, `AgentKit.Context`, `AgentKit.Hooks`, `AgentKit.IO`,
`AgentKit.Session`, `AgentKit.Permissions`, `AgentKit.Providers`, and
`AgentKit.Tools`. Feature families use `AgentKit.Tools.<ToolName>`,
`AgentKit.Providers.<ProviderName>`, and `AgentKit.Session.<StorageProvider>`.
Concrete provider, storage, filesystem, transport, MCP, and durable-backend
packages are leaves. Foundation and runtime packages MUST NOT reference a
concrete leaf.

Storage-owning runtimes define narrow contracts, immutable capability
descriptors, catalogs, selection, and domain coordination. They MUST NOT
register a concrete store. First-party implementations use
`AgentKit.<Owner>.InMemory` for deterministic ephemeral behavior and
`AgentKit.<Owner>.Sqlite` for durable local behavior when SQLite can implement
the contract honestly; other backends use `AgentKit.<Owner>.<ProviderName>`.
Every adapter is selected explicitly by key or singular registration. A missing
selection fails composition rather than falling back to process memory or
registration order.

Authoritative mutable domain state that survives one operation MUST be accessed
through its provider-neutral storage contract even when the first implementation
is process-local. This rule separates coordination from a concrete medium; it
does not rename immutable catalogs, local gates, or disposable operation state
as stores.

The in-memory and SQLite leaves MUST run the same reusable conformance suite for
their shared contract. Adapter descriptors state actual atomicity, isolation,
durability, concurrency, pagination, fencing, and migration capabilities. SQLite
durability does not imply distributed ownership, a cross-database transaction,
or support for an optional operation. Process-local caches, immutable
publication snapshots, and synchronization gates are implementation mechanics
rather than persistence adapters unless they expose authoritative domain state
through a storage contract.

A runtime adapter that projects domain behavior through an already selected
store contract is allowed and does not create another persistence medium.
Session-backed input, plan, goal, or settlement behavior reuses the selected
session transaction boundary; it MUST NOT open its own database or require a
parallel `<Projection>.Sqlite` package.

`AgentKit.Providers` MUST contain only provider-neutral catalog, selection,
capability-validation, and attempt-coordination behavior.
`AgentKit.Providers.OpenAICompatible` MAY contain reusable protocol-family base
classes and services. It MUST NOT replace concrete identity, options, profiles,
credentials, or registration in packages such as `AgentKit.Providers.OpenAI`,
`AgentKit.Providers.OpenRouter`, and `AgentKit.Providers.ZAI`.

The shared exporter-free `AgentKit.Observability` package is an explicit
infrastructure dependency of first-party facade, runtime, and leaf packages. It
may depend on Abstractions and Microsoft diagnostics/logging abstractions, never
on its consumers or exporter SDKs. Behavioral runtimes still cannot reference
sibling implementations. Evaluation and goal-worker hosting are application
leaves allowed to drive the public facade; no runtime or facade references those
leaves. These exceptions are enumerated in
[project structure](../architecture/project-structure.md#acyclic-dependency-graphs),
not inferred from arbitrary package names.

## Extension-axis rule

Contracts MUST separate these axes when implementations can vary independently:

| Axis        | Responsibility                  | Example contract     |
| ----------- | ------------------------------- | -------------------- |
| Description | Immutable capability metadata   | `ModelDescriptor`    |
| Discovery   | Enumerate candidates            | `IToolProvider`      |
| Selection   | Choose a candidate              | `IModelSelector`     |
| Resolution  | Bind identity to implementation | `IToolResolver`      |
| Execution   | Perform one operation           | `IToolInvoker`       |
| Policy      | Permit, deny, or defer          | `ISecurityAuthority` |
| State       | Load and append domain records  | `ISessionStore`      |
| Observation | Receive immutable events        | `IRunEventSink`      |

A public abstraction SHOULD NOT be added for a single known implementation
unless it establishes a security, testing, or package boundary.

## Lifetimes and ownership

Every service contract MUST document:

- DI lifetime and whether instances are thread-safe;
- who owns and disposes returned streams, leases, and child resources;
- whether state survives a request, run, session, or process;
- allowed concurrency and reentrancy;
- which layer owns retries and timeouts; and
- whether cancellation guarantees no new work or merely stops awaiting it.

Run-scoped mutable state MUST live in an explicit `AgentRunContext` or a
run-owned service scope. It MUST NOT live on singleton agent definitions,
provider descriptors, static registries, or `AsyncLocal` as the authoritative
store.

## Capabilities, not surprise exceptions

The [model capability contract](model-providers-and-capabilities.md) applies
this rule to provider, deployment, and model combinations before request I/O.

Optional behavior MUST be reported through descriptors, capability interfaces,
or discriminated results before use. Discovering lack of support through a
mid-run `NotSupportedException` is non-conforming.

Capability claims describe the selected model and configured deployment, not the
theoretical union of everything a provider has ever offered.

## Composition

The [public API and DI specification](public-api-and-dependency-injection.md)
turns these dependency rules into replaceable registrations, lifetimes, and
build-time validation.

Registration extensions MUST:

- return `IServiceCollection`;
- never build or resolve a service provider;
- state whether registration is singular, additive, keyed, replaceable, and
  idempotent;
- validate impossible option combinations at startup; and
- register defaults so applications can replace them without removing hidden
  internal services.

Every behaviorally meaningful mechanism and policy MUST be configurable at one
named boundary: DI, typed options, engine configuration, immutable agent
definition, or explicit run override. A first-party feature package SHOULD
register a sensible documented default with `TryAdd` semantics when a safe
general default exists. Concrete stores, credentials, external endpoints,
persistence targets, and granted authority MUST remain explicit.

`AgentEngine.CreateBuilder()` MUST return a separate mutable builder whose
`Services` property is the ordinary `IServiceCollection` composition surface.
`Build()` MUST return an immutable engine and validate the complete graph.
`AddAgentKit` MUST register the same facade and validation into an externally
owned service collection.

Standalone and host-managed composition MUST share one registration path. The
standalone engine owns the provider created by its builder. A hosted engine MUST
NOT dispose the host's provider.

Agent definitions are additive registrations with unique typed `AgentId` values.
The definition catalog is singular and replaceable; its default implementation
publishes immutable versioned snapshots. Build validation MUST validate every
registered definition against the keyed capabilities it selects and reject
unresolved duplicate IDs before the engine becomes runnable. Explicit definition
sources follow the canonical precedence and identical-content rules; equal
precedence never uses registration order as a tie-breaker.

Cardinality is evaluated at the boundary that owns it. The definition, session-
store, hook-profile, security-policy, and model catalogs/selectors; validators;
run-scope factory; hook dispatcher; approval broker; and `TimeProvider` are
singular engine-wide services. Each runnable definition MUST resolve exactly one
selected loop, continuation policy, input coordinator, output publisher, context
assembler, session coordinator/run coordinator/profile/store, hook profile,
security authority/profile, model selector, and model executor. Multiple keyed
implementations MAY coexist in the engine; ambiguity for one definition is a
definition validation failure, not a reason to collapse the engine to one global
agent configuration.

Named or keyed services SHOULD be used for multiple provider, queue, store, or
policy implementations. Runtime code MUST receive explicit factories or
selectors rather than use the container as a service locator.

The package-internal run-scope factory and run-plan compiler are the sole owners
of scope creation and arbitrary keyed contract resolution. They capture one
definition/catalog version, resolve its keys inside one run scope, and pass an
immutable collaborator bundle to the selected loop. Narrow package-internal
activators MAY execute prevalidated hook, tool, or contributor factories in that
scope, but cannot query arbitrary contracts or keys. No public or feature
component receives `IServiceProvider`, an ambient current-agent value, or
permission to rediscover a newer profile during the run.

## Baseline implementation set

The first complete runnable composition SHOULD include:

- immutable message/event/result abstractions;
- the `AgentKit` facade and strict composition validation;
- a versioned agent-definition catalog with at least one runnable definition;
- `AgentKit.Loop`, `AgentKit.Context`, `AgentKit.Hooks`, and `AgentKit.IO`
  implementations;
- `AgentKit.Session` plus an explicitly selected store;
- `AgentKit.Permissions` with a fail-closed security authority, policy pipeline,
  approval broker, and bounded grants;
- `AgentKit.Providers`, one conversational model, and a replaceable
  `TimeProvider`;
- scripted model and tool fakes;
- optional tool validation and scheduling pipelines; and
- conformance suite bases consumable by future packages.

Tools, skills, memory, embeddings, reranking, goals, MCP, evaluation, and extra
context contributors remain optional. Registering an optional capability MUST
make its dependencies subject to build validation.

## Acceptance criteria

The [shared conformance strategy](testing-and-evaluation.md) supplies the
behavioral and dependency-graph evidence for these criteria.

- A dependency graph test proves no inward package references a leaf package.
- Default services can be replaced through public registration APIs.
- Two implementations of each stabilized extension pass the same behavioral
  suite.
- Disposal occurs exactly once at the documented owner boundary.
- Concurrent runs share immutable definitions but no mutable run state.
- One engine can run two differently configured agents concurrently without
  catalog, option, session, or scope leakage.
- The facade package dependency graph contains no concrete component package.
- A compatible provider passes shared wire-family conformance plus its own
  capability and registration suite.
- Enabling a storage-backed capability without an explicit compatible adapter
  fails before application work and never creates a hidden in-memory store.
- In-memory and SQLite adapters pass the same common contract suite; durability,
  transactions, fencing, and optional operations are tested against only the
  capabilities each adapter advertises.
- Disposing a standalone or hosted composition disposes each selected store at
  its documented owner exactly once, without deleting retained state.

## Related specifications

- [Agent definition and run context](agent-definition-and-run-context.md)
- [Public API and dependency injection](public-api-and-dependency-injection.md)
- [Testing and evaluation](testing-and-evaluation.md)
