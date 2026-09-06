# Agent definition and run context

**Status:** Normative  
**Depends on:** [Architecture](architecture-and-dependency-boundaries.md),
[configuration](configuration-and-overrides.md)

## Purpose

An agent definition describes reusable behavior. A run context owns mutable
execution state. Conflating them makes concurrency unsafe and overrides leak
between callers.

## Agent definition

An immutable agent definition MUST be safe for concurrent use and SHOULD contain
only declarative defaults and strategy identities:

```csharp
public sealed record AgentDefinition(
    AgentId Id,
    string? Name,
    ModelSelection Model,
    ImmutableArray<InstructionSource> Instructions,
    ImmutableArray<ToolsetReference> Toolsets,
    RunPolicyDefaults Policies,
    ImmutableArray<CapabilityReference> Capabilities,
    IReadOnlyDictionary<string, JsonElement> Metadata);
```

The final API MAY expose a builder, configuration binding, or declarative
specification. Build-time convenience MUST produce an immutable validated
definition. The definition MUST NOT hold current messages, a cancellation
source, a streaming response, pending tool calls, or queued inputs.

## Run input and context

Starting a run MUST create a unique `RunId` and an isolated run-owned context.
The context tracks at least:

- session and optional conversation identity;
- admitted input IDs and promotion cutoffs;
- current loop state and monotonically increasing turn/step number;
- the effective immutable configuration snapshot;
- durable history cursor and request-local working context;
- usage and limit counters;
- pending tool calls, tasks, and terminal results;
- queued message snapshot/cursors;
- run-scoped dependency object or typed dependency provider;
- cancellation reason and deadline; and
- run metadata, trace context, and generated event sequence.

Mutable collections MUST be run-owned and hidden behind methods that preserve
invariants. Exposing `List<Message>` or a writable state property is forbidden.

## Dependencies

Typed application dependencies MAY be injected into a run. They MUST have a
documented lifetime and MUST NOT grant implicit authority. Permission policy
still evaluates the principal and tool call even when a dependency can perform
the requested side effect.

Dynamic instructions, model selectors, tool preparation, and middleware MAY read
the run dependency object. They SHOULD receive a restricted immutable view, not
the orchestration object's mutation methods.

## Effective configuration snapshot

Each model request MUST resolve an effective configuration from the immutable
agent definition plus run, capability, and per-turn layers. The resolved value
MUST be captured with the request or derivable from durable events.

Mid-run configuration changes MUST take effect only at named boundaries,
normally before context preparation for the next model request. They MUST NOT
mutate an in-flight request or retroactively reinterpret completed work.

## Continuation and resume

A continuation reuses the same session but starts a new run unless a durable
executor is resuming the exact interrupted run. A resumed run MUST restore:

- stable IDs and sequence cursors;
- committed messages and tool terminal states;
- budget consumption;
- the last valid configuration snapshot or an explicit migration; and
- only those pending operations the recovery policy can prove safe.

In-memory objects, tasks, cancellation sources, and service scopes MUST NOT be
serialized as run state.

## Result boundary

`AgentRunResult<T>` MUST include the terminal reason, output if present, new
messages or an append cursor, usage, run/session/conversation IDs, and metadata.
It MUST distinguish a successful output from a completed run with no output, a
limit outcome, cancellation, policy denial, or failure.

## Acceptance criteria

- Two runs of one definition can execute concurrently without state leakage.
- A run override does not alter later runs.
- Dynamic configuration observes the correct run dependency and prior merge
  layers.
- Resume restores committed state without duplicating admitted input or tool
  side effects.
- Result messages are the exact committed suffix, not a reconstructed guess.

## Upstream evidence

- Pydantic AI separates graph state, dependencies, run IDs, conversation IDs,
  pending messages, and per-run caches in
  [`_agent_graph.py`](https://github.com/pydantic/pydantic-ai/blob/c0e4d824eaa0401d4481d401e5b3894ab32ab59d/pydantic_ai_slim/pydantic_ai/_agent_graph.py).
- Pi exposes mutable agent state but guards one active run in
  [`agent.ts`](https://github.com/badlogic/pi-mono/blob/9767ba275f3e9a5ee0f5c5342249b629ab1b2282/packages/agent/src/agent.ts);
  AgentKit deliberately isolates this state per run to permit safe concurrency.

## Related specifications

- [Run lifecycle and settlement](run-lifecycle-and-settlement.md)
- [Configuration and overrides](configuration-and-overrides.md)
- [Public API and dependency injection](public-api-and-dependency-injection.md)
