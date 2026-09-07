# Provider request pipeline

**Status:** Normative  
**Depends on:** [Model capabilities](model-providers-and-capabilities.md),
[context assembly](context-assembly-and-instructions.md),
[streaming](streaming-and-event-protocol.md)

## Purpose

The request pipeline turns an approved working context into one provider call.
It makes translation, transport, authentication, parsing, and diagnostics
independently testable.

## Stages

```text
validate capabilities
  -> translate provider-neutral request
  -> apply bounded provider options
  -> inject credentials and required headers
  -> run typed request hooks
  -> authorize destination and data egress
  -> send transport request
  -> validate status and response framing
  -> parse typed stream
  -> normalize terminal response or error
```

Each stage MUST accept cancellation. No stage after credential injection may log
or expose raw secrets.

Lazy SDK or transport loading, credential refresh, endpoint materialization, and
request setup are part of the attempt and share its effect-start gate. A setup
failure becomes a typed stream/result failure with no fake response-start event.

The first-party catalog, selection, capability validation, and model request
execution live in `AgentKit.Providers`. Translation, authentication, transport,
parsing, and concrete errors live in the selected
`AgentKit.Providers.<ProviderName>` package. Shared OpenAI-compatible machinery
may live in `AgentKit.Providers.OpenAICompatible`, but the concrete package
remains responsible for its compatibility profile and observable behavior.

## Request contract

The provider-neutral request MUST include:

- provider/model descriptor and API family;
- captured service-surface, endpoint-profile, and credential-profile identities
  and versions;
- the complete immutable `ExecutionIdentity`, its matching
  `SecurityAuthorizationContext`, and typed operation correlation;
- ordered context messages and instruction sources;
- tool definitions, tool-choice policy, and output schema;
- sampling, output, cache, and reasoning settings;
- the requested response-candidate count;
- run/request IDs and safe metadata;
- deadline and attempt number; and
- explicitly scoped provider extension options.

Output token limits MUST be clamped or rejected against the model context and
output ceilings after reserving input estimate and a safety margin. Any clamp
MUST be visible in diagnostics.

Cache retention and session affinity are independent settings. Disabling cache
retention removes cache keys and affinity headers only where the concrete
profile defines that behavior. A session identifier is never sent merely because
AgentKit has a `SessionId`; egress requires an explicit classified mapping.

## Translation

Translation MUST preserve role, part order, tool-call IDs, media, reasoning
metadata, and continuation identifiers when supported. A downgrade MUST be
driven by the model compatibility profile and recorded.

Translation SHOULD produce a provider request DTO independent of the transport
so serialization can be fixture-tested. Vendor SDK types stay inside the leaf
adapter.

Translation cardinality is explicit. One neutral message MAY become several wire
messages, and several adjacent neutral parts MAY be coalesced only when the
compatibility profile proves the mapping loss-aware. The translation manifest
retains source message/part order and tool-call correlation so diagnostics and
responses can be mapped back. A protocol that requires one tool result per wire
message therefore expands a multi-result neutral `ToolMessage`; it never drops
all but the first result.

Only the bounded `ToolResultPart` projection enters provider translation; the
adapter never reconstructs the authoritative `ToolCallResult`, retries the
effect, or infers outcome from display text. It preserves the requested alias,
resolved identity/version when available, exact source status, uncertainty, and
projection-loss markers. A protocol without a status field uses the profile's
deterministic bounded status envelope or rejects the mapping.

Role fallback preserves trust as well as content. Runtime/synthetic notices,
retrieved text, and tool output MUST NOT be promoted to system or developer
authority merely because a provider lacks their native role. The profile must
use a non-elevating representation or reject the request before I/O.

Wire normalization MUST remove unpaired Unicode surrogates or reject them before
JSON serialization while preserving valid pairs. Incremental tool JSON is kept
as raw ordered fragments until the provider terminal boundary, then parsed and
schema-validated by the tool lifecycle. Provider-specific empty-content,
adjacent-tool-result, and reasoning-signature rules belong to the compatibility
profile, not scattered conditionals in the loop.

