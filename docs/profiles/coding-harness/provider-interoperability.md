# Coding-harness provider profiles

**Status:** Normative application profile

**Scope:** Optional coding-harness interoperability; not required by AgentKit
core.\
**Last reviewed:** 2026-09-07

## Purpose

A coding harness exercises provider behavior that a one-shot chat client can
ignore: mixed-provider history, long tool loops, partial JSON, durable retries,
prompt-cache stability, credential rotation, deferred responses, and recovery
after transport certainty is lost.

This document defines the AgentKit requirements that apply across those routes.
It is not a provider inventory, a support matrix, or evidence that a named
integration ships. Each concrete provider page and its linked first-party
documentation remain authoritative for public wire behavior. Undocumented or
private service surfaces are outside the portable contract.

## Provider registration is a route compiler

A provider name alone does not select a wire contract. One provider may expose
different API families by model, account, region, deployment, credential, or
service tier. AgentKit therefore resolves an immutable route containing at
least:

- provider, account or credential owner, operation kind, API family, endpoint,
  deployment, region, and model identity;
- descriptor and compatibility-profile revisions, catalog source, checked-at
  time, confidence, and manual-override provenance;
- declared input, output, reasoning, tool, structured-output, cache, deferred,
  streaming, usage, candidate-multiplicity, and transport capabilities;
- authentication mechanism, required endpoint variables, header policy, API
  version, timeout, retry ownership, and data-retention policy; and
- the request translator, stream parser, error normalizer, usage normalizer, and
  history-preparation policy selected for that exact route.

One branded registration may dispatch among several API families. Each model
descriptor names its family explicitly, and composition fails when that family
has no registered implementation. OpenAI compatibility is only a starting point
for a versioned compatibility profile; it never implies complete Chat
Completions or Responses behavior.

Route compilation must happen before the attempt begins. Catalog refresh,
credential rotation, or configuration reload cannot silently replace a route
mid-attempt.

## Catalog lifecycle

Runtime discovery is neither universally available nor automatically
authoritative. AgentKit uses the strongest source supplied by the provider:

1. authoritative credential-scoped runtime discovery, when available;
2. a versioned generated catalog from a documented vendor feed;
3. a reviewed static baseline with source and verification time; then
4. explicit application overrides, recorded as overrides rather than facts.

Catalog publication uses a last-known-good immutable snapshot. Startup may
restore a persisted snapshot before network access. Refresh is provider-scoped,
generation-fenced, serialized at publication, and replaceable. A stale refresh
cannot overwrite a later registration or snapshot. A failed refresh records
diagnostics while retaining the prior snapshot.

A credential may filter its published catalog. An account-specific view must not
mutate an engine-global catalog or reveal entitlements to another account.

Generated catalogs may contain allowlists, denylists, inferred capabilities,
cost corrections, and model-name heuristics. Descriptors preserve the origin of
each claim and whether it is verified, reported, inferred, or overridden.
Substrings such as vision, reasoning, or pro are not capability proofs.

## Credentials, endpoints, and headers

Credential resolution occurs for every logical request and has deterministic
ownership:

1. a trusted request-scoped credential reference, when policy permits it;
2. the stored credential selected for the provider and account profile; then
3. ambient credentials only when no stored credential owns that profile, unless
   the profile explicitly declares a composite source.

A composite profile enumerates every independently mergeable field and retains
field-level source provenance. This matters for routes that separately require
an API key, account identifier, gateway identifier, project, location, or
deployment.

Outside a declared composite profile, an incompatible, expired, or failed stored
credential does not silently fall through to an environment variable. OAuth
refresh is single-flight per provider and account, checks freshness again after
acquiring the lock, applies a bounded timeout, persists rotated credentials
before releasing waiters, and never writes tokens into messages, manifests,
logs, or durable operation state.

Command-backed credentials are explicit protected credential sources, not magic
string syntax. Resolution happens at send time under the captured account, route
profile, and execution identity. The source declares expiry, cache scope,
timeout, output bounds, and typed failures. A failed command does not fall
through to an ambient credential or populate a process-global cache shared
across accounts.

Endpoint and region values can be credential-dependent. Cloudflare account and
gateway IDs, Azure resources and deployments, Vertex projects and locations,
Bedrock regions, and broker gateways are route data, not ad hoc string
concatenation performed by request code.

Header merge is case-insensitive. An explicit removal suppresses a lower layer;
different casing cannot resurrect the header. The final trusted transform runs
immediately before dispatch and participates in the provider-egress
authorization fingerprint. Signed transports reject or normalize reserved
headers before signing so the authorized request and sent request remain
identical.

## Canonical history and wire repair

