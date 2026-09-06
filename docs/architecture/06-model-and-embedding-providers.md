# Model and embedding providers

**Role:** Translate provider-neutral requests into external model operations.

Provider integrations are leaf components. They expose capabilities and perform
one external operation; they do not own the agent loop, history selection, tool
execution, permissions, compaction, or application retry policy.

## Identity and capabilities

Provider, API family, endpoint or deployment, and model are distinct identities.
Capabilities belong to that configured combination, not to a brand name. A
descriptor states supported roles, modalities, tools, structured output,
streaming, reasoning, continuation, usage reporting, schema dialects, and
context limits.

Before I/O, the runtime compares the request with the effective descriptor.
Unsupported behavior is rejected, explicitly downgraded, or routed to another
compatible model. Silent downgrade is forbidden.

## Provider pipeline

One provider attempt validates capabilities, translates the immutable request,
applies bounded provider options, obtains credentials, runs trusted request
middleware, sends through a bounded transport, parses the response as a state
machine, and normalizes the terminal response or error.

The adapter preserves role and content order, call identities, media, usage,
finish reasons, continuation data, and safe unknown fields when supported.
Credentials stay inside the leaf integration and are injected only at send time.
Transport limits cover connection, headers, idle streams, total duration,
frames, bytes, redirects, and decompression.

## Shared wire families

OpenAI-compatible providers may share base translation, transport, and stream
parsing machinery. Reuse is governed by an explicit compatibility profile for
roles, fields, tool shapes, schema restrictions, streaming events, usage, and
errors. Calling an endpoint compatible does not make its behavior identical.

Native providers use their own adapter when shared-wire assumptions would lose
meaning or capability. Vendor SDK types remain inside the integration package.

## Selection, retry, and fallback

A catalog exposes configured descriptors and a selector chooses one from the
run's requirements and policy. The reason and catalog version are observable.
Retries and fallback occur above the single-attempt adapter, consume shared
budgets, and revalidate capability and history affinity. Provider-bound
reasoning, continuation state, or native tool state cannot be discarded merely
to make fallback succeed.

## Embeddings

Embedding generation is a separate provider contract with its own model,
revision, dimensions, modality, normalization, limits, and usage. It does not
appear as an optional method on conversational models. The memory component
enforces compatibility between stored vectors and query embeddings.

## Related concept specifications

- [Model providers and capabilities](../concepts/model-providers-and-capabilities.md)
- [Provider request pipeline](../concepts/provider-request-pipeline.md)
- [Provider research](../providers/index.md)
