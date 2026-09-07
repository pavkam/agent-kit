# Goals and multi-agent delegation

**Status:** Normative domain model

**Architecture:**
[Goals and delegation](../architecture/goals-and-delegation.md)

**Depends on:** [Input admission](input-admission-and-message-queues.md),
[sessions](sessions-persistence-and-branching.md)

## Purpose

Goals and delegated work are durable domain state, not prose prefixes. The same
[queue](input-admission-and-message-queues.md),
[budget](usage-limits-and-budgets.md), correlation, and
[settlement](run-lifecycle-and-settlement.md) rules apply whether work is
performed by one loop or several agents.

## Goal model

The canonical public `AgentGoal` shape is defined once in the
[goals and delegation architecture](../architecture/goals-and-delegation.md#normative-minimal-contract-shape).
This concept owns lifecycle, causality, and delegation semantics rather than a
parallel API declaration.

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
parent authority and its assigned scope. Tools still pass the
[current security authority](permissions-approvals-and-trust.md) under the
child's principal/agent context.

## Communication

Agent-to-agent messages use the
[normal admitted-input contract](input-admission-and-message-queues.md) with
sender, recipient, causal goal/attempt, delivery class, and idempotency ID. Text
is content, not routing metadata.

Progress is a bounded event and MAY be live-only. Decisions, handoffs, result,
failure, request for authority, and cancellation are durable semantic events.

## Parallel work

The parent MAY create independent child goals and await a join policy: all,
first successful, quorum, best effort, or named dependency graph. Join order and
winner selection MUST follow a captured policy. The default orders by recorded
child ordinal. A fastest-valid-success policy is explicitly timing-sensitive: it
selects and persists the winner using the durable parent join-inbox sequence,
never an unrecorded `Task.WhenAny` result. Replay uses that persisted decision.
Ordinal-first-success waits for earlier children to become terminally
ineligible. Quorum and deadline policies record their eligible set and cutoff.

Cancelling a parent propagates according to the declared relationship. Already
performed child side effects remain and their certainty is reported.

Local dispatch commits a child-admission intent; a host-owned worker invokes the
public engine after readiness. A dispatcher MUST NOT capture a runner that
creates a constructor cycle back through the loop and goal coordinator. A
waiting parent releases active-worker occupancy and session critical sections,
retains budget ownership, and wakes by expected operation identity. Children
must have a lane and executor capacity that can progress independently of the
waiting parent. See the
[delegation boundary](../architecture/goals-and-delegation.md#delegation-discovery-selection-policy-and-execution).

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
- Ordinal joins ignore task completion order; timing-sensitive joins replay the
  recorded inbox winner exactly.
- A one-worker host parks a waiting parent so its separately admitted child can
  progress without a dependency cycle or duplicate child.
- A child result cannot complete its parent without validation.
- Parent cancellation settles every child attempt or durable handoff.
- Goal replay reconstructs status and ownership from transitions.

## Related specifications

- [Usage limits and budgets](usage-limits-and-budgets.md)
- [Durable execution and recovery](durable-execution-and-recovery.md)
- [Permissions, approvals, and trust](permissions-approvals-and-trust.md)
