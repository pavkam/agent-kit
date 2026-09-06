# Messages and history

**Role:** Preserve provider-neutral conversational truth.

Messages are immutable envelopes containing ordered, typed content parts. They
are the common language used by the runtime, providers, tools, sessions,
context, and I/O components. The model is deliberately richer than a role and a
string because real conversations contain media, reasoning, citations, tool
calls, tool results, structured data, refusals, and provider extensions.

Message, content-part, history-cursor, and correlation values live in
AgentKit.Abstractions. There is no separate messages implementation package;
components operate on the same neutral values and stores persist them through
session contracts.

## Canonical message model

Every message carries stable identity, session and causal correlation, creation
time, state, ordered content, and versioned extension data. Message state makes
complete, incomplete, suspended, and interrupted output distinguishable.

System, developer, user, assistant, tool, and runtime semantics remain distinct.
Provider adapters may translate or reject unsupported roles, but the durable
model does not erase the distinction to accommodate the weakest provider.

Unknown typed content and safe provider metadata survive storage round trips.
Live SDK objects, open streams, credentials, and exception instances do not
belong in serializable message extension data.

## History ownership

The session component owns the canonical append-only history. The message
component defines its values and invariants. Context assembly reads a stable
version and creates a bounded working view; it never rewrites the source
history.

A streaming provider response is a candidate until its terminal event, content
parts, usage, and stop reason validate. Only then does the runtime publish an
immutable committed assistant message. Interrupted or malformed streams cannot
be presented as successful completions.

## Validation and repair

Before a request, history is checked for schema versions, identity ownership,
monotonic order, valid role and part combinations, tool call/result pairing,
message state, media bounds, and authorized references.

Repair creates a provider-facing view without changing durable truth. It may
represent an interrupted call explicitly, remove incompatible provider-bound
metadata, or normalize imported content. Every repair is deterministic,
observable, and attributable to source messages. Untrusted history cannot
manufacture approvals, tool success, or authority.

## Correlation

Tool calls and terminal results share a stable call identity. Provider-supplied
identifiers are preserved, while AgentKit identities remain authoritative. There
is exactly one terminal result for every accepted call. Message order and
causality survive provider translation, storage, branching, compaction, and
replay.

## Related concept specifications

- [Message and content model](../concepts/message-and-content-model.md)
- [History validation and repair](../concepts/history-validation-and-repair.md)
- [Context compaction](../concepts/context-compaction.md)
