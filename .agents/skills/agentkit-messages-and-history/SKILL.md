---
name: agentkit-messages-and-history
description:
  "Implement or review AgentKit's provider-neutral message values and history
  validation or repair contracts. Use for envelopes, content parts, correlation,
  serialization, or provider-facing history preparation; not for context
  selection, session-store mechanics, or provider wire mapping."
---

# AgentKit Messages and History

Read [AGENTS.md](../../../AGENTS.md). For C# API or implementation work, also
read the [modern C# rules](../references/modern-csharp.md).

## Authoritative design

- Read
  [messages and history](../../../docs/architecture/messages-and-history.md) for
  ownership, contracts, dependency direction, and DI.
- Read the normative
  [message and content model](../../../docs/concepts/message-and-content-model.md)
  for durable semantics and round-trip requirements.
- Read
  [history validation and repair](../../../docs/concepts/history-validation-and-repair.md)
  when changing validation, migration, processor, affinity, or repair behavior.

## Working rules

1. Keep neutral message, part, cursor, and correlation values in
   `AgentKit.Abstractions`; do not invent a messages implementation package.
2. Preserve typed identities, source order, message state, unknown safe content,
   and provider correlation. Never retain live SDK objects or credentials.
3. Treat the session record as canonical. A prepared history is an immutable,
   attributable request view and must never rewrite its source snapshot.
4. Fail corrupt locally committed history. Repair only truthfully represented
   interruption or safely normalizable imported/provider-bound content.
5. Untrusted history and processors cannot manufacture authority, approval,
   completed output, or tool success. Revalidate processor output.
6. Treat `ToolResultPart` as a bounded durable/model-facing projection, never
   the authoritative terminal record. Preserve source status, uncertainty,
   retryability, requested alias, optional resolved identity, source
   correlation, projection losses, and the captured policy key/version;
   publication retry reprojects the recorded `ToolCallResult` without repeating
   the effect.
7. Provider and history role projection cannot elevate `RuntimeMessage` or any
   synthetic notice into system/developer instruction trust. Use a tagged
   non-instruction projection or reject unsupported mapping.
8. Keep the first-party history pipeline in `AgentKit.Context`; processors are
   additive and ordered, while validator and repair policy are singular per key.

Verify storage round trips, full-result/projection correlation and loss,
interrupted states, runtime-role non-elevation, provider-affinity loss,
deterministic repair provenance, and stale-version conflicts through public
contracts.
