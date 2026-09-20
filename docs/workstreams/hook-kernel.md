# WS2: Hook kernel

Goal: replace the reduced dispatcher (caller passes hook instances and a scope)
with the specified kernel: closed `HookPointDefinition<THook,TEventArgs>`
values, a captured `HookCatalogSnapshot` per profile, per-dispatch
`HookDispatchContext`, activation leases, invocation tracking, deterministic
ordering, monotonic failure policy, and diagnostics. Phase 2a (C1–C8) rebuilds
the kernel and migrates the three existing points; phase 2b (C9–C13) adds
profiles, diagnostics, and timeouts. New component points land with their owning
workstreams, not here.

Owning documents: [Extensions](../architecture/extensions.md),
[Hooks and middleware](../concepts/extensions-hooks-and-middleware.md).

## Progress

- [x] WS2-C1 identity and vocabulary types
- [x] WS2-C2 kernel contract records and interfaces
- [x] WS2-C3 event-args reshape to `HookDispatchMetadata`
- [x] WS2-C4a order resolver and invocation tracker
- [ ] WS2-C4b registration catalog, instance factory, profile selector
- [ ] WS2-C5 new `DispatchAsync` overload and conformance suite
- [ ] WS2-C6 migrate `DefaultAgentLoop` and `AgentHookPoints`
- [ ] WS2-C7 remove legacy kernel surface
- [ ] WS2-C8 composition validation for hook services
- [ ] WS2-C9 `AgentDefinition.HookProfile`
- [ ] WS2-C10 named hook profiles
- [ ] WS2-C11 diagnostic sinks
- [ ] WS2-C12 timeout and quiescence
- [ ] WS2-C13 documentation reconciliation

## Verified current state

| Type                                                                                                                                                                                                                  | State                             | Evidence                                                                                                                                                    |
| --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `IHookDispatcher.DispatchAsync<THook,TArgs>(HookPointId, IEnumerable<THook>, TArgs, invoker, HookDispatchScope, HookFailureMode, int, CT)`                                                                            | EXISTS-AND-USED (old shape)       | `src/AgentKit.Abstractions/Hooks/IHookDispatcher.cs:30-73`; only impl `src/AgentKit.Hooks/DefaultHookDispatcher.cs:29`; only consumer `DefaultAgentLoop.cs` |
| `DefaultHookDispatcher`                                                                                                                                                                                               | EXISTS-AND-USED                   | reentrancy, `HookOrdering.Sort`, isolation snapshot/restore (`:231-263`), post-hook `Validate()` (`:269`), short-circuit (`:271`)                           |
| `HookOrdering` (internal)                                                                                                                                                                                             | EXISTS-AND-USED                   | `src/AgentKit.Hooks/HookOrdering.cs`; port target for `IHookOrderResolver`                                                                                  |
| `IHook`, `HookPriority`, `HookDispatchScope`, `AgentScopedHookEventArgs`                                                                                                                                              | EXISTS, to be superseded          | `Abstractions/Hooks/*.cs`; `HookDispatchScope` has 76 test references                                                                                       |
| `IRunStartedHook`, `IBeforeModelRequestHook`, `IBeforeToolInvocationHook` with `On*Async(args, ct)`                                                                                                                   | EXISTS-AND-USED                   | spec wants `InvokeAsync(args, HookInvocationContext, ct)` (`extensions.md:178-184`)                                                                         |
| `AgentHookEventArgs(OperationCorrelation, DateTimeOffset, HookInvocationId)`                                                                                                                                          | EXISTS, wrong shape               | carries a per-dispatch-shared `InvocationId` (`extensions.md:149-153`); no `Point`, `DispatchId`, `Deadline`                                                |
| `RunStartedEventArgs`, `BeforeModelRequestEventArgs`, `BeforeToolInvocationEventArgs`, `IShortCircuitingHookArgs`, `ToolInvocationVeto`                                                                               | EXISTS-AND-USED                   | constructed only at `DefaultAgentLoop.cs:479-481,773-774,1425-1426`                                                                                         |
| `AgentHookPoints` (three `HookPointId` values)                                                                                                                                                                        | EXISTS, not definitions           | `Abstractions/Hooks/AgentHookPoints.cs`                                                                                                                     |
| `HookFailureMode { FailOperation, Isolate }`                                                                                                                                                                          | EXISTS, wrong member name         | spec `IsolateAndDiagnose`; 27 references                                                                                                                    |
| `HookId`, `HookInvocationId`, `HookPointId`, `HookProfileKey`                                                                                                                                                         | EXISTS (`HookProfileKey` unwired) | `Abstractions/Identity/`                                                                                                                                    |
| `AgentHookOptions { MaximumInvocationDepth, MinimumFailureMode }`                                                                                                                                                     | EXISTS, partial                   | spec adds `DefaultHookTimeout`, `MutationDispatchMode`, `ReloadBoundary`                                                                                    |
| `AddAgentHooks`, `Add*Hook<T>()`                                                                                                                                                                                      | EXISTS-AND-USED                   | `src/AgentKit.Hooks/ServiceExtensions.cs:20-105`; called by `AgentKit.Simple/AgentEngineBuilderExtensions.cs:739`                                           |
| `HookRegistrationId`, `HookDispatchId`, `HookCatalogVersion`, `HookOrder`, `HookLifetime`, `HookReentrancyPolicy`, `HookPointKind`, `HookMutationDispatchMode`, `HookReloadBoundary`                                  | MISSING                           | –                                                                                                                                                           |
| `HookRegistrationDescriptor`, `HookCatalogSnapshot`, `HookDispatchMetadata`, `HookInvocationContext`, `HookDispatchContext`, `HookProfileOptions`, `HookInvocationDiagnostic`                                         | MISSING                           | –                                                                                                                                                           |
| `HookPointDefinition<,>`, `HookInvoker<,>`, `IHookMutationValidator<>`                                                                                                                                                | MISSING                           | –                                                                                                                                                           |
| `IHookRegistrationSource`, `IHookProfileSelector`, `IHookOrderResolver`, `IHookCatalog`, `IHookInstanceFactory`, `IHookActivationLease`, `IHookInvocationTracker`, `IHookDiagnosticSink`, `IHookDiagnosticDispatcher` | MISSING                           | –                                                                                                                                                           |
| `AgentDefinition.HookProfile`                                                                                                                                                                                         | MISSING                           | `Composition/AgentDefinition.cs` has `SecurityProfile` (`:184`) and `SessionProfile` (`:188`) only                                                          |
| hook services in composition validation                                                                                                                                                                               | MISSING                           | `src/AgentKit/AgentCompositionValidator.cs` has no hook references                                                                                          |

