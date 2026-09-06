# Architecture and dependency boundaries

**Status:** Normative  
**Depends on:** [Design principles](design-principles.md)

## Package topology

```text
applications / hosts
        │
        ├──────────────> AgentKit.* integrations
        │                         │
        └──────────────> AgentKit │
                                  │
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

`AgentKit` MUST contain the default orchestration, in-memory implementations,
and DI composition. It MAY depend on `AgentKit.Abstractions` and platform
libraries, but MUST NOT depend on concrete model, persistence, transport, MCP,
or hosting packages.

`AgentKit.*` integration packages are leaves. A provider or storage adapter MAY
depend on abstractions and, only for intentionally reused mechanics, on
`AgentKit`. Core packages MUST NOT reference an integration.

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

Named or keyed services SHOULD be used for multiple provider, queue, store, or
policy implementations. Runtime code MUST receive explicit factories or
selectors rather than use the container as a service locator.

## Baseline implementation set

The first complete runtime SHOULD include:

- immutable message/event/result abstractions;
- a default single-run loop and in-memory input queue;
- scripted model and tool fakes;
- in-memory session and event stores;
- default tool validation, permission, and scheduling pipelines; and
- conformance suite bases consumable by future packages.

Network provider adapters, databases, distributed queues, MCP, durable
orchestrators, and hosts belong in later leaf packages.

## Acceptance criteria

- A dependency graph test proves no inward package references a leaf package.
- Default services can be replaced through public registration APIs.
- Two implementations of each stabilized extension pass the same behavioral
  suite.
- Disposal occurs exactly once at the documented owner boundary.
- Concurrent runs share immutable definitions but no mutable run state.

## Related specifications

- [Agent definition and run context](agent-definition-and-run-context.md)
- [Public API and dependency injection](public-api-and-dependency-injection.md)
- [Testing and evaluation](testing-and-evaluation.md)
