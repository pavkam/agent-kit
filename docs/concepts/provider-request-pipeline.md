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
  -> run request middleware
  -> send transport request
  -> validate status and response framing
  -> parse typed stream
  -> normalize terminal response or error
```

Each stage MUST accept cancellation. No stage after credential injection may log
or expose raw secrets.

## Request contract

The provider-neutral request MUST include:

- provider/model descriptor and API family;
- ordered context messages and instruction sources;
- tool definitions, tool-choice policy, and output schema;
- sampling, output, cache, and reasoning settings;
- run/request IDs and safe metadata;
- deadline and attempt number; and
- explicitly scoped provider extension options.

Output token limits MUST be clamped or rejected against the model context and
output ceilings after reserving input estimate and a safety margin. Any clamp
MUST be visible in diagnostics.

## Translation

Translation MUST preserve role, part order, tool-call IDs, media, reasoning
metadata, and continuation identifiers when supported. A downgrade MUST be
driven by the model compatibility profile and recorded.

Translation SHOULD produce a provider request DTO independent of the transport
so serialization can be fixture-tested. Vendor SDK types stay inside the leaf
adapter.

## Options and headers

Core settings MUST remain typed. Provider-specific options MAY use a validated
extension object owned by that adapter. Unknown options MUST fail validation or
be explicitly passed through; silent misspelling is forbidden.

Header policy MUST define precedence among adapter defaults, authentication,
host configuration, run extensions, and middleware. Sensitive or protocol-
critical headers MUST NOT be replaceable by untrusted input. An explicit
suppression value MAY remove an optional default; absence means inherit.

## Middleware

Request middleware MAY inspect or replace the translated payload, add approved
headers, select transport details, and observe response headers. It MUST run in
documented order and MUST NOT receive raw credentials unless its contract is an
explicit trusted authentication component.

Payload replacement MUST be revalidated for size and protected fields. Every
replacement should emit a safe middleware identity in diagnostics.

## Transport

Transport configuration MUST bound connect, response-header, idle-stream, and
overall deadlines where supported. It MUST bound headers, frames, response
bytes, redirects, and decompression expansion. Redirect policy MUST prevent
credential forwarding to an unintended origin.

The transport SHOULD expose provider request IDs, rate-limit headers, retry
hints, and status without coupling the core to an HTTP client type.

## Retry ownership

The adapter performs one logical attempt by default. A resilience layer above it
owns provider retries so attempt count, budget, fallback, and observability are
consistent. A transport MAY retry an operation only when the contract proves it
was not observably sent or the provider supplies an idempotency mechanism.

Streaming attempts MUST NOT be retried transparently after visible output has
been delivered. The loop decides whether to preserve partial output, repair
history, and make a new request.

## Authentication

Credentials enter through an explicit credential provider at send time. They
MUST NOT be stored in model descriptors, messages, configuration snapshots,
events, exception text, or replay logs. Credential refresh and audience/scope
selection belong to the leaf integration.

## Acceptance scenarios

- Request serialization fixtures cover every declared capability combination.
- Credential values never appear in logs, snapshots, or errors.
- An unsafe redirect cannot forward authorization to another origin.
- Stream truncation returns protocol failure with partial diagnostics, not
  completion.
- A settings clamp is deterministic and observable.
- Provider-specific options cannot mutate protected identity or auth fields.

## Upstream evidence

- Pi's request options include headers, payload/response hooks, retries,
  timeouts, transport, cache retention, and session affinity in
  [`packages/ai/src/types.ts`](https://github.com/badlogic/pi-mono/blob/9767ba275f3e9a5ee0f5c5342249b629ab1b2282/packages/ai/src/types.ts).
- OpenCode's provider construction and wire-specific plugins live under
  [`packages/core/src/plugin/provider`](https://github.com/anomalyco/opencode/tree/337fd144d2ba144743368f78d9579a99cce175bd/packages/core/src/plugin/provider).

## Related specifications

- [Cancellation, timeouts, and resilience](cancellation-timeouts-and-resilience.md)
- [Extensions, hooks, and middleware](extensions-hooks-and-middleware.md)
- [Observability and audit](observability-and-audit.md)