Canonical messages are never mutated to satisfy a destination provider. A
provider-ready view performs a recorded, loss-aware transformation:

- normalize provider-invalid null content without losing the original value;
- reject unsupported media or use a distinct attributable placeholder rather
  than pretending the model received the original bytes;
- replay encrypted, signed, redacted, or opaque reasoning only to the same
  provider, API family, and compatible model profile;
- retain an empty signed reasoning part when its signature is semantically
  required, but drop thought signatures on incompatible routes;
- convert portable visible thinking to ordinary text when crossing providers and
  record that transformation;
- omit assistant attempts that settled as error, aborted, deferred, or unknown;
- synthesize correlated error results for orphaned tool calls before the next
  assistant or user boundary or the end of history; and
- sanitize invalid Unicode before JSON encoding without silently changing the
  canonical stored message.

Canonical tool-call identity and target wire identity are distinct. When a
protocol imposes a shape or length constraint, the adapter creates a
deterministic, collision-safe, request-scoped bijection and restores canonical
identity on every event and result. Array position is never identity.

One correlation map applies to assistant calls, tool results, staged durable
state, and later provider events. It is retained for the logical request and
recovery evidence.

## Structured sampling is a dialect

A boolean schema capability cannot describe real compatibility. A route profile
states:

- the accepted schema dialect and rejected keywords;
- whether optional properties are absent, required-nullable, or represented by
  another provider-specific form;
- whether closed objects, strict mode, or a grammar wrapper are required;
- prefer-versus-require behavior and the typed unsupported outcome;
- whether application output, tool arguments, and constrained sampling are
  separate mechanisms; and
- how partial JSON or grammar deltas are accumulated and validated.

Schema lowering reports every removed or retyped keyword, enum conversion,
nullable rewrite, and weakened tuple constraint. It never silently weakens the
canonical schema. Tool registration alone is not proof of strict structured
output.

## Streaming, retry, errors, and usage

Stream parsers operate across arbitrary byte fragmentation. They handle CR, LF,
CRLF, multiline SSE data, comments, unknown events, residual EOF data, partial
UTF-8, partial JSON, and provider-specific terminal frames. HTTP success and a
first content delta are not terminal success. A protocol requiring a terminal
event fails if the stream closes without it.

Fallback to another transport or route is permitted only before model-visible
output, tool calls, or other externally observable progress. After progress,
automatic fallback would splice two attempts into one response.

The AgentKit resilience layer owns retry policy for one logical attempt. SDK and
adapter retries are disabled or surfaced to that owner. Delay and absolute-date
retry hints are advisory, normalized through TimeProvider, bounded, budgeted,
and cancellable. A retry still follows durable intent, effect, and settlement
rules.

Error normalization retains the stable category plus safe provider status, code,
request ID, retry-hint provenance, original exception, and bounded raw body.
Truncation is explicit and includes a digest when useful. Raw provider text does
not become a model-visible message by default.

Usage fields are optional observations, not required numbers. Not reported,
interim, final, and reported zero are distinct. Input, cached-read,
cached-write, output, reasoning, audio, image, search, and provider-billable
units remain separate native counters. Derived totals never overwrite raw usage.

## API-family compatibility requirements

These profiles describe behavior that an implementation of the family must
model. They do not imply that AgentKit currently registers every family.

| API family                     | Harness-critical requirements                                                                                                                                                                                                                                                                                                         |
| ------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| OpenAI Chat Completions        | Version compatibility by role, token-limit field, finish and usage streaming, tool-result name and adjacency, reasoning replay field, thinking dialect, strict or grammar tools, cache markers, session affinity, deferred tools, and priority. Correlate tool deltas by both index and ID. Reject a missing required finish reason.  |
| OpenAI Responses               | Preserve response and item IDs, phases, namespaces, encrypted reasoning, hosted-tool state, incomplete reason, and function call IDs. Do not replay provider item IDs across incompatible routes. Model queued and in-progress statuses as deferred state. Require a terminal event.                                                  |
| Azure OpenAI Responses         | Normalize Azure roots without erasing legitimate proxy paths. Resolve deployment and API version explicitly. Preserve route-specific retention, cache-key limits, model/deployment placement, and reasoning replay fields.                                                                                                            |
| Anthropic Messages             | Require message termination, parse multiline SSE and fragmented tool JSON, preserve thinking signatures and redacted thinking, retain raw stop and serving model, and keep cache-read and cache-write usage distinct. Treat beta headers and OAuth behavior as versioned route capabilities.                                          |
| Gemini GenerateContent         | Preserve part-local thought signatures only on compatible routes. Treat function-call IDs, function-response grouping, image results, schema dialect, safety stops, and candidate multiplicity as explicit model capabilities.                                                                                                        |
| Vertex Gemini                  | Preserve native Gemini content semantics while binding authentication, project, location, and endpoint profile independently. Do not infer that every Vertex model family uses the Gemini adapter.                                                                                                                                    |
| Bedrock Converse               | Parse AWS event-stream framing and preserve model-family-specific reasoning, cache, guardrail, error, and stop semantics. Bind credential chain, region, inference profile, signing, and media rules to the route. Await terminal metadata required for final usage.                                                                  |
| Mistral Chat and Conversations | Map canonical tool IDs to exactly nine alphanumeric wire characters through a reversible, collision-safe bijection. Preserve naming conversion, multiline SSE, cache-token variants, native thinking, strict tools, affinity, and the route-specific reasoning mode.                                                                  |
| OpenRouter Chat and Responses  | Preserve upstream routing policy, actual response model, reasoning configuration, usage inclusion, cache keys, data policy, application attribution headers, and fallback settings. Treat chat-based image output and the dedicated image-generation surface as distinct operation profiles rather than generic OpenAI compatibility. |

