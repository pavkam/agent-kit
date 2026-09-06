# Goals and multi-agent delegation

**Status:** Normative domain model  
**Depends on:** [Input admission](input-admission-and-message-queues.md),
[sessions](sessions-persistence-and-branching.md)

## Purpose

Goals and delegated work are durable domain state, not prose prefixes. The same
queue, permission, budget, correlation, and settlement rules apply whether work
is performed by one loop or several agents.

## Goal model

```csharp
public sealed record AgentGoal(
    GoalId Id,
    GoalId? ParentId,
    SessionId SessionId,
    AgentId OwnerAgentId,
    GoalStatus Status,
    GoalDefinition Definition,
    int Attempt,
    GoalBudget Budget,
    DateTimeOffset CreatedAt,
    VersionToken Version,
    ExtensionData Metadata);
```

Statuses MUST include proposed, ready, active, waiting, completed, failed,
cancelled, and blocked or equivalent. Transitions are explicit durable events
with actor, reason, prior version, and timestamp.

`Blocked` means progress requires external change or authority, not merely that
work is difficult. A completed goal has a verified outcome reference, not just
an assistant claim.

## Attempts and ownership

An execution attempt has its own ID, assigned agent, run(s), start/terminal
time, budget consumption, and outcome. Retrying increments attempt and preserves
prior evidence. Reassignment changes ownership through a transition; it MUST not
mutate old attempts.

At most one worker holds the active execution lease for a goal unless the goal
explicitly contains parallel child work. Distributed leases require fencing.

## Delegation

Delegation creates a child goal or task envelope containing:

- parent goal/attempt and causal run/message;
- bounded objective and acceptance criteria;
- explicit authority, resource, and data scope;
- assigned agent/capability requirements;
- budget/deadline and cancellation relationship;
- context references with provenance, not an uncontrolled history dump; and
- expected typed result and evidence.

Delegation MUST NOT broaden authority. A child receives the intersection of
parent authority and its assigned scope. Tools still pass current permission
policy under the child's principal/agent context.

## Communication

Agent-to-agent messages use the normal admitted-input contract with sender,
recipient, causal goal/attempt, delivery class, and idempotency ID. Text is
content, not routing metadata.

Progress is a bounded event and MAY be live-only. Decisions, handoffs, result,
failure, request for authority, and cancellation are durable semantic events.

## Parallel work

The parent MAY create independent child goals and await a join policy: all,
first successful, quorum, best effort, or named dependency graph. Join order and
winner selection MUST be deterministic from declared criteria, not task
completion timing.

Cancelling a parent propagates according to the declared relationship. Already
performed child side effects remain and their certainty is reported.

## Result integration

Child output is untrusted agent-produced data until validated. The parent MUST
verify the expected result schema and evidence, preserve provenance, and decide
whether to accept, reject, or request revision. A child cannot directly mark the
parent goal complete or mutate parent history.

## Backpressure and recursion

Limits MUST bound child count, delegation depth, concurrent attempts, message
volume, total requests/tokens/cost/tools, and elapsed time. Budgets reserve from
the parent or an explicit shared pool. Cyclic goal dependencies MUST be
rejected.

## Acceptance scenarios

- Retried task messages do not create duplicate child goals.
- Child authority cannot exceed the parent's scope.
- Parallel completion order does not change deterministic join results.
- A child result cannot complete its parent without validation.
- Parent cancellation settles every child attempt or durable handoff.
- Goal replay reconstructs status and ownership from transitions.

## Upstream synthesis

Pi, OpenCode, and Pydantic AI primarily supply single-agent loop and tool
mechanics. AgentKit deliberately generalizes their identity, queueing,
settlement, and durable-operation lessons to delegation. This spec is a design
synthesis, not a claim that any one upstream implements the full model; see
[research provenance](research-provenance.md).

## Related specifications

- [Usage limits and budgets](usage-limits-and-budgets.md)
- [Durable execution and recovery](durable-execution-and-recovery.md)
- [Permissions, approvals, and trust](permissions-approvals-and-trust.md)
