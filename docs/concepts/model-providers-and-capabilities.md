# Model providers and capabilities

**Status:** Normative  
**Depends on:** [Architecture](architecture-and-dependency-boundaries.md),
[messages](message-and-content-model.md)

## Purpose

This contract describes what may enter the
[provider request pipeline](provider-request-pipeline.md); the
[provider profiles](../providers/index.md) supply current wire-level evidence
for each concrete adapter.

Provider, API family, endpoint/deployment, and model are distinct identities.
Capabilities belong to their configured combination, not to a brand name.

## Identity model

The canonical public `ModelDescriptor` shape is defined once in the
[provider architecture](../architecture/model-and-embedding-providers.md#normative-minimal-identity-and-catalog-shape).
This concept owns the identity and capability semantics rather than a parallel
descriptor declaration.

Two deployments with the same model name MAY have different capabilities,
limits, regions, policy, or API versions. Their descriptors MUST remain
distinct. Aliases MAY resolve to descriptors but MUST NOT erase actual identity
from responses and telemetry.

## Capability inventory

The descriptor MUST express support and relevant constraints for:

- system and developer instructions;
- text, image, audio, file, and generated media input/output;
- application-executed and provider-executed tools;
- parallel tool calls and tool-choice controls;
- native, prompted, and tool-based structured output;
- streaming, usage-in-stream, and partial result behavior;
- reasoning content, signatures, and continuation affinity;
- citations, safety/refusal, and server-side context;
- request continuation/response IDs and cache controls; and
- token counting and context/output limits.

Support SHOULD be more expressive than booleans when modes, schema dialects,
media limits, or mutually exclusive features matter.

## Provider contract

`IChatModel` or equivalent MUST accept a provider-neutral immutable request and
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
Responses, Chat Completions, embeddings, HTTP, streaming, and common errors. It
is not a provider identity. `AgentKit.Providers.OpenAI`,
`AgentKit.Providers.OpenRouter`, and `AgentKit.Providers.ZAi` MUST retain their
own profiles and tests even when they reuse that package.

Provider packages register operations independently. OpenAI may provide
conversation and embeddings; OpenRouter may provide conversation, embeddings,
and reranking; Z.ai MUST expose only the operations in its verified profile.
Applications MAY combine named operations from different providers.

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
- Embeddings from incompatible model revisions cannot share an index query.
- Replacing an embedding or reranking registration does not replace the selected
  conversational model.

## Upstream evidence

- Pi separates provider and API type from model capabilities, limits, cost, and
  compatibility in
  [`packages/ai/src/types.ts`](https://github.com/badlogic/pi-mono/blob/9767ba275f3e9a5ee0f5c5342249b629ab1b2282/packages/ai/src/types.ts).
- OpenCode isolates provider configuration and plugin adapters in
  [`provider.ts`](https://github.com/anomalyco/opencode/blob/337fd144d2ba144743368f78d9579a99cce175bd/packages/core/src/provider.ts).
- Pydantic AI defines a provider-independent model layer documented in
  [models](https://ai.pydantic.dev/models/overview/).

## Related specifications

- [Provider request pipeline](provider-request-pipeline.md)
- [Configuration and overrides](configuration-and-overrides.md)
- [Error taxonomy](error-taxonomy.md)
