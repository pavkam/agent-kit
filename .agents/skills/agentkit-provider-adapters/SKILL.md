---
name: agentkit-provider-adapters
description:
  "Implement or debug AgentKit conversational, multimodal, embedding, and
  reranking provider adapters. Use for capability mapping, wire translation,
  streaming, usage, errors, credentials, and provider conformance."
---

# AgentKit Provider Adapters

Follow
[Model and embedding providers](../../../docs/architecture/model-and-embedding-providers.md),
[model capabilities](../../../docs/concepts/model-providers-and-capabilities.md),
and the
[provider request pipeline](../../../docs/concepts/provider-request-pipeline.md).
When changing C#, also read the
[modern C# rules](../references/modern-csharp.md).

Read [provider families](references/provider-families.md) only when adding a
provider or deciding between a shared wire family, native adapter, and cloud
broker. For wire behavior, verify the current primary provider documentation
linked from the [provider index](../../../docs/providers/index.md).

For coding-harness interoperability also read the
[coding-harness provider profiles](../../../docs/providers/coding-harness-provider-profiles.md).
They define route-level compatibility requirements and do not replace current
official vendor protocol documentation.

## Decision guide

1. Keep `AgentKit.Providers` provider-neutral: catalog, selection, capability
   validation, and request execution. Concrete provider packages own
   credentials, endpoints, options, descriptors, translation, parsing, and error
   mapping.
2. Describe capabilities for the configured provider, API family, service
   surface, endpoint/deployment, model, candidate multiplicity, usage-reporting
   behavior, and operation before I/O. Unsupported behavior is rejected,
   explicitly downgraded, or reselected; never silently assumed.
3. Register conversation, embeddings, reranking, media, and provider-native
   tools as independent operations. A vendor may implement several without
   merging their contracts or replacement paths.
4. Make one leaf adapter perform one provider attempt. Selection, safe retry,
   and fallback stay in the provider executor above it and consume shared
   budgets.
5. Preserve portable semantics plus provider identities, call correlation,
   content order, trust/provenance, usage availability, finish reasons,
   continuation data, safety information, upstream routing, and safe unknown
   extensions. Make one-to-many or many-to-one wire projection explicit and
   never discard extra response candidates. Translate only the bounded
   tool-result projection and never reconstruct its authoritative execution
   record or infer status from display text.
6. Parse streams as typed state machines across arbitrary fragmentation and
   interleaving. Never emit success after malformed or incomplete termination.
7. Reuse a protocol-family package only for behavior proven common by
   conformance. Wire compatibility is neither provider identity nor a union of
   every compatible endpoint's capabilities.
8. Bind every operation to keyed, versioned endpoint and credential/account
   profiles. Resolve credentials only at send time, authorize classified egress,
   and keep secrets out of options display, logs, snapshots, exceptions, and
   records. Shared wire mechanics do not own provider authentication policy.
9. Map failures to stable AgentKit categories while retaining safe provider
   status, code, request identity, retry-hint form/provenance, and side-effect
   certainty. Normalize delay and absolute-date hints with `TimeProvider`; hints
   remain advisory.
10. Test serialization, fragmented streaming, cancellation, capability claims,
    tool calls, usage, error mapping, credentials, registration, and operation
    replacement without requiring live services.

Vendor SDK types remain in leaf packages. Provider-neutral contracts must not be
shaped around whichever API was implemented first.
