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
7. Select a journal adapter by what it can prove.
   [`AgentKit.Durability.Sqlite`](../../../src/AgentKit.Durability.Sqlite/README.md)
   is the only leaf with a lease manager, because its journal and lease manager
   share one host-supplied database.
   [`AgentKit.Durability.Json`](../../../src/AgentKit.Durability.Json/README.md)
   is durable and inspectable but single-writer, so it ships none.
   `AgentKit.Durability.InMemory` is explicitly ephemeral. All three run the
   same journal conformance suite.
8. A consumer journals a boundary through `DurableBoundaryScope` in
   `AgentKit.Abstractions`, gated on the selected profile enabling the
   boundary's operation name and on the capture being durably addressable. The
   scope publishes one live continuation into the engine-wide
   `DurableBoundaryRegistry`, and a `DurableBoundaryHandler` subclass registered
   for that name bridges the coordinator back to it. A recovering process holds
   no continuation and must refuse rather than invent a terminal record. The
   seven first-party boundaries are listed in the architecture document; add a
   new one as a dedicated operation-name class, manifest record, and handler,
   not as another branch in an existing boundary.
9. A handler writes mid-operation evidence only through
   `DurableInvocationContext.Checkpoints` (`IDurableCheckpointWriter`). Never
   hand a handler the journal, the lease, the captured grant, or the
   checkpoint-id generator. Call `DurableRecordResult.ThrowIfNotRecorded()`
   before an effect whose evidence makes recovery honest (provider call, tool
   call, session append); never discard the result of such a write. The
   coordinator authorizes each write with the effect its journal method enforces
   (start `Create`, checkpoint `Append`, wait `Mutate`, terminal `Append`); a
   test double that ignores effect enforcement will not catch drift, so cover
   the path against a real journal.
10. Payloads are identity-and-count manifests. Prompts, arguments, results,
    approval prompts, and resource paths never enter a durable record.
11. Durable writes failing to record is a composition or runtime fact, not a
    silent one: a profile may enable only operation names that have a registered
    handler (`agentkit.durability.handler.missing`), and a component that cannot
    compose a handler it needs registers it idempotently with
    `TryAddEnumerable`.
12. A security authority must not depend on the durability coordinator. The
    coordinator authorizes its own writes through the authority, so any
    authority-to-coordinator edge, including `Lazy<T>` or `Func<T>`, is a
    service-graph cycle. Code that observes a deferred decision (the tool
    executor) records the wait through `IApprovalWaitRecorder`.
13. The loop's `agentkit.loop.tool_call` checkpoint and the tool runtime's
    `IToolCallRecorder` are distinct and must not duplicate each other: the
    checkpoint is the journal's identity manifest that a requested call entered
    the executor; the session `ToolCallAcceptedSessionEntry` (committed after
    authorization, before the invoker) and `ToolCallTerminalSessionEntry` are
    the call's authoritative acceptance and settlement facts. An accepted entry
    with no terminal entry is "effect may have started" evidence for recovery.
    The recorder never writes through the journal.

A storage fence cannot stop an external effect. Takeover requires receiver
fencing, idempotency, or reconciliation before another invocation. Grant and
budget storage must match the requested distributed recovery domain. Terminal
semantic output and unfinished settlement remain separate durable facts.

Verify crashes around every protected effect and checkpoint, duplicate wakes,
idempotent commit, unknown outcomes, lease takeover, stale fencing, schema
migration or incompatibility, cancellation, and explicit non-durable behavior.
Test a consumer's sandwich in its own package with
`RecordingBoundaryCoordinator` and `FixedDurabilityProfileCatalog` from
`AgentKit.Test.Shared`, and test process loss through a real engine by
decorating the keyed journal so a terminal commit is lost, as
`AgentKit.Simple.Tests` does.
