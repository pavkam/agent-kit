# Deferred operations and human-in-the-loop

**Status:** Normative

**Architecture:**
[Security and human control](../architecture/permissions-and-human-control.md),
[Sessions](../architecture/sessions.md)

**Depends on:** [Permissions and approvals](permissions-approvals-and-trust.md),
[sessions](sessions-persistence-and-branching.md)

## Purpose

Deferral stores enough durable correlation for a later authorized continuation.
It has two distinct ownership modes: an external or human-in-the-loop handoff
may settle the current [run](run-lifecycle-and-settlement.md) and later start a
causally linked run, while an operation-owned retry or provider suspension keeps
the original operation open and yields a typed wait.

Any protected operation can require later approval, external work, or
asynchronous completion. Deferral preserves the causal continuation without
holding an in-memory task or pretending failure: it either suspends the same
durable operation or hands ownership to an external workflow. Tool calls are one
use case; file, network, process, memory, session, model-egress, MCP, and
delegation operations use the same lifecycle.

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
namespace AgentKit;

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

## Run outcome and ownership

The deferral kind MUST declare who owns the continuation and whether the current
operation remains open. When an external worker or human-resolution workflow
accepts a durable handoff, the current run MAY return a typed terminal
`Deferred` outcome containing safe request summaries and durable IDs, settle,
and later admit the resolution as a causally linked run.

When the runtime retains ownership of the original operation, including a
provider-suspended response or durable retry delay, a drive returns a typed
nonterminal `Waiting` outcome. The operation remains installed; it emits neither
`RunCompleted` nor `RunSettled`, and only an explicit wake, poll, or later drive
may advance it.

An inline handler MAY resolve within the same run. It still emits a deferred-
request event before handling and a resolution event afterward so behavior is
observable and equivalent to the external path.

## Resolution admission

This section applies when a durable external or human-resolution request owns
the continuation. An operation-owned provider suspension instead resumes through
the explicit poll/drive path described below.

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

After an external handoff settles the original run, cancelling a later caller or
observer does not delete the durable deferred request. Policy must explicitly
cancel the external operation. Expiry appends a terminal expired resolution and
makes later responses stale. Revocation and external cancellation are separate
audited transitions.

A provider-deferred handle is nonempty and bound to provider, account, model,
API family, original request, and operation identity. Suspension leaves that
operation open and returns a typed wait; resumption is an explicit status poll
or drive. A hidden process timer is not durable ownership, and host restart does
not turn waiting into settlement.

## Acceptance scenarios

- A process restart can list and resolve outstanding requests.
- Equivalent duplicate resolution is idempotent; conflicting resolution fails.
- Edited inputs or resources require new validation and security evaluation.
- Inline and external handlers emit the same semantic event sequence.
- Expired approval cannot start execution.
- An externally handed-off deferred run settles and later resolution starts a
  causally linked run.
- A provider-suspended run returns `Waiting` without a completion or settlement
  event and later resumes the same operation identity.
- A provider-deferred operation survives restart and can be polled only through
  its original compatible route.

## Related specifications

- [Durable execution and recovery](durable-execution-and-recovery.md)
- [Input admission and message queues](input-admission-and-message-queues.md)
- [Error taxonomy](error-taxonomy.md)