Canonical provider call identity is distinct from a constrained target wire ID.
When the destination imposes length or character rules, translation creates one
deterministic collision-safe request-scoped bijection and applies it to calls,
results, and stream correlation. Normalization is reversible in the translation
manifest; independently hashing each occurrence or using array position is
forbidden.

## Options and headers

Core settings MUST remain typed. Provider-specific options MAY use a validated
extension object owned by that adapter. Unknown options MUST fail validation or
be explicitly passed through; silent misspelling is forbidden.

Every payload extension key has an owning provider/profile namespace, content
classification, merge operation, and allowed source layers. The effective order
follows the configuration manifest; duplicate keys are replaced or rejected
according to that key's declared rule and never resolved by incidental
dictionary insertion order. Canonical request fields, identities, destination,
authentication, security, budgets, tool correlation, and protocol framing are
reserved. An extension that attempts to set or shadow one is rejected with a
safe diagnostic before credential resolution or network access rather than
silently ignored.

Header policy MUST define precedence among adapter defaults, authentication,
host configuration, run extensions, and trusted hooks. Sensitive or protocol-
critical headers MUST NOT be replaceable by untrusted input. An explicit
suppression value MAY remove an optional default; absence means inherit.

Header names compare case-insensitively even when the options representation is
a dictionary. A later override replaces differently cased earlier spellings
rather than sending duplicates. Signature-sensitive transports such as AWS
reject or ignore caller overrides for `Authorization`, `Host`, and signing
headers before signing. A custom HTTP transport/fetch option does not silently
claim to affect WebSocket or SDK-owned transports.

## Hooks

Request hooks MAY inspect or replace the translated payload, add approved
headers, select transport details, and observe response headers. It MUST run in
documented order and MUST NOT receive raw credentials unless its contract is an
explicit trusted authentication component.

Payload replacement MUST be revalidated for size and protected fields. Every
replacement should emit a safe hook identity in diagnostics. A replacement that
changes destination, classified content, or another security input requires a
fresh security decision.

Arbitrary sampling pass-through, when supported for local or compatible servers,
is scoped to an explicit API-family profile and merged at a documented
precedence. Other adapters reject or ignore it observably; it is not a universal
request bag.

## Transport

Transport configuration MUST bound connect, response-header, idle-stream, and
overall deadlines where supported. It MUST bound headers, frames, response
bytes, redirects, and decompression expansion. Redirect policy MUST prevent
credential forwarding to an unintended origin.

The transport SHOULD expose provider request IDs, rate-limit headers, retry
hints, and status without coupling the core to an HTTP client type.

Before network activity, the adapter submits the canonical destination and data
classification to the shared security authority. AgentKit.Network validates the
bounded grant again across DNS, connection, redirects, and upload. Provider
credentials remain excluded from the request and audit.

The adapter selects the authority named by the captured authorization context
through `ISecurityAuthoritySelector`; it MUST NOT inject an unkeyed authority or
resolve keyed services itself. Each attempt obtains and atomically consumes a
fresh provider-egress grant bound to the execution identity, destination,
classified payload, provider/model revision, and attempt fingerprint. The
network layer obtains a separate lower-boundary grant. Neither grant may be
reused for a retry, fallback, changed payload, redirect, or other effect.

## Retry ownership

The adapter performs one logical attempt by default. A resilience layer above it
owns provider retries so attempt count, budget, fallback, and observability are
consistent. A transport MAY retry an operation only when the contract proves it
was not observably sent or the provider supplies an idempotency mechanism.

Streaming attempts MUST NOT be retried transparently after visible output has
been delivered. The loop decides whether to preserve partial output, repair
history, and make a new request.

Conversational, embedding, and reranking execution use the immutable descriptor
and catalog version returned by selection; an executor MUST NOT re-read a newer
live catalog mid-operation. Embedding and reranking calls carry an
invocation-only budget capability and optional hook lease. Their executors
reserve before every attempt, settle actual usage exactly once, and dispatch
hooks through the configured dispatcher even when the operation runs outside an
agent run.

