# History validation and repair

**Status:** Normative

**Architecture:**
[Messages and history](../architecture/messages-and-history.md),
[Context](../architecture/context.md)

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
- exactly one authoritative terminal result and one correlated message
  projection for every locally committed bounded, identified call, with an
  accepted record before any admitted invocation;
- agreement among requested alias, optional resolved tool/version, terminal
  status, side-effect certainty, and captured result-projection policy version;
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

- project a recorded interrupted/error terminal result for a local call that
  recovery has already settled, or add a clearly non-authoritative provider-view
  placeholder for an imported orphaned call;
- exclude incomplete assistant text from normal completion semantics;
- degrade incompatible reasoning content to text or omit it according to the
  provider profile;
- move unsupported media into a synthetic user message with provenance;
- remove provider metadata that is unsafe for a different model family; and
- merge adjacent provider-compatible user content.

The default cross-provider repair applies these details in order:

1. normalize imported null/undefined content to the canonical empty-content
   representation without rewriting durable history;
2. apply capability-driven media handling, using an attributable placeholder or
   artifact reference when omission is the only truthful downgrade;
3. for assistant content, retain opaque/redacted reasoning and replay signatures
   only under their declared provider/API/model affinity; convert visible
   incompatible reasoning to ordinary text when allowed, and otherwise omit it
   with a diagnostic;
4. remove provider-bound thought signatures from cross-model tool calls;
5. normalize overlong or illegal tool-call IDs only through a deterministic,
   collision-safe mapping, and apply the same mapping to every correlated
   result; and
6. before the next assistant/user boundary or end of history, require local
   accepted orphans to pass through tool/durability recovery and terminal
   recording before projection; for imported orphans only, add an explicit
   non-authoritative provider-view error with source provenance.

Errored, aborted, deferred, and unknown-outcome assistant attempts are omitted
from an ordinary provider replay unless a compatibility profile defines a safe
interrupted representation. Genuine output-length responses remain eligible when
their parts and tool correlation are complete.

Every repair MUST be deterministic, observable, and attributable to source
message IDs. It MUST NOT alter the persisted source entries. A repaired view MAY
be cached by history version and provider profile.

Synthetic repair notices and runtime records remain operational evidence. A
provider compatibility projection MUST represent them as tagged
non-instruction-bearing content or reject the request; repair never promotes
them to system/developer instruction precedence.

## Provider affinity

Reasoning signatures, encrypted reasoning, continuation IDs, and provider item
IDs often require the same provider, API family, and sometimes exact model.
Cross-provider/model continuation MUST consult the
[model compatibility profile](model-providers-and-capabilities.md). Unsafe
metadata MUST be removed or represented as ordinary visible content when that
translation is truthful.

Affinity comparison uses actual response provider, API family, and model
identity, not merely the selected alias. Tool-call identity restrictions such as
character set and maximum length are also destination-profile capabilities;
normalizing them must preserve a reversible diagnostic mapping even when the
wire value cannot preserve the original spelling.

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
- A dangling locally accepted tool call is terminally settled by recovery, then
  projected into the provider view without another invocation.
- Repair leaves durable history byte-for-byte unchanged.
- Switching providers removes incompatible signatures but preserves visible
  reasoning according to policy.
- A processor that breaks tool pairing is rejected before provider I/O.
- Concurrent suffix append detects a stale history version.
- Cross-model conversion removes opaque reasoning and thought signatures while
  preserving visible content according to policy.
- Tool-call ID normalization updates every result and detects collisions.
- Consecutive imported orphaned calls receive one correlated, explicitly
  non-authoritative provider-view result each before the next conversational
  boundary.

## Related specifications

- [Context assembly and instructions](context-assembly-and-instructions.md)
- [Permissions, approvals, and trust](permissions-approvals-and-trust.md)
- [Error taxonomy](error-taxonomy.md)