Dispatch call sites: `DefaultAgentLoop.cs:474-486` (`RunStarted`, isolate),
`:771-799` (`BeforeModelRequest`, fail operation, applies `Settings`),
`:1423-1442` (`BeforeToolInvocation`, veto and argument rewrite). The loop
constructor (`:162-232`) takes `IHookDispatcher?`, three `IEnumerable<I*Hook>?`,
and `IIdentifierGenerator<HookInvocationId>?`; it fails closed when hooks exist
without a dispatcher (`:217-222`).

Test doubles: `tests/AgentKit.Hooks.Tests/TestHook.cs` (used by ~50 tests in
`DefaultHookDispatcherTests.cs`), `TestHookEventArgs.cs`,
`TestAgentScopedHookEventArgs.cs`, `ServiceExtensionsTests.cs:183-204` (4
hooks), `tests/AgentKit.Loop.Tests/DefaultAgentLoopTests.cs:3186-3218` (3 hooks;
10 loop hook tests at `:2998-3182`; `CreateLoop` at `:3646-3705`),
`tests/AgentKit.Simple.Tests/AgentEngineBuilderExtensionsTests.cs:969`
(`VetoSecretsHook`, end-to-end via DI at `:584`), 11 files under
`tests/AgentKit.Abstractions.Tests/Hooks/` (27 `InvocationId` references). There
are no test implementers of `IHookDispatcher`.

## Hidden prerequisites

1. `HookDispatchContext` scope contradiction (per-dispatch in
   `extensions.md:102-105`, per-run elsewhere). Recommended: keep it
   per-dispatch and add a run-scoped `HookActivationScope(Catalog, Activation)`
   with `CreateDispatch(HookDispatchMetadata)`; WS1's invocation carries the
   scope. Record the reconciliation in `extensions.md` and `agent-runtime.md`.
2. `IIdentifierGenerator<HookDispatchId>` and `<HookInvocationId>` must be
   registered by `AddAgentHooks`; the loop currently fabricates one
   (`DefaultAgentLoop.cs:231`). `AgentKit.Hooks` needs its own GUID generator.
3. Snapshot/restore for isolation is not in the spec; keep
   `CaptureMutableState`/`RestoreMutableState` on `AgentHookEventArgs` and add a
   default validator delegating to `Validate()`.