## Provider-specific compatibility requirements

The following rows are implementation requirements for a leaf package when that
integration exists. They are not a package roadmap or support declaration.

| Provider or service | Required profile distinctions                                                                                                                                                                                                                                                                                                                                     |
| ------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| OpenAI              | Chat and Responses are separate route families. Preserve response, item, and function-call identities; encrypted reasoning replay; hosted-tool ownership; candidate multiplicity; and image-result placement rules. Bind API-key and account-based authentication as distinct profiles.                                                                           |
| Anthropic           | Filter invalid empty wire blocks without changing canonical history. Preserve signed or encrypted thinking, adaptive versus budgeted modes, ordered system updates, tool-streaming behavior, cache controls, beta headers, and deferred stop states by route. Wire input tokens exclude cache reads and writes, so normalize totals without erasing raw counters. |
| Amazon Bedrock      | Keep credential-chain mechanism, bearer or session token, region, inference-profile prefix, model family, signing, event-stream CRC framing, and media naming rules in the route. Do not fabricate a default region.                                                                                                                                              |
| Google Gemini       | Treat synthesized call IDs as response-scoped when the wire lacks stable IDs. Preserve thought signatures, safety reasons, cached-input and thought-token counters, schema rewrites, candidate count, and image-in-function-response support.                                                                                                                     |
| Google Vertex AI    | Separate project and location aliases, global and regional endpoints, API-key and application-default credentials, native SDK ownership, compatibility endpoints, and hosted third-party model domains.                                                                                                                                                           |
| Azure OpenAI        | Distinguish resource-name and explicit-base-URL profiles. API-key, identity, and custom authentication have different header rules. API version, deployment, endpoint shape, and operation family jointly select the route.                                                                                                                                       |
| OpenRouter          | Preserve nested reasoning, usage inclusion, cache keys, versioned attribution-header spelling, upstream provider and model routing, data policy, ordering, fallback, and effort vocabulary. Retain the actual upstream model identity.                                                                                                                            |
| xAI                 | Chat and Responses are distinct tested profiles, not interchangeable transports. Voice, hosted tools, files, collections, embeddings, and batch capabilities remain operation-specific.                                                                                                                                                                           |
| Mistral             | Use a collision-safe reversible mapping for constrained wire tool IDs. Any adjacency repair is a provider-ready projection with provenance, never a canonical-history mutation.                                                                                                                                                                                   |
| DeepSeek            | Replayed assistant messages may require an explicit reasoning field, including an empty value. That is destination-profile repair, not a canonical message field or generic Chat behavior. Treat Responses, Messages, and Chat as independent families.                                                                                                           |
| Moonshot Kimi       | Keep schema restrictions, reasoning behavior, deferred tools, files, caches, and batch behavior in the selected route. Schema projection returns typed loss diagnostics.                                                                                                                                                                                          |
| Groq                | Treat Chat, beta Responses, Compound, batch, and speech operations independently. Model-specific reasoning fields and structured-output subsets require separate conformance fixtures.                                                                                                                                                                            |
| Z.ai                | Preserve regional endpoint, coding-plan, reasoning, hosted-search, files, media, and context-overflow behavior in versioned profiles. Do not infer one route from the provider brand.                                                                                                                                                                             |
| Microsoft Copilot   | Public Microsoft 365, Copilot Studio, GitHub administration, and any inference integration are separate contracts. Entitlement-scoped catalogs remain account-local and cannot be generalized into public model availability.                                                                                                                                     |
| Ollama              | Native Chat, native Generate, and compatibility endpoints are separate profiles. Local model metadata, server version, context settings, and missing usage evidence must remain explicit.                                                                                                                                                                         |

