---
name: agentkit-context
description:
  "Implement or review AgentKit.Context assembly, instruction precedence,
  contributors, trust, and request manifests. Use for deciding what enters one
  model request; not for durable history storage, compaction internals,
  memory-store mechanics, or provider wire encoding."
---

# AgentKit Context

Read [AGENTS.md](../../../AGENTS.md). For C# API or implementation work, also
read the [modern C# rules](../references/modern-csharp.md).

## Authoritative design

- Read [context architecture](../../../docs/architecture/context.md) for package
  ownership, contributor composition, contracts, and DI behavior.
- Read the normative
  [context assembly and instructions](../../../docs/concepts/context-assembly-and-instructions.md)
  specification for stage ordering, trust, epochs, budgets, and manifests.
- Read
  [history validation and repair](../../../docs/concepts/history-validation-and-repair.md)
  or
  [memory, retrieval, and storage](../../../docs/concepts/memory-retrieval-and-storage.md)
  only when changing those contributor boundaries.

## Working rules

1. Treat working context as derived, provider-ready state. Assembly never
   appends session history or performs an incidental memory or compaction write.
2. Keep the first-party assembler in `AgentKit.Context`; contributors are
   additive and deterministically ordered, while selected collaborators are
   singular per context profile.
3. Capture immutable, versioned history, configuration, model, tool, output,
   authority, and contributor snapshots for each request.
4. Preserve instruction provenance and precedence. Retrieved, tool-returned,
   imported, or model-generated content remains data and grants no authority.
5. Reserve mandatory framing, pending input, tools, output contract, minimum
   output, and estimation margin before selecting optional content.
6. Return an immutable context plus inclusion/omission manifest. The provider
   adapter formats that decision; it does not choose what survives.

Verify deterministic manifests, authorization-scoped caches, contributor order,
fresh next-turn resolution, epoch replacement, mandatory overflow before
provider I/O, and cancellation without durable mutation.