4. Registration-to-instance binding has no spec type: add an internal
   `HookRegistrationBinding(Descriptor, HookInterface, ImplementationType|Instance)`
   emitted by `Add*Hook<T>` and consumed by the catalog and instance factory.
   Default `HookRegistrationId` derivation is an open question.
5. A point-definition registry (`HookPointDefinitionRegistration`) is needed to
   validate registrations against closed definitions; prose only
   (`extensions.md:498,524`).
6. `HookDispatchMetadata.Deadline`: kernel clamps to
   `min(caller, now + DefaultHookTimeout)`; enforcement is C12.
7. `tests/AgentKit.Tests` composes without `AgentKit.Hooks`; when hook services
   become required spine (C8), `CompositionTestData.AddRunServicesFakes` needs
   fakes for `IHookDispatcher`, `IHookCatalog`, `IHookProfileSelector`,
   `IHookOrderResolver`, `IHookInstanceFactory` in `AgentKit.Test.Shared`.
8. WS1 moves hook resolution into the run invocation; keep C6's loop change to
   three constructor parameters so WS1 replaces one block.
9. `examples/CodingAgent` references `AgentKit.Hooks` but has no hook code.

## Spec coverage

| Contract                                                                                      | Spec                                   | Status                                                                               |
| --------------------------------------------------------------------------------------------- | -------------------------------------- | ------------------------------------------------------------------------------------ |
| identities                                                                                    | `extensions.md:49-63`                  | SPEC                                                                                 |
| `HookRegistrationDescriptor`                                                                  | `extensions.md:72-82`                  | SPEC; `HookOrder`, `HookLifetime`, `HookReentrancyPolicy` NO-SPEC                    |
| `HookCatalogSnapshot`, `HookDispatchMetadata`, `HookInvocationContext`, `HookDispatchContext` | `extensions.md:84-105`                 | SPEC (scope conflict)                                                                |
| `IHookInvocationTracker`, `IHookActivationLease`, `IHookInstanceFactory`                      | `extensions.md:107-128`                | SPEC; attempt/result/resolution types NO-SPEC                                        |
| `AgentHookEventArgs`                                                                          | `extensions.md:130-138`                | SPEC (properties); capture/restore NO-SPEC                                           |
| example tool point args                                                                       | `extensions.md:157-207`                | SPEC but uses WS4 types                                                              |
| source, profile selector, order resolver, catalog                                             | `extensions.md:219-244`                | SPEC; request/result types NO-SPEC                                                   |
| `HookPointDefinition<,>`, `HookInvoker<,>`, `IHookDispatcher`                                 | `extensions.md:246-273`                | SPEC; `HookPointKind`, validator members NO-SPEC                                     |
| diagnostic sink and dispatcher                                                                | `extensions.md:283-295`                | SPEC; `HookInvocationDiagnostic` NO-SPEC                                             |
| `HookDispatcher` ctor                                                                         | `extensions.md:340-345`                | SPEC (class name differs from `DefaultHookDispatcher`)                               |
| `AgentHookOptions`, `ServiceExtensions`                                                       | `extensions.md:373-437`                | SPEC; `HookMutationDispatchMode`, `HookReloadBoundary`, `HookProfileOptions` NO-SPEC |
| `AgentDefinition.HookProfile`                                                                 | `composition-and-configuration.md:173` | SPEC                                                                                 |
| composition requirements                                                                      | `extensions.md:523-533`                | prose; diagnostic codes NO-SPEC                                                      |

## Chunks

### WS2-C1: Identity and vocabulary types

- Depends on: –. Risk: ADDITIVE. Size: S.
- Deliverables: `Identity/HookRegistrationId.cs`, `HookDispatchId.cs`,
  `HookCatalogVersion.cs`; `Hooks/HookPointKind.cs`
  (`Observational, Mutating, ShortCircuiting`), `HookLifetime.cs`,
  `HookOrder.cs` + `HookOrderAnchor.cs`, `HookReentrancyPolicy.cs`,
  `HookMutationDispatchMode.cs`, `HookReloadBoundary.cs`; identity conformance
  tests. Snapshot: Abstractions.
- Open: exact enum member sets.

### WS2-C2: Kernel contract records and interfaces

