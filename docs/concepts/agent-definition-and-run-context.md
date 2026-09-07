# Agent definition and run context

**Status:** Normative  
**Depends on:** [Architecture](architecture-and-dependency-boundaries.md),
[configuration](configuration-and-overrides.md)

## Purpose

An agent definition describes reusable behavior. A run context owns mutable
execution state. Conflating them makes concurrency unsafe and overrides leak
between callers.

## Engine, agent, and run

A built `AgentEngine` is the complete process-level composition. It owns the
versioned agent-definition catalog and shared service graph and can run many
agents concurrently. It is not itself an agent.

An `Agent` is an immutable engine-bound handle over one validated
`AgentDefinition`. It may start concurrent runs and sessions, but it owns no
mutable run or session state. Looking up an agent by its typed `AgentId`
captures the catalog version used to bind the handle; a later definition reload
affects newly resolved handles or a documented next-run boundary, never an
in-flight run.

## Agent definition

The canonical public `AgentDefinition` shape is defined once in
[composition and configuration](../architecture/composition-and-configuration.md#agent-definitions-and-catalog).
This concept owns its behavior: the immutable definition MUST be safe for
concurrent use and SHOULD contain only declarative defaults, typed selections,
and strategy identities.

The final API MAY expose a builder, configuration binding, or declarative
specification. Build-time convenience MUST produce an immutable validated
definition. The definition MUST NOT hold current messages, a cancellation
source, a streaming response, pending tool calls, or queued inputs.

Every configurable choice belongs either in the engine's replaceable service
graph and typed options or in the definition's declarative selections and
defaults. The definition MUST use typed keys or identity values when selecting
named loops, models, stores, toolsets, policies, and capability profiles; raw
service names and an `IServiceProvider` are not a runtime selection API. The
compiled definition records the exact resolved keys and versions used for a run.

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
- the immutable, explicitly typed collaborator bundle compiled for the run;
- cancellation reason and deadline; and
- run metadata, trace context, and generated event sequence.

Mutable collections MUST be run-owned and hidden behind methods that preserve
invariants. Exposing `List<Message>` or a writable state property is forbidden.

## Dependencies

Typed application dependencies MAY be injected into a run. They MUST have a
documented lifetime and MUST NOT grant implicit authority. A generic dependency
provider or service-locator-shaped lookup API is not allowed; the internal
run-plan compiler resolves selected keys once and supplies explicit interfaces
or a user-declared immutable dependency record. Permission policy still
evaluates the principal and operation even when a dependency can perform the
requested side effect.

Dynamic instructions, model selectors, tool preparation, and middleware MAY read
their declared run dependencies. They receive a restricted immutable view, not
the orchestration object's mutation methods or the DI container.

## Effective configuration snapshot

Each model request MUST resolve an
[effective configuration](configuration-and-overrides.md) from the immutable
agent definition plus run, capability, and per-turn layers. The resolved value
MUST be captured with the request or derivable from durable events.

Mid-run configuration changes MUST take effect only at named boundaries,
normally before context preparation for the next model request. They MUST NOT
mutate an in-flight request or retroactively reinterpret completed work.

## Continuation and resume

A continuation reuses the
[same durable session](sessions-persistence-and-branching.md) but starts a new
run unless the [durable executor](durable-execution-and-recovery.md) is resuming
the exact interrupted run. A resumed run MUST restore:

- stable IDs and sequence cursors;
- committed messages and tool terminal states;
- budget consumption;
- the last valid configuration snapshot or an explicit migration; and
- only those pending operations the recovery policy can prove safe.

In-memory objects, tasks, cancellation sources, and service scopes MUST NOT be
serialized as run state.

## Result boundary

`AgentRunResult<TOutput>` MUST include the terminal reason, output if present,
new messages or an append cursor, usage, run/session/conversation IDs, and
metadata. It MUST distinguish a successful output from a completed run with no
output, a limit outcome, cancellation, policy denial, or failure.

## Acceptance criteria

- Two runs of one definition can execute concurrently without state leakage.
- Two differently configured agents in one engine can execute concurrently
  without definition, option, catalog, session, or run-scope leakage.
- A run override does not alter later runs.
- Dynamic configuration observes the correct run dependency and prior merge
  layers.
- Resume restores committed state without duplicating admitted input or tool
  side effects.
- Result messages are the exact committed suffix, not a reconstructed guess.

## Related specifications

- [Run lifecycle and settlement](run-lifecycle-and-settlement.md)
- [Configuration and overrides](configuration-and-overrides.md)
- [Public API and dependency injection](public-api-and-dependency-injection.md)
