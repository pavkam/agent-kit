# Research provenance

**Status:** Evidence ledger  
**Research date:** 2026-09-06  
**Target:** AgentKit on .NET 10 / C# 14

## Scope

The
[architecture concept-coverage map](../architecture/index.md#concept-coverage)
records where each synthesized behavior is owned. Evidence itself intentionally
has no runtime package.

These specifications synthesize observable designs from Pi, OpenCode, and
Pydantic AI. Research focused on agent loops, message models, event streaming,
input queues, overrides, history/context, tools, permissions, structured output,
sessions, durability, limits, middleware, testing, and provider abstraction.

This is not a feature checklist or source port. Upstream behavior was selected,
combined, or rejected against AgentKit's package, DI, safety, and determinism
requirements.

## Pinned upstream revisions

| Project     | Repository                                                      | Commit researched                                                                                                                   |
| ----------- | --------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| Pi          | [badlogic/pi-mono](https://github.com/badlogic/pi-mono)         | [`9767ba275f3e9a5ee0f5c5342249b629ab1b2282`](https://github.com/badlogic/pi-mono/tree/9767ba275f3e9a5ee0f5c5342249b629ab1b2282)     |
| OpenCode    | [anomalyco/opencode](https://github.com/anomalyco/opencode)     | [`337fd144d2ba144743368f78d9579a99cce175bd`](https://github.com/anomalyco/opencode/tree/337fd144d2ba144743368f78d9579a99cce175bd)   |
| Pydantic AI | [pydantic/pydantic-ai](https://github.com/pydantic/pydantic-ai) | [`c0e4d824eaa0401d4481d401e5b3894ab32ab59d`](https://github.com/pydantic/pydantic-ai/tree/c0e4d824eaa0401d4481d401e5b3894ab32ab59d) |

The user-supplied name “Pydatinc-AI” was interpreted as Pydantic AI. Pinned
links are used throughout so later upstream changes do not rewrite the evidence
under these decisions.

## Primary evidence

### Pi

- Minimal loop and queue semantics:
  [`packages/agent/src`](https://github.com/badlogic/pi-mono/tree/9767ba275f3e9a5ee0f5c5342249b629ab1b2282/packages/agent/src)
- Provider-neutral types and request options:
  [`packages/ai/src/types.ts`](https://github.com/badlogic/pi-mono/blob/9767ba275f3e9a5ee0f5c5342249b629ab1b2282/packages/ai/src/types.ts)
- Higher-level sessions, settings, extensions, persistence, and compaction:
  [`packages/coding-agent/src/core`](https://github.com/badlogic/pi-mono/tree/9767ba275f3e9a5ee0f5c5342249b629ab1b2282/packages/coding-agent/src/core)

### OpenCode

- V2 schema for messages, admitted input, delivery, and durable/live events:
  [`packages/schema/src`](https://github.com/anomalyco/opencode/tree/337fd144d2ba144743368f78d9579a99cce175bd/packages/schema/src)
- V2 admission, execution coordination, context epoch, and runner:
  [`packages/core/src/session`](https://github.com/anomalyco/opencode/tree/337fd144d2ba144743368f78d9579a99cce175bd/packages/core/src/session)
- Legacy production loop and stream processor, used where V2 remains explicit
  work in progress:
  [`packages/opencode/src/session`](https://github.com/anomalyco/opencode/tree/337fd144d2ba144743368f78d9579a99cce175bd/packages/opencode/src/session)
- V2 design documents:
  [`specs/v2`](https://github.com/anomalyco/opencode/tree/337fd144d2ba144743368f78d9579a99cce175bd/specs/v2)

### Pydantic AI

- Graph/run state:
  [`_agent_graph.py`](https://github.com/pydantic/pydantic-ai/blob/c0e4d824eaa0401d4481d401e5b3894ab32ab59d/pydantic_ai_slim/pydantic_ai/_agent_graph.py)
- Message/event types and history repair:
  [`messages.py`](https://github.com/pydantic/pydantic-ai/blob/c0e4d824eaa0401d4481d401e5b3894ab32ab59d/pydantic_ai_slim/pydantic_ai/messages.py)
- Pending message queue:
  [`_enqueue.py`](https://github.com/pydantic/pydantic-ai/blob/c0e4d824eaa0401d4481d401e5b3894ab32ab59d/pydantic_ai_slim/pydantic_ai/_enqueue.py)
- Capabilities and middleware:
  [`capabilities`](https://github.com/pydantic/pydantic-ai/tree/c0e4d824eaa0401d4481d401e5b3894ab32ab59d/pydantic_ai_slim/pydantic_ai/capabilities)
- Durable execution adapters:
  [`durable_exec`](https://github.com/pydantic/pydantic-ai/tree/c0e4d824eaa0401d4481d401e5b3894ab32ab59d/pydantic_ai_slim/pydantic_ai/durable_exec)
- Declarative agent specification:
  [`docs/agent-spec.md`](https://github.com/pydantic/pydantic-ai/blob/c0e4d824eaa0401d4481d401e5b3894ab32ab59d/docs/agent-spec.md)
- Official concept documentation: [agent](https://ai.pydantic.dev/agent/),
  [message history](https://ai.pydantic.dev/message-history/),
  [tools](https://ai.pydantic.dev/tools/),
  [output](https://ai.pydantic.dev/output/), and
  [usage](https://ai.pydantic.dev/usage/).

## Comparative findings

| Concern           | Pi                                             | OpenCode                                         | Pydantic AI                                         | AgentKit decision                                     |
| ----------------- | ---------------------------------------------- | ------------------------------------------------ | --------------------------------------------------- | ----------------------------------------------------- |
| Loop              | Compact nested turn/follow-up loop             | Event-oriented V2 runner plus mature legacy loop | Inspectable graph nodes                             | Replaceable explicit state machine                    |
| Queues            | Steer/follow-up, all or one-at-a-time          | Durable admission and cutoff-based promotion     | ASAP/when-idle enqueue IDs                          | Durable idempotent admission + cutoff promotion       |
| Messages          | Lean user/assistant/tool result types          | Tagged messages and detailed tool states         | Broad request/response part taxonomy                | Immutable envelopes + typed parts + message state     |
| Events            | Rich live agent/model/tool events              | Explicit durable versus live-only boundary       | Typed part/tool/deferred/custom events              | Separate replayable semantics from live deltas        |
| Tool concurrency  | Parallel unless batch contains sequential tool | Concurrent execution, serialized publication     | Barrier segments for sequential tools               | Barrier segments + source-order commit                |
| Overrides         | Next-turn transforms, layered settings         | Layered config and V2 schema                     | Agent/run/capability settings and declarative merge | Field-specific merge algebra + immutable snapshots    |
| History           | Transform before each request                  | Provider conversion and interruption repair      | Sanitizers/processors and trust warning             | Durable truth + deterministic provider view repair    |
| Sessions          | Versioned JSONL tree and compaction            | Session event projection and local coordinator   | Run result suffix/history IDs                       | Append-only branching record + optimistic concurrency |
| Structured output | Provider/message primitives                    | Synthetic required tool in legacy loop           | Multiple output modes and end strategies            | Capability-negotiated modes + local validation        |
| Middleware        | Broad extension hooks                          | Plugins and provider hooks                       | Ordered capabilities with per-run isolation         | Narrow typed middleware with topology                 |
| Durability        | Session persistence                            | V2 migration toward event-oriented state         | Multiple durable backend adapters                   | Backend-neutral operation/checkpoint boundary         |

## Important synthesis decisions

### Admission is not delivery

OpenCode V2 provides the strongest model: acknowledge durable, idempotent
admission first; promote at a captured event-sequence cutoff later. Pi and
Pydantic AI supply the two useful priorities. AgentKit combines both in the
[input admission and queue contract](input-admission-and-message-queues.md).

### Tool scheduling uses barriers

Pi's simple rule serializes an entire batch when any tool is sequential.
Pydantic AI's barrier segments retain safe parallelism before and after a
sequential tool. AgentKit adopts barrier segments while using OpenCode's
[record-before-effect](tool-call-lifecycle.md) and
[serialized publication](tool-scheduling-and-concurrency.md) discipline.

### Settlement is a public boundary

Pi's higher-level session distinguishes post-agent retry/compaction processing
from the core agent end. The
[AgentKit settlement boundary](run-lifecycle-and-settlement.md) names this
`RunSettled` and requires no later run-owned semantic mutation.

### Durable history and working context differ

All three transform history for provider use. AgentKit makes the distinction
normative: repairs, filtering, retrieval, and compaction alter the
[request view](context-assembly-and-instructions.md), not the
[canonical session record](sessions-persistence-and-branching.md).

### Security is not generic middleware

Upstreams expose useful hooks and approval flows. AgentKit promotes permission,
approval, trust, audit, and sandbox derivation to narrow first-class contracts
so [extension order](extensions-hooks-and-middleware.md) cannot accidentally
decide [authority](permissions-approvals-and-trust.md).

### Some domains are AgentKit synthesis

The complete goal/delegation, memory/storage, distributed lease/fencing, and DI
models are not claimed as existing wholesale in any researched project. They
apply the verified causality, state, queue, and durability lessons to AgentKit's
[goal](goals-and-multi-agent-delegation.md),
[memory](memory-retrieval-and-storage.md),
[recovery](durable-execution-and-recovery.md), and
[composition](public-api-and-dependency-injection.md) extension axes.

## Evidence rules for implementers

- Treat these pinned revisions as design evidence, not current provider or
  protocol documentation.
- Before implementing a volatile wire protocol, verify its current official
  specification and record the version in the adapter.
- Distinguish OpenCode V2 implemented behavior from comments or design TODOs.
  Unfinished V2 work was not treated as proven runtime behavior.
- Prefer observable source behavior and official docs over examples or blog
  summaries.
- When live evidence conflicts with these specs, do not silently code around it.
  Record the incompatibility and amend the relevant spec deliberately.

## Related specifications

- [Concept index](index.md)
- [Design principles](design-principles.md)
- [Testing and evaluation](testing-and-evaluation.md)
