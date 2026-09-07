# Choosing a provider family

Read this reference only when adding a concrete provider or deciding whether
existing wire machinery can be reused. The canonical ownership and package rules
remain in
[model and embedding providers](../../../../docs/architecture/model-and-embedding-providers.md)
and [project structure](../../../../docs/architecture/project-structure.md).
Provider-specific research and primary links live in the
[provider index](../../../../docs/providers/index.md). For a long-running coding
loop, also inspect the pinned
[coding-harness provider profiles](../../../../docs/providers/coding-harness-provider-profiles.md);
they are interoperability evidence, not current vendor guarantees.

## Heuristics

- Use a shared protocol-family package only for request, response, streaming,
  transport, and error behavior proven common by conformance tests.
- Treat compatibility as a versioned capability profile, never as provider
  identity or permission to claim the union of compatible implementations.
- Prefer a native adapter when a compatibility facade would lose content,
  lifecycle, safety, tool, continuation, usage, or error semantics.
- Treat cloud brokers as concrete providers with deployment, region, identity,
  routing, quota, and policy of their own, even when they reuse a payload
  translator.
- Register conversation, embeddings, reranking, media, and provider-native tools
  independently. Sharing a vendor does not merge operation contracts.
- Treat model discovery as a capability: prefer authoritative credential-scoped
  discovery, otherwise retain a versioned generated/static snapshot with
  provenance, freshness, and confidence.
- Keep canonical tool-call identity distinct from a constrained provider wire
  ID. Any normalization is deterministic, collision-safe, request-scoped, and
  reversible across calls, events, and results.
- Keep endpoint/service-surface profiles, credential/account policy and binding,
  options, descriptors, and registration in the concrete provider package. A
  family package may share secret-safe header/token mechanics proven common, but
  not authentication support, audience, scopes, refresh, or account selection.
  Keep vendor SDK types out of neutral contracts.
- Research coverage is not a commitment to ship a package. Add one only when its
  supported operations and compatibility profile can be stated and tested.

Before implementation, verify the current primary documentation for the exact
API family, version, deployment, model, authentication, stream grammar,
capabilities, limits, errors, and retry signals. Return to the main
[provider-adapter guide](../SKILL.md) for the AgentKit mapping rules.
