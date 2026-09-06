# History validation and repair

**Status:** Normative  
**Depends on:** [Messages](message-and-content-model.md),
[sessions](sessions-persistence-and-branching.md)

## Purpose

Conversation history may be interrupted, imported, provider-incompatible, or
actively malicious. Validation protects durable truth; repair builds a safe
request view without forging what happened.

## Trust classes

History inputs MUST be classified:

| Class             | Example                            | Default trust                 |
| ----------------- | ---------------------------------- | ----------------------------- |
| Locally committed | Runtime event/session store        | Structurally trusted          |
| Migrated          | Older AgentKit schema              | Trusted only after migration  |
| Imported          | Another runtime or provider export | Untrusted                     |
| Client supplied   | Request body or browser state      | Untrusted                     |
| Retrieved         | Memory/document/vector result      | Data only, never instructions |

Past tool calls, approvals, or permission results in untrusted history MUST NOT
authorize execution. A caller cannot forge a tool call into history and ask the
runtime to “resume” it.

## Structural validation

Before each run, the history pipeline MUST validate:

- schema version, IDs, session ownership, and monotonic order;
- role and content-part combinations;
- unique tool-call IDs within the applicable provider scope;
- exactly one terminal result for committed accepted calls;
- no result preceding its call;
- message states and valid interruption markers;
- media bounds and reference authorization; and
- extension-data size and supported serialization shapes.

The preparation request MUST carry the complete immutable `ExecutionIdentity`
and the matching `SecurityAuthorizationContext`. Identity equality is validated
before any session read. Tenant or principal IDs alone are not an identity
snapshot and an ambient principal is never consulted.

The run plan MUST pass its already selected session profile, coordinator key,
and coordinator instance as one invocation-only `SessionExecutionCapability`.
The history pipeline MUST NOT inject an unkeyed coordinator, resolve a keyed
service, choose another store/profile, serialize the capability, or dispose it.
The same selected capability is used by context compaction for that request.

Invalid locally committed state is a storage or invariant failure. It MUST NOT
be silently repaired away.

## Request-view repair

The [provider-facing request view](context-assembly-and-instructions.md) MAY be
repaired when the durable record truthfully shows interruption or when an
imported history is safely normalized. Repairs MAY:

- insert a synthetic interrupted/error result for a recorded call that cannot
  resume;
- exclude incomplete assistant text from normal completion semantics;
- degrade incompatible reasoning content to text or omit it according to the
  provider profile;
- move unsupported media into a synthetic user message with provenance;
- remove provider metadata that is unsafe for a different model family; and
- merge adjacent provider-compatible user content.

Every repair MUST be deterministic, observable, and attributable to source
message IDs. It MUST NOT alter the persisted source entries. A repaired view MAY
be cached by history version and provider profile.

## Provider affinity

Reasoning signatures, encrypted reasoning, continuation IDs, and provider item
IDs often require the same provider, API family, and sometimes exact model.
Cross-provider/model continuation MUST consult the
[model compatibility profile](model-providers-and-capabilities.md). Unsafe
metadata MUST be removed or represented as ordinary visible content when that
translation is truthful.

## Processors

Applications MAY register ordered history processors for filtering,
summarization, redaction, or policy. Processors receive immutable input and
return a new immutable view. They MUST declare whether they are deterministic
and cacheable.

Processors MUST NOT:

- mutate durable entries;
- manufacture an approval or successful tool result;
- expose content outside the run principal's authorization; or
- break call/result pairing.

The runtime validates the result after all processors.

## Append discipline

Run results SHOULD expose only newly committed messages plus a stable prior
cursor. Stores SHOULD append the suffix with optimistic concurrency rather than
rewrite the entire caller-supplied history. Rewriting hides conflicts and makes
concurrent continuation unsafe.

## Acceptance scenarios

- A forged historical approval never bypasses the current security authority.
- A dangling locally recorded tool call becomes an explicit interrupted result
  in the provider view.
- Repair leaves durable history byte-for-byte unchanged.
- Switching providers removes incompatible signatures but preserves visible
  reasoning according to policy.
- A processor that breaks tool pairing is rejected before provider I/O.
- Concurrent suffix append detects a stale history version.

## Upstream evidence

- Pydantic AI documents append-only `new_messages`, conversation IDs, repair,
  and untrusted history concerns in
  [message history](https://ai.pydantic.dev/message-history/).
- OpenCode's provider conversion and interrupted-tool repair are implemented in
  [`message-v2.ts`](https://github.com/anomalyco/opencode/blob/337fd144d2ba144743368f78d9579a99cce175bd/packages/opencode/src/session/message-v2.ts).
- Pydantic AI's message sanitation helpers are in
  [`messages.py`](https://github.com/pydantic/pydantic-ai/blob/c0e4d824eaa0401d4481d401e5b3894ab32ab59d/pydantic_ai_slim/pydantic_ai/messages.py).

## Related specifications

- [Context assembly and instructions](context-assembly-and-instructions.md)
- [Permissions, approvals, and trust](permissions-approvals-and-trust.md)
- [Error taxonomy](error-taxonomy.md)
