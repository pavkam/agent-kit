# Context assembly and instructions

**Status:** Normative

**Architecture:** [Context](../architecture/context.md)

**Depends on:** [History validation](history-validation-and-repair.md),
[model capabilities](model-providers-and-capabilities.md),
[memory and retrieval](memory-retrieval-and-storage.md)

## Purpose

Working context is the bounded, provider-neutral and capability-checked input
for one model request. It is derived from durable history, instructions, tools,
retrieval, and runtime state, but is not itself the durable conversation record
or a provider wire DTO.

## Assembly pipeline

The context assembler MUST run ordered stages:

1. Load the [authorized session branch](sessions-persistence-and-branching.md)
   at a stable version.
2. [Validate, migrate, and repair](history-validation-and-repair.md) a request
   view.
3. Resolve effective static and dynamic instructions.
4. Obtain
   [authorized memory/retrieval candidates](memory-retrieval-and-storage.md)
   with provenance.
5. Apply history processors and active
   [compaction summaries](context-compaction.md).
6. Resolve the effective [tool descriptions](tools-and-toolsets.md) and
   [output schema](structured-output.md).
7. Allocate [token/byte budgets](usage-limits-and-budgets.md) by content class.
8. Select, trim, or summarize candidates deterministically.
9. Validate roles, parts, tools, and output requirements against the selected
   provider capability profile and record any required adapter transformation or
   rejection without producing wire messages.
10. Return an immutable `ModelRequestContext` plus a manifest of included and
    omitted source IDs.

No stage may write durable history as an incidental side effect. Deliberate
memory writes or compaction are separate operations.

Every assembly request carries the complete authenticated `ExecutionIdentity`
and immutable security/configuration snapshots. Tenant or principal projections
are not substitutes for that identity. Cache keys include its fingerprint and
the relevant authority and policy versions, so cached context cannot cross an
identity boundary.

Compaction is optional and is selected when the run plan is compiled. An enabled
plan passes an invocation-only capability containing the exact keyed compactor
and already selected session coordinator; neither component rediscovers services
from a container. Without that capability, mandatory overflow returns
`ContextLimitExceeded`.

When assembly triggers configured compaction, that is an explicit nested
operation with its own identity, authorization, budget, and terminal result.
After activation, the assembler reloads the committed cursor and rebuilds its
manifest. Cancelling assembly discards the candidate request but MUST NOT erase
an activated compaction or its usage. Without activation, source history stays
unchanged. The compactor never calls back into assembly.

## Instruction sources

Instructions MUST retain source, trust, priority, and scope. Typical classes are
framework safety, host policy, agent definition, capability/middleware,
application resource guidance, run override, and generated next-turn
instruction.

Concatenation and replacement MUST be explicit per source. Higher precedence
does not let untrusted retrieved or tool-return text become instructions.
Retrieved content and tool output MUST be delimited as data with provenance.

Dynamic instructions MAY evaluate once per run or once per request. Their
frequency MUST be declared. They receive the current immutable run view and a
cancellation token; failures are typed context-preparation failures.

## Context epoch

The runtime SHOULD model a context epoch: a baseline set of instructions,
application resource state, and history/event cursor used by subsequent turns.

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
- ordered provider-neutral messages and instructions with stable source-part
  correlation;
- available tool definitions and output schema;
- effective settings;
- estimated token/byte use and reservations;
- source manifest and transformation diagnostics; and
- session history/configuration versions used.

The provider adapter owns every wire-specific role, part, schema, and identifier
translation. It MUST NOT make policy decisions about which history or retrieval
content survives.

## Acceptance scenarios

- The same versioned inputs and deterministic processors yield identical context
  manifests.
- Mandatory content overflow fails before provider I/O.
- Retrieved prompt injection remains data and cannot replace host instructions.
- Switching model profiles rebuilds capability validation, tool schemas, and the
  adapter's translated wire request.
- A replacement instruction set starts a new epoch or waits for compaction.
- An in-flight request is unaffected by a later configuration reload.

## Related specifications

- [Context compaction](context-compaction.md)
- [Provider request pipeline](provider-request-pipeline.md)
- [Configuration and overrides](configuration-and-overrides.md)