- Depends on: C1. Risk: ADDITIVE. Size: M.
- Deliverables (all in `Abstractions/Hooks/`): `HookRegistrationDescriptor`,
  `HookCatalogSnapshot`, `HookDispatchMetadata`, `HookInvocationContext`,
  `HookDispatchContext`, `HookActivationScope`, `HookPointDefinition<,>`,
  `HookInvoker<,>`, `IHookMutationValidator<>`, `IHookInvocationTracker`,
  `HookInvocationAttempt`, `HookInvocationTrackingResult` (+`Entered`,
  `Rejected`), `IHookActivationLease`, `HookInstanceResolution<>` (+`Resolved`,
  `Unavailable`), `IHookInstanceFactory`, `IHookCatalog`, `HookCatalogRequest`,
  `IHookProfileSelector`, `HookProfileSelectionRequest`,
  `HookProfileSelectionResult` (+2), `IHookOrderResolver`, `HookOrderResult`
  (+2), `IHookRegistrationSource`, `HookRegistrationSnapshot`,
  `IHookDiagnosticSink`, `IHookDiagnosticDispatcher`,
  `HookInvocationDiagnostic`; guard tests per record. Snapshot: Abstractions.
- Done when: all compile with zero consumers; `HookDispatchContext` usable by
  WS1, WS3, WS4 immediately.

### WS2-C3: Event-args reshape

- Depends on: C2. Risk: CONTRACT-BREAK (6 Abstractions hook test files, Hooks
  fixtures, 7 `InvocationId` assertions in dispatcher tests, 3 loop construction
  sites). Size: M.
- Deliverables: `AgentHookEventArgs(HookDispatchMetadata)` with `Point`,
  `DispatchId`, `Deadline`; three concrete args take metadata first; rename
  `HookFailureMode.Isolate` to `IsolateAndDiagnose` (27 references); tags
  `HookDispatchId`, `HookRegistrationId` in Observability; loop gains
  `IIdentifierGenerator<HookDispatchId>?` and builds metadata at the three
  sites. Snapshots: Abstractions, Hooks, Loop, Observability.
- Done when: the 10 loop hook tests and the Simple veto test pass unchanged.

### WS2-C4a: Order resolver and invocation tracker

- Depends on: C2. Risk: ADDITIVE. Size: M.
- Deliverables: `HookOrderResolver` (port of `HookOrdering` over descriptors;
  soft before/after, hard `DependsOn`, single first/last, contradictions, cycles
  → `HookOrderInvalid` with stable `CompositionDiagnostic` codes),
  `HookInvocationTracker` (lock-guarded depth per lease and point);
  `HookOrderResolverTests` mirroring the concept ordering matrix,
  `HookInvocationTrackerTests`. Snapshot: Hooks.

### WS2-C4b: Registration catalog, instance factory, profile selector, options

- Depends on: C4a. Risk: ADDITIVE. Size: M.
- Deliverables: internal `HookRegistrationBinding`; public
  `HookPointDefinitionRegistration` (in Abstractions);
  `HookRegistrationCatalog : IHookCatalog`;
  `ServiceProviderHookInstanceFactory : IHookInstanceFactory` (scope per lease,
  lifetimes, owns a tracker); `DefaultHookProfileSelector` (single default
  profile); `HookProfileOptions` shell; `AgentHookOptions` gains
  `DefaultHookTimeout`, `MutationDispatchMode`, `ReloadBoundary` with
  validation; `AddAgentHooks` registers catalog, factory, selector, resolver,
  both generators, the three point definitions; `Add*Hook<T>()` also emits a
  binding. Tests for each. Snapshot: Hooks.
- Open: derived default `HookRegistrationId`.

### WS2-C5: New `DispatchAsync` overload and conformance suite

- Depends on: C3, C4b. Risk: ADDITIVE (interface gains a member; no test
  implementers). Size: M.
- Deliverables: a new `IHookDispatcher.DispatchAsync<THook,TEventArgs>` overload
  taking `HookPointDefinition`, `HookDispatchContext`, the event args, an
  optional `HookFailureMode` tightening, and a cancellation token;
  `DefaultHookDispatcher` implements the three-way point/dispatch match, catalog
  selection, per-invocation context, tracker enter/exit, lease resolution,
  invoke, validate, isolation only for observational points under
  `IsolateAndDiagnose`, monotonic policy, short-circuit;
  `tests/AgentKit.Conformance/HookDispatcherConformanceTests.cs` covering the
  concept acceptance list (`extensions-hooks-and-middleware.md:273-310`).
  Snapshots: Abstractions, Hooks.

### WS2-C6: Migrate `DefaultAgentLoop` and `AgentHookPoints`

- Depends on: C5. Risk: DENSE-MODIFY `DefaultAgentLoop.cs` ctor `:162-232`,
  `:474-486`, `:771-799`, `:1423-1442`; `AgentHookPoints.cs`. Size: M.
