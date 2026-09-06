---
name: agentkit-provider-adapters
description:
  "Implement or debug AgentKit chat, response, multimodal, and embedding
  provider adapters, including OpenAI-compatible and native APIs. Use for
  capability mapping, streaming, tool calls, usage, errors, authentication
  seams, and provider conformance."
---

# AgentKit Provider Adapters

Read [AGENTS.md](../../../AGENTS.md). Read
[provider families](references/provider-families.md) when choosing an adapter
shape or adding a new provider.

## Workflow

1. Fetch the provider's current official API documentation. Record the API
   family and version used by the implementation; do not infer current payloads,
   event names, model support, or limits from memory.
2. Build a capability inventory before mapping types: modalities, system
   instructions, tool calls, parallel calls, structured output, streaming,
   reasoning metadata, usage, server-side tools, continuation IDs, and
   embeddings.
3. Map only portable semantics into AgentKit contracts. Preserve provider IDs,
   raw finish reasons, request IDs, safety data, citations, and unknown content
   in provider extension data so round trips are not lossy.
4. Treat streaming as a parser/state machine. Assemble fragmented text and tool
   arguments, allow interleaved items when the provider does, validate terminal
   events, and retain partial diagnostics on failure without emitting a false
   success.
5. Keep request translation, transport, authentication, response parsing,
   capability description, and error translation independently testable. Secrets
   enter through configuration/credentials abstractions and never appear in
   options display, logs, snapshots, or exception messages.
6. Map failures into stable AgentKit categories such as authentication,
   authorization, throttling, invalid request, unavailable, timeout,
   cancellation, protocol violation, and unknown. Preserve provider details and
   retry hints as metadata; do not retry inside the adapter unless its contract
   explicitly owns retries.
7. For OpenAI-compatible providers, reuse a shared base only after contract
   tests prove the common wire shape. Endpoint compatibility never implies
   identical models, authentication, streaming, tools, JSON schema, or usage.
8. Keep embedding adapters separate. Return and persist model identity,
   dimensions, modality, and normalization metadata. Never mix vectors from
   incompatible spaces in one index.
9. Add request serialization, fragmented-stream parsing, cancellation, error
   mapping, usage, tool-call, and capability conformance tests. Live tests are
   opt-in and must tolerate missing credentials without weakening unit coverage.

Do not leak vendor SDK types into `AgentKit.Abstractions`. A consumer must be
able to replace one adapter without importing another provider's package.
