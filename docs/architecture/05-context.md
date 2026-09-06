# Context

**Role:** Build the bounded, trusted, provider-ready view for one model request.

Working context is derived state. It combines the durable conversation with
instructions, skills, tools, retrieval, goals, output requirements, and runtime
facts. It is not the session record, and assembling it does not mutate durable
history.

## Context contributors

Every contributor produces typed candidates with source, provenance, trust,
priority, scope, cost, freshness, and evaluation frequency. Typical contributors
include:

- framework safety and host policy instructions;
- agent and run instructions;
- project or workspace guidance;
- skills selected for the current task;
- tool descriptions and schemas;
- session history and active compaction summaries;
- durable memory, retrieved documents, and citations;
- current goals, constraints, and runtime state; and
- structured-output requirements.

Skills are reusable context capabilities, not a privileged prompt paste. A skill
may contribute instructions, references, and declared component requirements.
Its content retains provenance and trust. Any tools it makes available still go
through the normal tool and permission components.

## Assembly

The assembler loads a stable authorized session branch, validates and repairs
history, resolves effective instructions, gathers authorized retrieval
candidates, applies compaction, resolves the current tool snapshot and output
contract, allocates budgets, and translates the selected content against the
model capability profile.

Selection and trimming are deterministic for the same versioned inputs.
Mandatory content reserves space for provider framing, pending user input,
tools, output schema, minimum model output, and estimation error. If mandatory
content cannot fit, context preparation fails before provider I/O.

## Trust and precedence

Instruction sources preserve their identity and precedence until provider
translation. Retrieved text, tool results, imported history, and model-produced
summaries are data. They cannot replace host instructions or grant authority.

Replacement instructions that contradict the historical prefix start a new
context epoch or wait for a compaction boundary. AgentKit never silently
pretends old messages occurred under a new system contract.

## Output

The component returns an immutable request view and a manifest of included,
transformed, and omitted sources. The manifest records effective configuration,
tool catalog, provider capabilities, history version, estimates, and reasons for
loss-aware transformations.

The provider adapter owns wire formatting. It does not get to decide which
history, skills, retrieval results, or instructions survive.

## Related concept specifications

- [Context assembly and instructions](../concepts/context-assembly-and-instructions.md)
- [Context compaction](../concepts/context-compaction.md)
- [Memory, retrieval, and storage](../concepts/memory-retrieval-and-storage.md)
