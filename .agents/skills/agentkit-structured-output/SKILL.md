---
name: agentkit-structured-output
description:
  "Implement or debug AgentKit.Output definitions, extraction, validation,
  repair decisions, and deserialization. Use for turning terminal model
  candidates into validated application values; not for final-result
  publication, application tool execution, or provider wire translation."
---

# AgentKit Structured Output

Read [AGENTS.md](../../../AGENTS.md). For C# API or implementation work, also
read the [modern C# rules](../references/modern-csharp.md).

## Authoritative design

- Read
  [structured output architecture](../../../docs/architecture/structured-output.md)
  for package ownership, contract shape, DI, and dependency direction.
- Read the normative
  [structured output specification](../../../docs/concepts/structured-output.md)
  for modes, validation order, retry semantics, and output-tool strategies.
- Read
  [usage limits and budgets](../../../docs/concepts/usage-limits-and-budgets.md)
  only when changing validation-retry accounting.

## Working rules

1. Keep contracts in `AgentKit.Abstractions` and the first-party processor in
   `AgentKit.Output`; `AgentKit.IO` alone publishes accepted final results.
2. Resolve one immutable output definition before provider I/O and negotiate
   modes explicitly. A provider-native schema response still needs local checks.
3. Keep streamed or partially parsed values provisional. Deserialize an
   application type only after bounds, protocol, and canonical schema
   validation. Typed values remain provisional until the selected semantic
   validators pass; successful deserialization is not output acceptance.
4. Treat a synthetic output tool as an internal protocol channel, never an
   application tool or permission-bearing external effect.
5. Return typed accept, retry, or reject decisions. The processor must not call
   the loop, provider, tool executor, or publisher.
6. Reserve validation retries from their own child budget and keep repair text
   bounded, attributable, and unable to widen the declared output contract.
7. Select one versioned `IOutputSchemaEngine` under the processor's explicit DI
   key. Preflight complete schemas and union alternatives before provider I/O;
   unsupported assertions are configuration failures, never model repairs.
   Revalidate preflight evidence during evaluation and preserve profile
   isolation.

Verify each supported mode, ambiguous unions, invalid native output,
source-order selection, partial streams, retry exhaustion, capability mismatch,
and final deserialization through the public processor contract.
