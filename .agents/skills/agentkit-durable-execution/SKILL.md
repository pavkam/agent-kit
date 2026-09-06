---
name: agentkit-durable-execution
description:
  "Implement or review AgentKit.Durability checkpoints, recovery evidence,
  leases, fencing, codecs, and backend adapters. Use when work must resume
  safely after process loss; not for ordinary transient retries, session
  persistence alone, or provider-specific request translation."
---

# AgentKit Durable Execution

Read [AGENTS.md](../../../AGENTS.md). For C# API or implementation work, also
read the [modern C# rules](../references/modern-csharp.md).

## Authoritative design

- Read
  [durable execution architecture](../../../docs/architecture/durable-execution.md)
  for the optional coordinator, contracts, profiles, DI, and backend boundary.
- Read the normative
  [durable execution and recovery](../../../docs/concepts/durable-execution-and-recovery.md)
  specification for operations, checkpoints, evidence, replay, and fencing.
- Read
  [run lifecycle and settlement](../../../docs/concepts/run-lifecycle-and-settlement.md)
  when changing handoff or terminal settlement behavior.

## Working rules

1. Keep durable contracts in `AgentKit.Abstractions`, optional coordination in
   `AgentKit.Durability`, and workflow-engine integrations in leaf packages.
2. Record stable operation and idempotency identities, versioned input/result,
   retry owner, timeout, cancellation, and side-effect classification.
3. Recover from evidence: reconcile known idempotent work, commit an existing
   terminal result without reinvocation, and require action for unknown
   non-idempotent effects. Never manufacture exactly-once semantics.
4. Replay captured configuration, catalog, profile, clock, identity, and random
   inputs. Do not perform live DI discovery or silently reinterpret old schemas.
5. Use expiring leases with monotonic fencing tokens for distributed ownership;
   every durable write must reject a stale owner.
6. Keep durability optional and explicit. No backend, journal, or recovery
   profile may appear by fallback, and cancellation reports true external state.

Verify crashes around every protected effect and checkpoint, duplicate wakes,
idempotent commit, unknown outcomes, lease takeover, stale fencing, schema
migration or incompatibility, cancellation, and explicit non-durable behavior.
