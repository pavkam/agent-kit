# Message and content model

**Status:** Normative  
**Depends on:** [Design principles](design-principles.md)

## Purpose

Messages are the durable, provider-neutral
[session record](sessions-persistence-and-branching.md). They are immutable
envelopes containing ordered typed parts, not role-plus-string DTOs.

## Typed identities

Every domain identity is a dedicated immutable value type. The canonical shared
identity and `IIdentifierGenerator<TIdentifier>` shapes are defined once in the
[composition architecture](../architecture/composition-and-configuration.md#typed-identity);
each named type lives in its own matching source file.

Other domains define the same kind of value for provider, model, approval, goal,
delegation, durable-operation, and storage identities. Public contracts MUST NOT
substitute the underlying string, GUID, integer, or provider-supplied
identifier. The default value is invalid at public boundaries; construction,
parsing, and deserialization validate the representation and preserve a stable
canonical text form.

Framework-created identities use an injected, thread-safe
`IIdentifierGenerator<TIdentifier>`. First-party generators provide
collision-resistant defaults; deterministic tests and replay replace them.
Provider-supplied IDs remain typed external correlation values and never replace
AgentKit identity.

## Envelope

The canonical public `AgentMessage` envelope and its concrete message variants
are defined once in the
[message architecture](../architecture/messages-and-history.md#normative-minimal-message-shape).
This concept specifies their durable semantics rather than redeclaring a second
API shape.

Concrete message kinds MUST include user request, assistant response, tool
result, system/context record, and synthetic runtime message. Compaction and
configuration-change records SHOULD be distinct session entries rather than
pretend conversational messages.

`MessageState` MUST distinguish at least complete, incomplete, suspended, and
interrupted. Only complete messages are normally sent to a provider; repair
policy decides how other states are represented.

## Roles and instruction semantics

Provider-neutral roles MUST represent system, developer, user, assistant, tool,
and runtime/synthetic semantics without assuming every provider supports each
role. [Adapter capability profiles](model-providers-and-capabilities.md) MUST
define a loss-aware translation or reject an unsupported mapping before sending
the request.

Instructions are structured sources with provenance and precedence. The core
MUST NOT flatten system, developer, retrieved, and user content into one string
before the provider adapter can translate them correctly.

## Content parts

The portable model MUST support these part families:

- text and instruction text;
- image, audio, file, and opaque binary references;
- reasoning/thinking, including redacted or signed provider forms;
- citation and source reference;
- application tool call and provider-native tool call;
- application tool result and provider-native result;
- structured data and structured-output result;
- compaction/summary marker;
- refusal, safety, and diagnostic information; and
- unknown provider extension content.

Parts MUST preserve source order. Unknown parts MUST round-trip through durable
storage and compatible provider continuation paths even when the core cannot
interpret them.

## Assistant response metadata

An assistant response SHOULD retain:

- provider, API family, requested and actual model identity;
- provider response, item, and continuation IDs;
- normalized and raw finish reasons;
- input, output, cached, and reasoning token usage when available;
- provider-computed cost or local cost estimate with provenance;
- reasoning signatures and diagnostics;
- deferred/suspended continuation handle; and
- partial response details when failed or interrupted.

The normalized stop reason MUST include pending, completed, length, tool use,
error, cancelled, and deferred. Provider-specific values remain in extension
data.

## Tool correlation

Every tool call has a stable `ToolCallId`, tool identity/name, raw argument
representation, and optional validated argument value. The
[tool-call lifecycle](tool-call-lifecycle.md) requires a result to carry the
same call ID and identify the resolved tool/version. There MUST be exactly one
terminal result per accepted call.

Provider call IDs MUST be preserved. If a provider supplies no usable ID, the
adapter MAY generate one and record that provenance.

## Data ownership

Byte content SHOULD be represented by immutable owned memory, a bounded stream
factory, or an authorized object reference. APIs MUST document who disposes or
retains buffers. Raw external URLs MUST NOT be fetched merely because a model
included them.

`ExtensionData` MUST use immutable JSON-compatible values or typed provider
extensions. It MUST NOT contain live SDK objects, streams, credentials, or
exceptions intended for serialization.

## Equality and serialization

Identity equality uses IDs. Semantic equality for idempotent admission MUST use
a canonical representation that excludes mutable delivery metadata but includes
every instruction- or execution-relevant field.

Serialized messages MUST carry a schema version. Readers MUST reject unsupported
major versions, preserve unknown fields when round-tripping, and migrate older
versions explicitly.

## Acceptance scenarios

- Mixed text, image, and tool parts retain exact order after storage round-trip.
- Unknown provider parts survive load/save without becoming strings.
- A reasoning signature is reused only on a compatible continuation path.
- Tool call and result remain correlated through adapter translation and replay.
- An interrupted assistant message cannot be mistaken for successful output.
- Canonical equality detects a duplicate ID with materially different content.

## Upstream evidence

- Pi's provider-neutral message and stream types are in
  [`packages/ai/src/types.ts`](https://github.com/badlogic/pi-mono/blob/9767ba275f3e9a5ee0f5c5342249b629ab1b2282/packages/ai/src/types.ts).
- OpenCode V2 models tagged message and tool-state variants in
  [`session-message.ts`](https://github.com/anomalyco/opencode/blob/337fd144d2ba144743368f78d9579a99cce175bd/packages/schema/src/session-message.ts).
- Pydantic AI's broader request/response part taxonomy and message states are in
  [`messages.py`](https://github.com/pydantic/pydantic-ai/blob/c0e4d824eaa0401d4481d401e5b3894ab32ab59d/pydantic_ai_slim/pydantic_ai/messages.py).

## Related specifications

- [Streaming and event protocol](streaming-and-event-protocol.md)
- [History validation and repair](history-validation-and-repair.md)
- [Model providers and capabilities](model-providers-and-capabilities.md)