- Deliverables: `AgentHookPoints` exposes three `HookPointDefinition` values
  (observational/isolate, mutating/fail, short-circuiting/fail); loop drops the
  three `IEnumerable<I*Hook>` parameters, takes `IHookCatalog?` and
  `IHookInstanceFactory?`, captures a catalog at run start, creates a lease and
  activation scope, dispatches through the new overload; `AgentKit.Test.Shared`
  gets `StaticHookCatalog`, `StaticHookInstanceFactory`. Snapshots:
  Abstractions, Loop.
- Done when: loop tests pass with unchanged assertions;
  `rg "HookDispatchScope|IEnumerable<IRunStartedHook>" src/AgentKit.Loop` is
  empty.

### WS2-C7: Remove legacy kernel surface

- Depends on: C6. Risk: CONTRACT-BREAK (`TestHook`, dispatcher tests,
  `ServiceExtensionsTests`, `HookDispatchScopeTests`, `IHookTests`,
  `AgentScopedHookEventArgsTests`, three loop fakes, `VetoSecretsHook`). Size:
  M.
- Deliverables: delete old overload, `IHook`, `HookPriority`,
  `HookDispatchScope`, `AgentScopedHookEventArgs`, `HookOrdering`; point
  interfaces become `InvokeAsync(TArgs, HookInvocationContext, CT)`;
  `Add*Hook<T>(HookRegistrationDescriptor)`, `Replace*Hook<T>`,
  `ReplaceHookDispatcher/Catalog/OrderResolver/ProfileSelector<T>`; concrete
  args declare `AgentId`/`SessionId` directly. Snapshots: Abstractions, Hooks,
  Loop, Simple.
- Done when: `rg "IHook\b|HookPriority|HookDispatchScope" src tests` is empty.

### WS2-C8: Composition validation for hook services

- Depends on: C7. Risk: DENSE-MODIFY
  `AgentCompositionValidator.cs:58-105,132-156`,
  `ComponentRegistrationSnapshot.cs:106-123`. Size: M.
- Deliverables: require singular `IHookDispatcher`, `IHookCatalog`,
  `IHookProfileSelector`, `IHookOrderResolver`, `IHookInstanceFactory`, both
  generators; validate point-definition collisions and default-profile catalog
  capture; diagnostics `agentkit.hook-dispatcher.missing`,
  `agentkit.hook-catalog.missing`, `agentkit.hook-point.collision`; Test.Shared
  fakes in `CompositionTestData`. Snapshot: AgentKit if constants are public.
- Done when: an engine built without `AddAgentHooks` fails with the new
  diagnostics; Simple unaffected.

### WS2-C9: `AgentDefinition.HookProfile` and request plumbing

- Depends on: C8, WS18-C3 if landed. Risk: ADDITIVE. Size: M.
- Deliverables: `HookProfileKey HookProfile` (default `"default"`),
  `AgentLoopRunRequest.HookProfile` (the loop-level request type, renamed from
  `AgentRunRequest` in WS1-C8), engine passes it, loop uses it in
  `HookCatalogRequest`; validator resolves each definition's profile. Snapshots:
  Abstractions, AgentKit.

### WS2-C10: Named hook profiles

- Depends on: C9. Risk: ADDITIVE. Size: M.
- Deliverables: `HookProfileOptions` (registration filter, requested failure
  mode, reload boundary), `AddHookProfile`, `ReplaceHookProfile`, per-profile
  catalog filtering; per-definition selection tests.

### WS2-C11: Diagnostic sinks

- Depends on: C7. Risk: ADDITIVE. Size: M.
- Deliverables: `HookDiagnosticDispatcher`, `AddHookDiagnosticSink<T>`,
  `ReplaceHookDiagnosticDispatcher<T>`; kernel emits a content-free
  `HookInvocationDiagnostic` per invocation; tests assert no protected values.

### WS2-C12: Timeout and quiescence

- Depends on: C5. Risk: DENSE-MODIFY `DefaultHookDispatcher`. Size: M.
- Deliverables: enforce `HookDispatchMetadata.Deadline` and
  `DefaultHookTimeout`; bounded drain; fail the owning operation and retain
  ownership when a hook does not quiesce (`extensions.md:681-689`). Drain policy
  option types are NO-SPEC.

### WS2-C13: Documentation reconciliation

- Depends on: C7. Size: S.
- Deliverables: `extensions.md:608-631`, `src/AgentKit.Hooks/README.md`, hooks
  skill, the scope-reconciliation note in `agent-runtime.md`.

## Totals

2a: S 1, M 7. 2b: M 4, S 1. Confidence medium: the two unresolved design points
(context scope, capture/restore ownership) can each add a chunk if decided late.