Provider-specific exception rules require exact positive and negative fixtures
and retain the raw status and bounded body. They never become generic family
behavior merely because two services share a wire shape.

## Silent overflow classification

Some endpoints can return a successful HTTP status while truncating or refusing
context through provider-specific signals. A route may classify that outcome as
context overflow only when a verified profile defines positive evidence.

The classifier must:

- retain the raw finish status, usage, headers, and bounded body;
- distinguish positive, negative, and insufficient evidence;
- avoid treating a full context window, reported zero usage, or a generic length
  finish as universal proof;
- remain versioned by provider, endpoint, and model profile; and
- return an unknown or provider-failure outcome when evidence is insufficient.

Examples of route-specific evidence include reported input usage exceeding the
selected context limit, or a length finish with zero output and input usage at a
verified near-limit threshold. Neither heuristic is portable: its exact
combination and threshold belong to the versioned route profile.

For local compatibility servers, missing or inconsistent usage often makes the
cause unknowable. AgentKit reports that uncertainty instead of inventing a
provider error.

## Deliberate AgentKit requirements

- Canonical content retains text, images, audio, documents, video, citations,
  artifacts, and provider-native parts as explicit capabilities.
- Stream events are immutable deltas or immutable snapshots with stated
  ownership.
- SSE framing is shared and conformance-tested rather than reimplemented by each
  adapter.
- Private subscription endpoints are not portable provider integrations.
- Adapter-owned hidden retry loops are forbidden.
- Generated capability heuristics never become verified descriptors silently.
- Multiple runtime implementations for one route share capabilities only after
  passing the same route-level conformance suite.
- Model-name or SDK-version heuristics never silently strip a requested option.
- Schema and media downgrades return typed loss diagnostics.
- Unknown provider event and metadata variants survive as bounded extension data
  when safe.

## Conformance requirements

Every provider route runs the applicable reusable suites:

- model and catalog replacement, persisted restore, failed refresh, stale
  refresh, credential-specific filtering, and cross-tenant isolation;
- request, stored, and ambient credential precedence, concurrent OAuth refresh,
  rotation persistence, cancellation, timeout, and redaction;
- arbitrary UTF-8 and event fragmentation, CR/LF/CRLF, multiline SSE, comments,
  unknown events, residual EOF, missing terminal, and post-terminal data;
- text, reasoning, media, tool-call, tool-result, structured-output, deferred,
  finish, error, candidate, and usage normalization for every declared
  capability;
- canonical-to-wire tool-ID bijection, collision handling, mixed content-part
  positions, orphan repair, and cross-provider reasoning replay;
- exact request snapshots for role, token field, tool adjacency, schema dialect,
  cache controls, retention, affinity, and route-specific headers;
- retry classification, hint parsing, bounded interruptible wait, and proof that
  only the resilience owner retries;
- context-overflow positive and negative cases, including silent-success
  heuristics;
- fallback before first visible output and refusal to splice after progress; and
- raw provider evidence retention with body limits, hashes, request IDs, and
  redaction.

Undocumented profiles are opt-in, clearly labelled, isolated from public
provider packages, and removable without changing provider-neutral contracts.

## Current public references

- [OpenAI Responses](https://platform.openai.com/docs/api-reference/responses)
- [Anthropic extended thinking](https://platform.claude.com/docs/en/docs/build-with-claude/extended-thinking)
- [Anthropic fine-grained tool streaming](https://platform.claude.com/docs/en/docs/agents-and-tools/tool-use/fine-grained-tool-streaming)
- [Gemini thought signatures](https://ai.google.dev/gemini-api/docs/generate-content/thought-signatures)
- [Amazon Bedrock prompt caching](https://docs.aws.amazon.com/bedrock/latest/userguide/prompt-caching.html)
- [Mistral API](https://docs.mistral.ai/api)
- [Cloudflare AI Gateway REST APIs](https://developers.cloudflare.com/ai-gateway/usage/rest-api/)
- [OpenRouter models](https://openrouter.ai/docs/guides/overview/models)
- [OpenRouter app attribution headers](https://openrouter.ai/docs/app-attribution)
- [OpenRouter reasoning tokens](https://openrouter.ai/docs/guides/best-practices/reasoning-tokens)
- [OpenRouter image generation](https://openrouter.ai/docs/guides/overview/multimodal/image-generation)

## Related specifications

- [Provider API index](../../providers/index.md)
- [Model providers and capabilities](../../concepts/model-providers-and-capabilities.md)
- [Provider request pipeline](../../concepts/provider-request-pipeline.md)
- [History validation and repair](../../concepts/history-validation-and-repair.md)
- [Coding harness execution profile](coding-harness-execution-profile.md)
