# Model and embedding providers

**Role:** Select a compatible operation and translate provider-neutral requests
into external model services.

Provider behavior has two layers. AgentKit.Providers owns provider-neutral
runtime mechanics. Concrete AgentKit.Providers.ProviderName packages own
endpoints, credentials, wire translation, stream parsing, errors, and capability
descriptors. Neither layer owns the agent loop, history selection, tool
execution, permissions, compaction, or durable storage.

## Package split

AgentKit.Providers contains the first-party model catalog, selector, capability
validator, and model request executor. AddAgentProviders registers those
replaceable services. The package never references a vendor SDK or assumes an
OpenAI wire shape.

The initial integration family is:

| Package                             | Responsibility                                                                                          | Operations                                                               |
| ----------------------------------- | ------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------ |
| AgentKit.Providers.OpenAICompatible | Reusable Responses, Chat Completions, embeddings, transport, parsing, and tested compatibility profiles | Building blocks for concrete compatible packages and custom endpoints    |
| AgentKit.Providers.OpenAI           | OpenAI identity, endpoints, credentials, options, descriptors, and registration                         | Conversation and embeddings                                              |
| AgentKit.Providers.OpenRouter       | OpenRouter routing, upstream provenance, metadata, credentials, options, and registration               | Conversation, embeddings, and reranking                                  |
| AgentKit.Providers.ZAi              | Z.ai identity, endpoints, credentials, Chat Completions profile, and provider-native operations         | Conversation and only other operations supported by its verified profile |

OpenAICompatible is deliberately not the package applications normally select as
their provider. OpenAI, OpenRouter, and Z.ai have different authentication,
extensions, model catalogs, capability claims, errors, and usage even when some
requests look alike.

Each concrete package exposes an ASP.NET-style registration entry point:
AddOpenAI, AddOpenRouter, or AddZAi. Registrations may add several named models,
but conversation, embedding, and reranking capabilities remain independent.
Adding OpenAI can register both conversational and embedding models. Adding
OpenRouter can register conversational, embedding, and reranking models. Z.ai
does not advertise an embedding implementation unless its current verified API
actually provides one.

## Identity and capabilities

Provider, API family, endpoint or deployment, and model are distinct identities.
Capabilities belong to that configured combination, not to a brand name. A
descriptor states supported roles, modalities, tools, structured output,
streaming, reasoning, continuation, usage reporting, schema dialects, and
context limits.

Before I/O, the provider runtime compares the request with the effective
descriptor. Unsupported behavior is rejected, explicitly downgraded, or routed
to another compatible model. Silent downgrade is forbidden.

Registration produces immutable, named descriptors and operation factories.
Names are application aliases; results, telemetry, durable state, and vector
metadata preserve the actual provider, upstream provider where routed, API
family, model, and deployment identity.

## Provider pipeline

One provider attempt validates capabilities, translates the immutable request,
applies bounded provider options, obtains credentials, runs trusted request
middleware, sends through a bounded transport, parses the response as a state
machine, and normalizes the terminal response or error.

The adapter preserves role and content order, call identities, media, usage,
finish reasons, continuation data, safety information, upstream routing, and
safe unknown fields when supported. Credentials stay inside the concrete
integration and are injected only at send time. Transport limits cover
connection, headers, idle streams, total duration, frames, bytes, redirects, and
decompression.

## Shared wire families

AgentKit.Providers.OpenAICompatible exposes reusable base classes and services
only where they remove proven duplication: request translation, JSON contracts,
HTTP behavior, SSE parsing, stream assembly, common errors, and compatibility
profile evaluation. Direct interface implementation remains supported.

Every concrete compatible package supplies an explicit profile for roles,
fields, tool shapes, schema restrictions, streaming events, usage placement,
finish reasons, authentication, and provider extensions. The shared package
cannot broaden that profile. A provider test suite must run both shared-family
conformance and concrete-package tests.

Native providers use independent adapters when shared-wire assumptions would
lose meaning or capability. Cloud brokers keep deployment, identity, region, and
platform policy in their concrete package even when they reuse a payload
translator. Vendor SDK types never enter AgentKit.Abstractions.

## Selection, retry, and fallback

The catalog exposes configured descriptors and the selector chooses one from the
run's requirements and policy. The reason and catalog version are observable.
The model request executor invokes the selected concrete adapter and owns
provider retry and fallback decisions within the loop's reserved budgets.

Retries and fallback occur above the single-attempt adapter, consume shared run
budgets, and revalidate capabilities and history affinity. Provider-bound
reasoning, continuation state, native tool state, or routed-provider identity
cannot be discarded merely to make fallback succeed. There is no generic
resilience package allowed to replay arbitrary provider work behind the loop's
back.

## Embeddings and reranking

Embedding generation is a separate provider contract with its own provider,
model, revision, dimensions, modality, normalization, limits, and usage. It does
not appear as an optional method on conversational models. The memory component
enforces compatibility between stored vectors and query embeddings.

Reranking is another independent semantic operation. Its request, score
semantics, model identity, limits, and usage differ from embeddings. A package
may implement conversation, embeddings, and reranking together, but applications
select each named operation independently and may use different vendors for
each.

Provider-native tools, token counting, media generation, files, caches, and
hosted retrieval also remain explicit capabilities. AgentKit does not pretend
that installing a vendor package turns every endpoint in that vendor's control
plane into part of the conversational model contract.

## Related documentation

- [Model providers and capabilities](../concepts/model-providers-and-capabilities.md)
- [Provider request pipeline](../concepts/provider-request-pipeline.md)
- [Provider research](../providers/index.md)
- [Semantic operations](../providers/semantic-operations.md)
- [Project structure](project-structure.md)
