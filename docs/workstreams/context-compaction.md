# WS10: Context compaction completion

Goal: compaction is triggered by provider overflow, explicit maintenance, and
instruction-epoch change as well as context pressure; summary generation is a
replaceable seam; strategies resolve through a keyed profile; activation is a
separate coordinator; events reach sinks; and the compactor receives session,
budget, and hook capabilities.

Owning documents: [Context compaction](../architecture/context-compaction.md),
[Context compaction concept](../concepts/context-compaction.md).

## Progress

- [ ] WS10-C1 provider overflow classification
- [ ] WS10-C2 loop overflow retry with `CompactionRetryContinuationCause`
- [ ] WS10-C3 `ExplicitMaintenance` and `InstructionEpochChanged`
- [ ] WS10-C4 summary generator seam
- [ ] WS10-C5 keyed registration, profiles, strategy resolver
- [ ] WS10-C6 events and five-argument `CompactAsync`
- [ ] WS10-C7 activation coordinator
- [ ] WS10-C8 validator

## Verified current state

| Item                                                                                                                                                                                                                                                 | State                      | Evidence                                                                                                                                                              |
| ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ProviderOverflow`, `ExplicitMaintenance`, `InstructionEpochChanged` triggers                                                                                                                                                                        | never raised               | only `ContextPressure` at `DefaultAgentLoop.cs:2654`                                                                                                                  |
| `CompactionRetryContinuationCause`                                                                                                                                                                                                                   | EXISTS-UNWIRED, NO-SPEC    | `Abstractions/Loop/CompactionRetryContinuationCause.cs`; ranked at `DefaultRunContinuationPolicy.cs:182`; never constructed                                           |
| `ICompactionSummaryGenerator(+Resolver)`, `ICompactionActivationCoordinator`, `ICompactionStrategyResolver`, event sink/dispatcher, `CompactionProfileKey`, `ContextCompactionOptions`, `CompactionStrategyRegistration`, `CompactionPolicySnapshot` | MISSING                    | `CompactionPolicySnapshot` only in a remark at `CompactionRequest.cs:20`                                                                                              |
| `ModelCompactionStrategy` resolves models directly                                                                                                                                                                                                   | CONFIRMED                  | `ModelCompactionStrategy.cs:80-82,111-114`; `context-compaction.md:537-586` admits it                                                                                 |
| budget reservation                                                                                                                                                                                                                                   | MISSING                    | `DefaultCompactor.cs:52-61`; `ICompactor.CompactAsync(request, ct)`                                                                                                   |
| `CompactionRequest`, `CompactionRecord`, producer, checkpoint                                                                                                                                                                                        | EXISTS-AS-REDUCED-STAND-IN | remark `CompactionRequest.cs:17-26`                                                                                                                                   |
| registration                                                                                                                                                                                                                                         | unkeyed `TryAddSingleton`  | `Context.Compaction/ServiceExtensions.cs:33-42`                                                                                                                       |
| pressure trigger                                                                                                                                                                                                                                     | once per run               | `DefaultAgentLoop.cs:488,498-510`; `ContextEpoch(0)` hard-coded at `:2652`                                                                                            |
| overflow detection                                                                                                                                                                                                                                   | impossible                 | `ProviderFailureKind` lacks a limit kind; 413 and `request_too_large` map to `InvalidRequest` (`HttpStatusFailureKindMapper.cs:23,37`, `AnthropicErrorMapping.cs:23`) |

Test doubles: `ICompactor` (`DefaultCompactor`, `ScriptedCompactor` at
`DefaultAgentLoopTests.cs:3358`); `ICompactionStrategy` (two production, one
fake in `FakeCompactionCollaborators.cs`); cut selector and validator fakes.

## Hidden prerequisites

1. Overflow failure kind (C1) before any loop retry work.
2. WS7 executor for summary generation; C4 keeps direct provider dependencies
   until `IModelRequestExecutor` is registered.
3. WS9 manifest/epoch source for `InstructionEpochChanged` (C3 after WS9-C3).
4. `BudgetExecutionCapability` producer (WS11-C3), `HookDispatchContext` (WS2),
   session capability for the five-argument compactor; ship overloads.
5. `ICompactionStrategy.Descriptor` addition breaks three implementations and
   one fake; use a default interface member in C5.
6. `Components.Compaction` is absent from the spec record; resolve before C8.

## Spec coverage

| Contract                                                                                      | Spec                           |
| --------------------------------------------------------------------------------------------- | ------------------------------ |
| identities including profile and generator keys, `CompactionEventSinkId`                      | `context-compaction.md:64-103` |
| `CompactionPolicySnapshot`, full `CompactionRequest`                                          | `:148-177`                     |
| summary generator family                                                                      | `:434-516`                     |
| `CompactionStrategyDescriptor`, budgeted `ICompactionStrategy`, `ICompactionStrategyResolver` | `:609,639-665`                 |
| `ICompactionActivationCoordinator`, `CompactionActivationRequest`                             | `:789-830`                     |
| five-argument `ICompactor`                                                                    | `:979-987`                     |
| events, sink, dispatcher                                                                      | `:1011-1071`                   |
| default compactor and model-backed strategy ctors; options; DI                                | `:1088-1113,1162-1330`         |
| `CompactionRetryContinuationCause`, overflow failure kind, maintenance entry point            | NO-SPEC                        |

## Chunks

### WS10-C1: Provider overflow classification

- Depends on: –. Risk: ADDITIVE. Size: S.
- Deliverables: `ProviderFailureKind.ContextLengthExceeded` (write into the enum
  block at `model-and-embedding-providers.md:379-390`); mappings in
  `HttpStatusFailureKindMapper`, `AnthropicErrorMapping`, OpenAI
  `context_length_exceeded`, Gemini/Cohere/Mistral equivalents; per-leaf
  error-mapping fixtures. Snapshots: Abstractions plus leaves.

### WS10-C2: Loop overflow retry

- Depends on: C1. Risk: DENSE-MODIFY `DefaultAgentLoop.cs` L859-891,
  `CompactUnderPressureAsync` L2625-2697, turn loop L488-510. Size: M.
- Deliverables: on `ModelAttemptFailed{ContextLengthExceeded}` with a compactor
  and no retry yet this turn, compact with `ProviderOverflow` and the turn's
  operation id, produce `CompactionRetryContinuationCause`, re-issue with a
  fresh `ModelRequestId`; a second overflow settles as failed; tests with
  `ScriptedCompactor` and a once-failing `FakeLlmModel`.

### WS10-C3: `ExplicitMaintenance` and `InstructionEpochChanged`

- Depends on: WS9-C3. Risk: ADDITIVE. Size: S.
- Deliverables: an engine-level maintenance entry point (NO-SPEC; simplest is
  `Agent.CompactAsync(sessionId, branchId)` with a before-run correlation); loop
  compares the assembler's epoch with the last checkpoint's and raises
  `InstructionEpochChanged`. Snapshot: AgentKit.

### WS10-C4: Summary generator seam

- Depends on: –. Risk: ADDITIVE contracts; DENSE-MODIFY
  `ModelCompactionStrategy.cs:60-330`, `ServiceExtensions.cs:47-59`. Size: M.
- Deliverables: Abstractions generator key/version, descriptor, segment,
  request, generated summary, generation result (+4),
  `ICompactionSummaryGenerator`, resolution (+2),
  `ICompactionSummaryGeneratorResolver` (`:434-516`);
  `ModelBackedSummaryGenerator` (provider code moves here),
  `DefaultSummaryGeneratorResolver`; strategy delegates. Budget capability
  parameter nullable until WS11-C3. Snapshots: Abstractions, Context.Compaction.

### WS10-C5: Keyed registration, profiles, strategy resolver

- Depends on: –. Risk: DENSE-MODIFY `ServiceExtensions.cs:13-59`,
  `DefaultCompactor.cs:52-61,75`. Size: M.
- Deliverables: `CompactionProfileKey`, `CompactionProfileVersion`,
  `CompactionStrategyDescriptor`, `CompactionStrategyResolution` (+2),
  `ICompactionStrategyResolver`, `CompactionPolicySnapshot` (additive nullable
  on `CompactionRequest`); `ContextCompactionOptions`, snapshot,
  `CompactionProfileOptions`, `CompactionStrategyRegistration`,
  `CompactionStrategyKeys`, `DefaultCompactionStrategyResolver`;
  `AddAgentContextCompaction(ComponentKey<ICompactor>, …)`,
  `AddCompactionProfile`, `AddCompactionStrategy<T>(key, registration)`,
  `Replace*`; `ICompactionStrategy.Descriptor` as a default member. Snapshots:
  Abstractions, Context.Compaction.

### WS10-C6: Events and five-argument `CompactAsync`

- Depends on: WS2, WS11-C3, C5. Risk: CONTRACT-BREAK on `ICompactor` unless
  shipped as an overload. Size: M.
- Deliverables: `CompactionEvent` (+3), outcome kind/summary,
  `ICompactionEventSink`, dispatch result (+2), `ICompactionEventDispatcher`,
  `CompactionEventSinkId`; `DefaultCompactionEventDispatcher`,
  `AddCompactionEventSink<T>(key, registration)`; compactor validates the
  session capability against the request and passes budget to the strategy;
  required-sink failure blocks activation.

### WS10-C7: Activation coordinator

- Depends on: C5. Risk: ADDITIVE. Size: S.
- Deliverables: `ICompactionActivationCoordinator`, activation request/result;
  `SessionCompactionActivationCoordinator` (append and optimistic concurrency
  move out of the compactor); existing conflict tests unchanged.

### WS10-C8: Validator

- Depends on: WS18 `Components`. Size: S.
- Deliverables: compaction selected ⇒ exactly one compatible compactor key.

## Totals

S 4, M 4. Confidence medium: the overflow kind and maintenance entry point are
NO-SPEC, and C6 depends on three other workstreams.