Retry delay parsing accepts the provider's documented seconds/date and
millisecond headers, clamps invalid negative values, and enforces a configured
maximum wait. A server-requested delay beyond that bound returns control to the
higher resilience layer instead of parking an invisible SDK timer. Any
transport/SDK retry is disabled or surfaced so total attempts are accounted
once.

## Authentication

Every operation registration binds a keyed, versioned credential profile to a
keyed, versioned endpoint profile. Credentials enter through that captured
profile at send time; there is no unkeyed process-global credential source whose
meaning changes with registration order. Resolution receives the provider,
service surface, endpoint/audience, account profile, operation, attempt,
execution identity, and deadline.

The operation descriptor retains both profile references. At each attempt, a
narrow profile runtime selector returns an owned lease over the exact
version-retained, secret-free snapshots and matching credential source. A
replacement affects only later selections; resolution MUST NOT fall forward to a
newer profile or another account while an operation is in flight.

Credential access is itself a protected effect. Before resolution, the adapter
obtains a separate bounded grant tied to the profile revision, source, account,
audience, execution identity, attempt, and deadline. The source revalidates that
grant immediately before returning an opaque disposable credential lease; raw
secret material is never a public value. Sending uses a different
provider-egress grant, and the network transport enforces its own grant again.

Credential refresh, expiry skew, rotation, authentication support, scheme,
audience, and scope selection belong to the concrete leaf integration. A shared
wire-family package MAY supply opaque-token retrieval and header-construction
mechanics, but it does not claim that every compatible provider supports the
same authentication modes. Expired or near-expiry material is refreshed before
I/O or returns a typed authentication failure. Credentials MUST NOT be stored in
model descriptors, messages, configuration snapshots, events, exception text,
replay logs, profile keys, or diagnostics.

The compatibility profile also declares candidate multiplicity and usage
reporting. A single-candidate operation requests and accepts exactly one
candidate; extra choices are a protocol failure, never silently discarded. When
a successful response omits usage, the terminal aggregate records usage as not
reported, emits no fabricated usage update, and leaves budget reconciliation to
the configured usage policy. Not reported is distinct from a provider-reported
zero.

Credential precedence is explicit: a trusted per-request override, then stored
provider credential, then ambient provider-specific sources when no stored
credential owns the provider. A failed or incompatible stored credential MUST
NOT silently fall back to environment credentials. OAuth refresh uses a
provider-scoped single-flight/double-check boundary, persists rotated
credentials before release, has its own timeout, and leaves the prior credential
available for an explicit retry or re-login decision.

## Acceptance scenarios

- Request serialization fixtures cover every declared capability combination.
- Credential values never appear in logs, snapshots, or errors.
- An unsafe redirect cannot forward authorization to another origin.
- Stream truncation returns protocol failure with partial diagnostics, not
  completion.
- A settings clamp is deterministic and observable.
- Provider-specific options cannot mutate protected identity or auth fields.
- One neutral tool-result message can expand to several wire messages without
  losing source order or call correlation.
- A runtime notice is rejected or mapped without gaining system/developer
  authority.
- Two operations using different endpoint or account profiles resolve only their
  captured credentials.
- Unexpected extra candidates and absent usage are handled explicitly rather
  than by selecting index zero or reporting zero consumption.
- Differently cased header overrides yield one effective header.
- Provider replacement or request cancellation fences a late credential/catalog
  publication.
- A server retry hint beyond policy returns a typed retry decision without an
  unbounded sleep.
- Unpaired surrogate input fails or sanitizes before transport while valid emoji
  survives unchanged.
- A constrained wire tool ID maps back to one canonical call ID across request,
  stream events, and tool results without collision.

## Related specifications

- [Cancellation, timeouts, and resilience](cancellation-timeouts-and-resilience.md)
- [Extensions, hooks, and middleware](extensions-hooks-and-middleware.md)
- [Permissions, approvals, and trust](permissions-approvals-and-trust.md)
- [Observability and audit](observability-and-audit.md)
- [Coding-harness provider profiles](../providers/coding-harness-provider-profiles.md)
