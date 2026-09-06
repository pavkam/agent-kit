# Goals and delegation

**Role:** Represent intended outcomes and delegated work as durable domain
state.

Goals are not prompt prefixes. They have stable identity, ownership, status,
version, budget, attempts, and evidence. Their transitions are part of the
session record and follow the same authorization, queueing, settlement, and
recovery rules as the rest of AgentKit.

AgentKit.Goals will contain the first-party goal coordinator and delegation
policies. Goal values, stores, messages, leases, and join-policy contracts live
in AgentKit.Abstractions. The package remains optional until an application
registers goal behavior.

## Goal lifecycle

A goal moves explicitly through proposed, ready, active, waiting, completed,
failed, cancelled, or blocked states. Every transition records its actor,
reason, prior version, and time. Blocked means that progress requires external
authority or change; it does not mean the task is merely inconvenient.

Execution attempts have their own identities, assigned agent, associated runs,
budget usage, and outcomes. Retrying creates a new attempt without erasing old
evidence. Completion references a verified outcome rather than trusting an
assistant claim.

## Delegation

Delegation creates a child goal with a bounded objective, acceptance criteria,
required capabilities, data and resource scope, budget, deadline, cancellation
relationship, context references, and expected result shape.

A child receives the intersection of parent authority and its assigned scope.
Delegation cannot broaden permissions. The child uses the normal composition,
runtime, context, provider, tool, and permission components and cannot mutate
the parent's history or mark the parent complete.

## Communication and joins

Agent-to-agent communication enters through normal input admission with sender,
recipient, causal goal and attempt, delivery class, and idempotency identity.
Text remains content; routing and authority are typed metadata.

Independent child goals may run concurrently. The parent declares a join policy
such as all, first valid success, quorum, best effort, or a dependency graph.
Winner and result ordering are deterministic from the declared criteria, not
from whichever task happened to finish first.

Child output is untrusted agent-produced data until the parent validates its
shape and evidence. The parent may accept, reject, or request revision.

## Control and limits

Limits bound delegation depth, child count, concurrent attempts, messages, model
usage, tool usage, cost, and elapsed time. Budgets reserve from a parent or an
explicit shared pool. Cyclic dependencies are rejected.

Cancellation propagates according to the declared relationship. Already
performed effects remain truthful and visible. A child may be durably handed
off, but every attempt must eventually settle or require explicit external
action.

## Related concept specifications

- [Goals and multi-agent delegation](../concepts/goals-and-multi-agent-delegation.md)
- [Input admission and message queues](../concepts/input-admission-and-message-queues.md)
- [Durable execution and recovery](../concepts/durable-execution-and-recovery.md)
