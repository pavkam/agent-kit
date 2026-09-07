# Model providers and capabilities

**Status:** Normative

**Architecture:**
[Model and embedding providers](../architecture/model-and-embedding-providers.md)

**Depends on:** [Architecture](architecture-and-dependency-boundaries.md),
[messages](message-and-content-model.md)

## Purpose

This contract describes what may enter the
[provider request pipeline](provider-request-pipeline.md); the
[provider profiles](../providers/index.md) supply current wire-level evidence
for each concrete adapter.

Provider, API family, service surface, endpoint, deployment, and model are
distinct identities. Capabilities belong to their configured combination, not to
a brand name.

A provider registration is the concrete runtime unit for one provider identity:
it owns authentication methods, endpoint/profile metadata, model catalog and
refresh behavior, and independently registered operations. It MAY dispatch
different models through different API families. A model whose declared API
family has no registered operation fails before transport; the runtime never
guesses from the provider name.

## Identity model

The canonical public `ModelDescriptor` shape is defined once in the
[provider architecture](../architecture/model-and-embedding-providers.md#normative-minimal-identity-and-catalog-shape).
This concept owns the identity and capability semantics rather than a parallel
descriptor declaration.

Two service surfaces, endpoints, or deployments with the same model name MAY
have different capabilities, limits, regions, routing, billing, retention,
authentication, policy, or API versions. Their descriptors MUST remain distinct.
Aliases MAY resolve to descriptors but MUST NOT erase actual identity from
responses and telemetry.

Composition MUST keep three bindings independent: an endpoint profile selects
the service surface and transport target, a credential profile selects one
account/workload identity and authentication policy, and an operation
registration binds one model adapter to both. Those bindings are keyed,
versioned, validated, and captured before an attempt. A process-global unkeyed
credential source or endpoint option cannot safely represent several providers,
accounts, or service surfaces in one engine.

The credential-profile reference is classified execution/configuration evidence,
not ordinary assistant-message metadata. Response identity preserves the actual
provider, surface, endpoint, deployment, and model; credential binding appears
only in protected operation/audit records under explicit redaction and access
policy.

## Capability inventory

The descriptor MUST express support and relevant constraints for:

- system and developer instructions;
- text, image, audio, file, and generated media input/output;
- application-executed and provider-executed tools;
- parallel tool calls and tool-choice controls;
- native, prompted, and tool-based structured output;
- streaming, usage-in-stream, and partial result behavior;
- candidate/choice counts and multi-candidate stream behavior;
- whether usage can be absent, interim, or terminal and which counters may be
  unavailable;
- reasoning content, signatures, and continuation affinity;
- citations, safety/refusal, and server-side context;
- request continuation/response IDs and cache controls; and
- token counting and context/output limits.

Support SHOULD be more expressive than booleans when modes, schema dialects,
media limits, or mutually exclusive features matter.

Capabilities also describe operational details that affect applications:
supported thinking levels and their provider mapping, cache retention modes,
session affinity, deferred response/tool loading, tool-name and call-ID
constraints, usage availability, transport choices, and whether model discovery
is static or credential-scoped.

Reasoning capability is a structured profile, not a list of display labels. It
declares the mechanism—effort, token budget, toggle, adaptive mode, or a
provider template field—its legal values and mapping, replay/signature binding,
summary and display behavior, and whether reasoning tokens are included in
reported output usage. A reasoning block that is valid only with one prior
response, model, tool set, or provider route carries that affinity explicitly.

## Provider contract

`ILlmModel` or equivalent MUST accept a provider-neutral immutable request and
return a typed stream/result. The adapter owns wire translation, transport,
authentication injection, stream parsing, raw error capture, and normalized
provider failure mapping.

The adapter MUST NOT own agent retries, tool execution, security policy, history
selection, or compaction. It MAY expose a provider-specific request extension
surface that is validated and isolated from other adapters. It uses the shared
security authority and network boundary for destination and data egress rather
than deciding its own authority.

## Package ownership

`AgentKit.Providers` owns the first-party catalog, selector, capability
validation, and model request execution. It MUST NOT contain vendor protocol.
Concrete integrations use `AgentKit.Providers.<ProviderName>` and own their
endpoint, credentials, options, profiles, descriptors, wire behavior, and
registration.

`AgentKit.Providers.OpenAICompatible` is a reusable protocol-family package for
Responses, Chat Completions, embeddings, HTTP, streaming, common errors, and
secret-safe credential transport mechanics proven common by conformance. It is
not a provider identity and MUST NOT decide authentication support, account,
scheme, audience, scopes, refresh, or rotation policy. Those choices and the
credential-profile binding remain owned by the concrete branded package.
`AgentKit.Providers.OpenAI`, `AgentKit.Providers.OpenRouter`, and
`AgentKit.Providers.ZAi` MUST retain their own profiles and tests even when they
reuse that package.

Provider packages register operations independently. OpenAI may provide
conversation and embeddings; OpenRouter may provide conversation, embeddings,
and reranking; Z.ai MUST expose only the operations in its verified profile.
Applications MAY combine named operations from different providers.

## Catalog lifecycle

Catalog reads return one immutable last-known snapshot and never perform hidden
network I/O. Static baseline models and a dynamic overlay merge by stable model
identity; an overlay replaces the matching baseline descriptor rather than
creating an ambiguous duplicate.

Every descriptor claim carries catalog source, checked-at time, and confidence
or provenance sufficient to distinguish provider-reported, generated,
heuristically inferred, manually corrected, and application-overridden data.
Runtime discovery is used only when the provider actually offers an
authoritative surface; a generated or reviewed static snapshot is a valid
explicit fallback, not a reason to fabricate live discovery.

Refresh is explicit, cancellable, and provider-scoped. A dynamic provider:

1. restores its validated cached snapshot before requiring current credentials
   or network access;
2. skips network work in cache-only mode;
3. resolves or refreshes credentials only when network access is allowed;
4. publishes persistence and in-memory state through one generation-checked
   operation; and
5. retains the prior usable snapshot when refresh fails.

Concurrent refreshes for one provider are superseded or serialized by a
generation/fence. Replacing or removing a provider invalidates in-flight refresh
publication so stale results cannot repopulate the new registration. Refreshing
several providers MAY run concurrently, but errors and cancellation remain
attributable per provider.

Credential-specific availability is a filter over the complete catalog, not a
mutation of its descriptors. A subscription token may expose fewer models than
an API key for the same provider.

## Capability negotiation

Before I/O, request validation MUST compare the request with the effective
descriptor. For unsupported features the configured policy must choose one:

- reject with `UnsupportedCapability`;
- use a documented loss-aware downgrade;
- select a compatible model; or
- omit optional behavior with an observable diagnostic.

Silent downgrade is forbidden. `NotSupportedException` after sending is not
capability negotiation.

## Compatibility profiles

OpenAI-compatible and other shared wire families MUST use explicit tested
profiles for differences including roles, schema keywords, tool-choice shape,
stream events, usage placement, finish reasons, headers, and reasoning fields.
Endpoint compatibility is evidence for reuse, not proof of equivalence.

Unknown provider response fields SHOULD be preserved in extension data. Request
profiles MUST avoid sending fields a deployment is known not to accept.

## Selection and fallback

`IModelSelector` receives requirements, policy, run context, and candidate
descriptors. Its decision MUST record why a model was chosen. Selection SHOULD
be deterministic for the same catalog version and inputs unless a declared
load-balancing policy uses injectable randomness.

Fallback MUST revalidate capabilities and request translation. A fallback MAY be
unsafe when history contains provider-bound reasoning signatures, continuation
IDs, native tool state, or unsupported media. The selector MUST fail rather than
corrupt context.

Thinking/reasoning selection MUST use the model's supported-level map. An
unsupported requested level follows an explicit clamp/reselect/fail policy and
records the effective provider value; the numeric or string spelling is not
assumed common across API families.

## Embeddings are separate

Canonical embedding and reranking request, identity, and result semantics live
in the [semantic-operation profile](../providers/semantic-operations.md).

Embedding generation MUST use a separate contract and descriptor containing
provider/model/revision, dimensions, modality, normalization, and limits.
Conversational generation interfaces MUST NOT sprout optional embedding methods.
Vector compatibility is enforced by storage and retrieval contracts.

Reranking MUST use another separate contract and descriptor. Reranking scores,
query/document asymmetry, model identity, usage, and limits MUST NOT be inferred
from an embedding contract.

## Acceptance scenarios

- A selected deployment reports capabilities narrower than its provider's
  catalog and request validation honors them.
- Unsupported parallel tools fail or downgrade according to explicit policy.
- A shared wire adapter passes distinct compatibility profiles for two
  endpoints.
- Fallback refuses to discard provider-bound continuation state silently.
- Actual response model identity survives alias-based selection.
- Two accounts for the same provider remain isolated through distinct captured
  credential profiles.
- A single-candidate operation rejects an unexpected second candidate instead of
  silently selecting the first.
- Missing provider usage remains unknown rather than becoming reported zero.
- Embeddings from incompatible model revisions cannot share an index query.
- Replacing an embedding or reranking registration does not replace the selected
  conversational model.
- A mixed-API provider dispatches each model only to its declared family.
- Provider replacement prevents an older model-refresh result from publishing.
- Cache-only startup restores a validated catalog without auth or network I/O.
- Credential-scoped availability filters models without changing the canonical
  catalog snapshot.

## Related specifications

- [Provider request pipeline](provider-request-pipeline.md)
- [Configuration and overrides](configuration-and-overrides.md)
- [Error taxonomy](error-taxonomy.md)
- [Coding-harness provider profiles](../profiles/coding-harness/provider-interoperability.md)
