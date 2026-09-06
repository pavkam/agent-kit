# Architecture and dependency boundaries

**Status:** Normative  
**Depends on:** [Design principles](design-principles.md)

## Package topology

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

First-party implementations MUST live in focused packages such as
`AgentKit.Loop`, `AgentKit.Context`, `AgentKit.IO`, `AgentKit.Session`,
`AgentKit.Permissions`, `AgentKit.Providers`, and `AgentKit.Tools`. Feature
families use `AgentKit.Tools.<ToolName>`, `AgentKit.Providers.<ProviderName>`,
and `AgentKit.Session.<StorageProvider>`. Concrete provider, storage,
filesystem, transport, MCP, and durable-backend packages are leaves. Foundation
and runtime packages MUST NOT reference a concrete leaf.

`AgentKit.Providers` MUST contain only provider-neutral catalog, selection,
capability-validation, and attempt-coordination behavior.
`AgentKit.Providers.OpenAICompatible` MAY contain reusable protocol-family base
classes and services. It MUST NOT replace concrete identity, options, profiles,
credentials, or registration in packages such as `AgentKit.Providers.OpenAI`,
`AgentKit.Providers.OpenRouter`, and `AgentKit.Providers.ZAi`.

## Extension-axis rule

Contracts MUST separate these axes when implementations can vary independently:

| Axis        | Responsibility                  | Example contract        |
| ----------- | ------------------------------- | ----------------------- |
| Description | Immutable capability metadata   | `ModelDescriptor`       |
| Discovery   | Enumerate candidates            | `IToolProvider`         |
| Selection   | Choose a candidate              | `IModelSelector`        |
| Resolution  | Bind identity to implementation | `IToolResolver`         |
| Execution   | Perform one operation           | `IToolInvoker`          |
| Policy      | Permit, deny, or defer          | `IToolPermissionPolicy` |
| State       | Load and append domain records  | `ISessionStore`         |
| Observation | Receive immutable events        | `IRunEventSink`         |

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

Optional behavior MUST be reported through descriptors, capability interfaces,
or discriminated results before use. Discovering lack of support through a
mid-run `NotSupportedException` is non-conforming.

Capability claims describe the selected model and configured deployment, not the
theoretical union of everything a provider has ever offered.

## Composition

Registration extensions MUST:

- return `IServiceCollection`;
- never build or resolve a service provider;
- state whether registration is singular, additive, keyed, replaceable, and
  idempotent;
- validate impossible option combinations at startup; and
- register defaults so applications can replace them without removing hidden
  internal services.

`AgentEngine.CreateBuilder()` MUST return a separate mutable builder whose
`Services` property is the ordinary `IServiceCollection` composition surface.
`Build()` MUST return an immutable engine and validate the complete graph.
`AddAgentKit` MUST register the same facade and validation into an externally
owned service collection.

Standalone and host-managed composition MUST share one registration path. The
standalone engine owns the provider created by its builder. A hosted engine MUST
NOT dispose the host's provider.

Named or keyed services SHOULD be used for multiple provider, queue, store, or
policy implementations. Runtime code MUST receive explicit factories or
selectors rather than use the container as a service locator.

## Baseline implementation set

The first complete runnable composition SHOULD include:

- immutable message/event/result abstractions;
- the `AgentKit` facade and strict composition validation;
- `AgentKit.Loop`, `AgentKit.Context`, and `AgentKit.IO` implementations;
- `AgentKit.Session` plus an explicitly selected store;
- `AgentKit.Permissions` with a fail-closed policy;
- `AgentKit.Providers`, one conversational model, and a replaceable
  `TimeProvider`;
- scripted model and tool fakes;
- optional tool validation and scheduling pipelines; and
- conformance suite bases consumable by future packages.

Tools, skills, memory, embeddings, reranking, goals, MCP, evaluation, and extra
context contributors remain optional. Registering an optional capability MUST
make its dependencies subject to build validation.

## Acceptance criteria

- A dependency graph test proves no inward package references a leaf package.
- Default services can be replaced through public registration APIs.
- Two implementations of each stabilized extension pass the same behavioral
  suite.
- Disposal occurs exactly once at the documented owner boundary.
- Concurrent runs share immutable definitions but no mutable run state.
- The facade package dependency graph contains no concrete component package.
- A compatible provider passes shared wire-family conformance plus its own
  capability and registration suite.

## Related specifications

- [Agent definition and run context](agent-definition-and-run-context.md)
- [Public API and dependency injection](public-api-and-dependency-injection.md)
- [Testing and evaluation](testing-and-evaluation.md)
