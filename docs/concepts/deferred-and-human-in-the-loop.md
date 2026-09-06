# Deferred operations and human-in-the-loop

**Status:** Normative  
**Depends on:** [Permissions and approvals](permissions-approvals-and-trust.md),
[sessions](sessions-persistence-and-branching.md)

## Purpose

Deferral extends the [tool-call lifecycle](tool-call-lifecycle.md) across a
[settled run](run-lifecycle-and-settlement.md) by storing enough durable
correlation for a later authorized continuation.

Any protected operation can require later approval, external work, or
asynchronous completion. Deferral suspends that causal operation without holding
an in-memory task or pretending failure. Tool calls are one use case; file,
network, process, memory, session, model-egress, MCP, and delegation operations
use the same lifecycle.

## Deferral kinds

- **Approval required:** execution waits for an authorized human or policy
  response.
- **Operation deferred:** an external actor or durable worker will perform it.
- **Result pending:** execution started, but the result will arrive later.
- **Provider suspended:** the provider returned a resumable continuation handle.

Each kind MUST state whether side effects have started and what evidence is
required to resume.

## Deferred request

```csharp
public sealed record DeferredOperationRequest(
    DeferredRequestId Id,
    SessionId SessionId,
    RunId RunId,
    OperationId OperationId,
    DeferralKind Kind,
    ProtectedOperation Operation,
    InputFingerprint InputFingerprint,
    SecurityDecisionReference SecurityDecision,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    SessionVersion SessionVersion,
    ExtensionData Extensions);
```

Inputs and resources MUST be canonicalized and validated before deferral.
Sensitive values require encrypted authorized storage and redacted presentation.
A deferred request is durable; the runtime MUST NOT depend on a captured
closure, service scope, task, or cancellation source to resume it.

## Run outcome

When no configured handler can resolve deferral inline, the run returns a typed
`Deferred` outcome containing safe request summaries and durable IDs. The run
settles; it does not remain “active” forever.

An inline handler MAY resolve within the same run. It still emits a deferred-
request event before handling and a resolution event afterward so behavior is
observable and equivalent to the external path.

## Resolution admission

A resolution is new authenticated input containing request ID, operation ID,
resolution kind, approval/result payload, resolver identity, timestamp, and an
idempotency key. The session executor MUST:

1. load the exact unresolved request;
2. verify session/tenant ownership, expiry, version, and unresolved state;
3. validate resolution schema and correlation;
4. revalidate resources, inputs, and security authority when required;
5. atomically commit one resolution; and
6. wake a continuation run.

Repeated equivalent resolution returns the existing receipt. Conflicting or late
resolution fails typed and cannot overwrite the first.

## Approval resolution

Approval by itself is not an operation result. After approval, the runtime
revalidates the exact operation and executes it through its normal
record/effect/result pipeline. A tool call uses the tool pipeline; another
component uses its own typed lifecycle. Denial creates a terminal denied result
or policy halt.

If a human edits an input or resource, the edit forms a new fingerprint and MUST
undergo domain validation and fresh security evaluation. The original approval
request cannot authorize changed or expanded scope.

## Externally executed result

An external result MUST include operation/request identity, terminal status,
bounded content, side-effect certainty, safe error details, and producer
attestation appropriate to the host. Tool results also retain tool-call
identity. The runtime validates the result before committing it.

The external worker MUST use an idempotency key for mutating calls. AgentKit
must not retry an ambiguous externally started effect.

## Cancellation and expiry

Cancelling the original run does not delete a durable deferred request unless
policy explicitly cancels the external operation. Expiry appends a terminal
expired resolution and makes later responses stale. Revocation and external
cancellation are separate audited transitions.

## Acceptance scenarios

- A process restart can list and resolve outstanding requests.
- Equivalent duplicate resolution is idempotent; conflicting resolution fails.
- Edited inputs or resources require new validation and security evaluation.
- Inline and external handlers emit the same semantic event sequence.
- Expired approval cannot start execution.
- A deferred run settles and later resolution starts a causally linked run.

## Upstream evidence

- Pydantic AI models approval and call deferral, deferred request/result events,
  and later correlated resolution in
  [deferred tools](https://ai.pydantic.dev/deferred-tools/) and
  [`_deferred.py`](https://github.com/pydantic/pydantic-ai/blob/c0e4d824eaa0401d4481d401e5b3894ab32ab59d/pydantic_ai_slim/pydantic_ai/_deferred.py).
- Pi's AI types include a deferred response handle in
  [`types.ts`](https://github.com/badlogic/pi-mono/blob/9767ba275f3e9a5ee0f5c5342249b629ab1b2282/packages/ai/src/types.ts).

## Related specifications

- [Durable execution and recovery](durable-execution-and-recovery.md)
- [Input admission and message queues](input-admission-and-message-queues.md)
- [Error taxonomy](error-taxonomy.md)
