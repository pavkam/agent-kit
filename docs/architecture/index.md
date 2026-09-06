# AgentKit architecture

**Status:** Project architecture

AgentKit is a set of independently selectable .NET 10 libraries for composing
agentic applications. The AgentKit package is the facade, not the runtime. It
builds an engine from contracts and feature registrations supplied by the host.

The architecture follows four rules:

1. Every meaningful behavior has one owner.
2. Components depend on provider-neutral contracts, not sibling implementations.
3. Policy is evaluated before I/O or side effects.
4. Durable facts and live activity are represented separately.

The specifications in [concepts](../concepts/index.md) define normative
behavior. These documents describe how that behavior is divided into components
and packages. The [project structure](project-structure.md) records the concrete
solution layout.

## Package direction

| Layer                            | Responsibility                                                                          |
| -------------------------------- | --------------------------------------------------------------------------------------- |
| Applications and hosts           | Select feature packages, configuration, policy, and lifecycle                           |
| Feature and integration packages | Implement loops, context, sessions, tools, providers, storage, and protocols            |
| AgentKit                         | Expose the AgentEngine facade, builder, hosted registration, and composition validation |
| AgentKit.Abstractions            | Define provider-neutral contracts, values, events, and results                          |

Dependencies point down this table. AgentKit references AgentKit.Abstractions
and the required Microsoft.Extensions abstractions. It does not pull in a loop,
session implementation, provider, permission engine, storage implementation, or
tool transitively.

Concrete packages normally reference AgentKit.Abstractions. A leaf integration
may reference the implementation package whose stable extension surface it
adapts, but no foundation or implementation package references a concrete leaf.

## Composition

AgentEngine is the immutable runtime facade. AgentEngineBuilder is the mutable
composition surface returned by AgentEngine.CreateBuilder. The builder exposes
its service collection, so every feature uses ordinary ASP.NET-style dependency
injection registration.

AgentKit supports two ownership modes through the same registrations:

- In standalone mode, the builder creates the service provider and the built
  engine owns its disposal.
- In hosted mode, AddAgentKit registers AgentEngine into an existing service
  collection and the host owns the provider and its lifecycle.

Build validation requires one loop, one input coordinator, one output publisher,
one session coordinator, one session store, one context assembler, one
permission policy, one model catalog, one model selector, one model request
executor, at least one conversational model, and a TimeProvider.
TimeProvider.System is the normal default. Tools, skills, memory, embeddings,
reranking, goals, MCP, and additional context contributors are optional. A
registered optional capability must still be complete; a tool registration, for
example, cannot build without its execution and permission pipeline.

## Components

| Component                                                            | Owns                                                                                             |
| -------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| [Project structure](project-structure.md)                            | Package boundaries, dependency direction, common files, and mirrored tests                       |
| [Composition and configuration](01-composition-and-configuration.md) | AgentEngine, its builder, dependency injection, validation, configuration, and lifetimes         |
| [Agent runtime](02-agent-runtime.md)                                 | The replaceable loop, turn coordination, limits, cancellation, and settlement                    |
| [Messages and history](03-messages-and-history.md)                   | The immutable conversation model, durable history rules, validation, and repair                  |
| [Input and output](04-input-and-output.md)                           | Input admission, queues, live streams, final results, and structured output                      |
| [Context](05-context.md)                                             | Ordered contributors, instruction trust, selection, budgeting, and manifests                     |
| [Model and embedding providers](06-model-and-embedding-providers.md) | Capability discovery, selection, wire adaptation, streaming, and embeddings                      |
| [Tools](07-tools.md)                                                 | Tool runtime and feature packages for discovery, validation, scheduling, invocation, and results |
| [Permissions and human control](08-permissions-and-human-control.md) | Authorization policy, approvals, deferral, and human-in-the-loop decisions                       |
| [Sessions](09-sessions.md)                                           | Session coordination, append-only state, branching, and replaceable storage                      |
| [Durable execution](10-durable-execution.md)                         | Checkpoints, recovery, leases, fencing, and durable backend adaptation                           |
| [Memory and retrieval](11-memory-and-retrieval.md)                   | Durable memory, documents, vectors, retrieval, provenance, and deletion                          |
| [Goals and delegation](12-goals-and-delegation.md)                   | Goal state, attempts, delegation, agent communication, and joins                                 |
| [Extensions](13-extensions.md)                                       | Typed capabilities, middleware, hooks, strategies, and their ordering                            |
| [Observability](14-observability.md)                                 | Events, traces, metrics, logs, audit, correlation, and redaction                                 |
| [MCP](15-mcp.md)                                                     | Protocol lifecycle, primitive adaptation, transports, and remote capability policy               |
| [File system](16-file-system.md)                                     | Replaceable file and directory operations used by the framework and tools                        |
| [Testing and evaluation](17-testing-and-evaluation.md)               | Mirrored tests, shared conformance, deterministic fakes, datasets, and evaluation reports        |

## Runtime flow

A normal run moves through the components in a deliberate order:

1. The facade validates registrations and builds an immutable AgentEngine.
2. The engine creates an isolated run scope from the selected components.
3. The I/O component authorizes, validates, admits, and promotes input at a safe
   boundary.
4. The session provides a stable history version for the active branch.
5. Context contributors gather instructions, skills, tools, memory, goals, and
   runtime facts into a bounded request view.
6. The provider runtime selects a compatible model, then a concrete provider
   package performs one model request.
7. The output stream exposes typed provisional events while the loop builds a
   candidate response.
8. A validated assistant response is committed to the session.
9. Requested tools are resolved against the request's catalog snapshot,
   authorized, recorded, and executed.
10. Tool results are committed in deterministic source order and the loop
    decides whether another turn is required.
11. The run produces one typed terminal outcome and settles all required work.

Goals, deferrals, recovery, and queued follow-up input can start later runs, but
they do not weaken these boundaries.

## Cross-cutting invariants

- Every run, turn, request, message, input, tool call, goal, and durable
  operation has a stable identity and causal relationship.
- Provider output is a proposal until validated and committed.
- Model text, retrieved content, tool metadata, and remote protocol metadata do
  not grant authority.
- Tool calls are durably recorded before side effects begin.
- Every accepted tool call and run reaches exactly one terminal result.
- Configuration, catalogs, and context are immutable snapshots while an
  operation is in flight.
- Time-dependent framework behavior uses the injected TimeProvider.
- Replacement implementations preserve observable behavior through shared
  conformance suites.
- OpenAI compatibility is a reusable tested wire profile, never a substitute for
  concrete provider identity.
