# Context assembly and instructions

**Status:** Normative  
**Depends on:** [History validation](history-validation-and-repair.md),
[model capabilities](model-providers-and-capabilities.md),
[memory and retrieval](memory-retrieval-and-storage.md)

## Purpose

Working context is the bounded, provider-ready input for one model request. It
is derived from durable history, instructions, tools, retrieval, and runtime
state, but is not itself the durable conversation record.

## Assembly pipeline

The context assembler MUST run ordered stages:

1. Load the authorized session branch at a stable version.
2. Validate, migrate, and repair a request view.
3. Resolve effective static and dynamic instructions.
4. Obtain authorized memory/retrieval candidates with provenance.
5. Apply history processors and active compaction summaries.
6. Resolve the effective tool descriptions and output schema.
7. Allocate token/byte budgets by content class.
8. Select, trim, or summarize candidates deterministically.
9. Translate roles and parts against the selected provider capability profile.
10. Return an immutable `ModelRequestContext` plus a manifest of included and
    omitted source IDs.

No stage may write durable history as an incidental side effect. Deliberate
memory writes or compaction are separate operations.

## Instruction sources

Instructions MUST retain source, trust, priority, and scope. Typical classes are
framework safety, host policy, agent definition, capability/middleware, project
or workspace guidance, run override, and generated next-turn instruction.

Concatenation and replacement MUST be explicit per source. Higher precedence
does not let untrusted retrieved or tool-return text become instructions.
Retrieved content and tool output MUST be delimited as data with provenance.

Dynamic instructions MAY evaluate once per run or once per request. Their
frequency MUST be declared. They receive the current immutable run view and a
cancellation token; failures are typed context-preparation failures.

## Context epoch

The runtime SHOULD model a context epoch: a baseline set of instructions,
workspace state, and history/event cursor used by subsequent turns.

An additive context change MAY append a visible context-update record. A
replacement that would contradict the historical prefix SHOULD wait for a
compaction boundary or begin a new epoch. The runtime MUST NOT silently present
old messages as though they occurred under new system instructions.

## Budget allocation

The assembler MUST reserve capacity for:

- provider framing and schema overhead;
- the pending user/steering input;
- a configured minimum output allowance;
- tool definitions and structured-output schema; and
- a safety margin for tokenizer estimation error.

It MUST reject a request when mandatory content alone exceeds the context
window. Optional content SHOULD be removed by stable policy, not “last item
until it fits” hidden inside an adapter.

## Freshness and per-turn resolution

Model, tools, settings, and dynamic instructions SHOULD be resolved at every
next-turn boundary so controlled changes can take effect. In-flight requests use
an immutable snapshot.

A compaction or retry MAY require context to be rebuilt. It MUST record the new
manifest and MUST NOT reuse a stale tool catalog or authorization-derived
content.

## Output

`ModelRequestContext` MUST include:

- selected provider/model and effective capabilities;
- ordered translated messages/instructions;
- available tool definitions and output schema;
- effective settings;
- estimated token/byte use and reservations;
- source manifest and transformation diagnostics; and
- session history/configuration versions used.

The provider adapter MAY perform wire-specific translation but MUST NOT make
policy decisions about which history or retrieval content survives.

## Acceptance scenarios

- The same versioned inputs and deterministic processors yield identical context
  manifests.
- Mandatory content overflow fails before provider I/O.
- Retrieved prompt injection remains data and cannot replace host instructions.
- Switching model profiles rebuilds translated history and tool schemas.
- A replacement instruction set starts a new epoch or waits for compaction.
- An in-flight request is unaffected by a later configuration reload.

## Upstream evidence

- OpenCode V2 tracks system-context baselines and reconciles additive versus
  replacement changes in
  [`context-epoch.ts`](https://github.com/anomalyco/opencode/blob/337fd144d2ba144743368f78d9579a99cce175bd/packages/core/src/session/context-epoch.ts).
- Pi supports per-request context transformation and next-turn preparation in
  [`agent-loop.ts`](https://github.com/badlogic/pi-mono/blob/9767ba275f3e9a5ee0f5c5342249b629ab1b2282/packages/agent/src/agent-loop.ts).
- Pydantic AI supports ordered history processing through its capability model,
  documented in
  [process history](https://ai.pydantic.dev/capabilities/process-history/).

## Related specifications

- [Context compaction](context-compaction.md)
- [Provider request pipeline](provider-request-pipeline.md)
- [Configuration and overrides](configuration-and-overrides.md)
