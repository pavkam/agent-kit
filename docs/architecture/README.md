# AgentKit architecture

**Status:** Project architecture

AgentKit is a composable runtime for building agentic applications on .NET 10.
It is not a single agent implementation with extension points bolted onto the
side. It is a set of independently replaceable components that a host composes
into an agent definition and activates for a run.

The architecture follows four rules:

1. Every meaningful behavior has one owner.
2. Components depend on contracts, not sibling implementations.
3. Policy is evaluated before I/O or side effects.
4. Durable facts and live activity are represented separately.

The specifications in [concepts](../concepts/index.md) define normative
behavior. These documents describe how that behavior is divided into project
components.

## Package direction

| Layer                  | Responsibility                                                  |
| ---------------------- | --------------------------------------------------------------- |
| Applications and hosts | Choose integrations, configuration, policy, and lifecycle       |
| Integration packages   | Adapt providers, MCP, storage, transports, and durable backends |
| AgentKit               | Supply the default runtime, composition, and orchestration      |
| AgentKit.Abstractions  | Define provider-neutral contracts, values, events, and results  |

Dependencies point down this table. AgentKit.Abstractions does not reference
provider SDKs, persistence clients, transports, or AgentKit default
implementations. Integration packages sit at the leaves and translate external
systems into the neutral contracts.

## Components

| Component                                                            | Owns                                                                                     |
| -------------------------------------------------------------------- | ---------------------------------------------------------------------------------------- |
| [Composition and configuration](01-composition-and-configuration.md) | Agent definitions, dependency injection, configuration layers, validation, and lifetimes |
| [Agent runtime](02-agent-runtime.md)                                 | The run state machine, turn coordination, limits, cancellation, and settlement           |
| [Messages and history](03-messages-and-history.md)                   | The immutable conversation model, durable history rules, validation, and repair          |
| [Input and output](04-input-and-output.md)                           | Input admission, queues, live streams, final results, and structured output              |
| [Context](05-context.md)                                             | Ordered context contributors, instruction trust, selection, budgeting, and manifests     |
| [Model and embedding providers](06-model-and-embedding-providers.md) | Capability discovery, selection, wire adaptation, streaming, and embedding generation    |
| [Tools](07-tools.md)                                                 | Tool discovery, resolution, validation, scheduling, invocation, and results              |
| [Permissions and human control](08-permissions-and-human-control.md) | Authorization policy, approvals, deferral, and human-in-the-loop decisions               |
| [Sessions](09-sessions.md)                                           | Durable coordination, append-only state, branching, and active-run ownership             |
| [Durable execution](10-durable-execution.md)                         | Checkpoints, recovery, leases, fencing, and durable backend adaptation                   |
| [Memory and retrieval](11-memory-and-retrieval.md)                   | Durable memory, documents, vectors, retrieval, provenance, and deletion                  |
| [Goals and delegation](12-goals-and-delegation.md)                   | Goal state, attempts, delegation, agent communication, and joins                         |
| [Extensions](13-extensions.md)                                       | Typed capabilities, middleware, hooks, strategies, and their ordering                    |
| [Observability](14-observability.md)                                 | Events, traces, metrics, logs, audit, correlation, and redaction                         |
| [MCP](15-mcp.md)                                                     | Protocol lifecycle, primitive adaptation, transports, and remote capability policy       |

## Runtime flow

A normal run moves through the components in a deliberate order:

1. The host resolves an immutable agent definition and creates an isolated run
   scope.
2. Input is authorized, validated, admitted, and promoted at a safe boundary.
3. The session provides a stable history version for the active branch.
4. The context component gathers instructions, skills, tools, memory, goals, and
   runtime facts into a bounded request view.
5. The provider component selects a compatible model and performs one model
   request.
6. The output stream exposes typed provisional events while the runtime builds a
   candidate response.
7. A validated assistant response is committed to the session.
8. Requested tools are resolved against the request's catalog snapshot,
   authorized, recorded, and executed.
9. Tool results are committed in deterministic source order and the loop decides
   whether another turn is required.
10. The run produces one typed terminal outcome and settles all required work.

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
- Replacement implementations preserve observable behavior through shared
  conformance suites.
