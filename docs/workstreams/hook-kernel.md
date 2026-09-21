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
- [x] WS2-C4b registration catalog, instance factory, profile selector
- [x] WS2-C5 new `DispatchAsync` overload and conformance suite
- [x] WS2-C6 migrate `DefaultAgentLoop` and `AgentHookPointDefinitions`
- [x] WS2-C7 remove legacy kernel surface
- [x] WS2-C8 composition validation for hook services
- [x] WS2-C9 `AgentDefinition.HookProfile`
- [x] WS2-C10 named hook profiles
- [x] WS2-C11 diagnostic sinks
- [x] WS2-C12 timeout and quiescence
- [x] WS2-C13 documentation reconciliation

## Verified current state

| Area                     | State   | Evidence                                                                                                                                                               |
| ------------------------ | ------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Kernel dispatch          | EXISTS  | `IHookDispatcher.DispatchAsync<THook,TEventArgs>(HookPointDefinition, HookDispatchContext, …)` in `AgentKit.Abstractions`; `DefaultHookDispatcher` in `AgentKit.Hooks` |
| Legacy surface           | REMOVED | `IHook`, `HookPriority`, `HookDispatchScope`, old dispatcher overload absent from `src/`                                                                               |
| Catalog and activation   | EXISTS  | `HookRegistrationCatalog`, `ServiceProviderHookInstanceFactory`, `HookActivationScope`, `HookRegistrationBinding`                                                      |
| Profiles and diagnostics | EXISTS  | `HookProfileOptions`, `AddHookProfile`, `DefaultHookProfileSelector`, `HookDiagnosticDispatcher`, `AddHookDiagnosticSink`                                              |
| Loop integration         | EXISTS  | `DefaultAgentLoop` opens `HookActivationScope` per run; dispatches via `AgentHookPointDefinitions`                                                                     |
| Composition              | EXISTS  | `HookCompositionValidator`; codes such as `agentkit.hook-dispatcher.missing`, `agentkit.hook-profile.unavailable`                                                      |
| Definition plumbing      | EXISTS  | `AgentDefinition.HookProfile`, `AgentLoopRunRequest.HookProfile`, engine passes profile in `AgentEngineRuntime`                                                        |
| Conformance              | EXISTS  | `tests/AgentKit.Conformance/HookDispatcherConformanceTests.cs` against `DefaultHookDispatcher`                                                                         |

Remaining gaps called out elsewhere: reload-boundary catalog refresh is
configured but not yet honored at runtime; bounded post-timeout drain beyond
cooperative cancellation awaits explicit drain-policy types.

## Hidden prerequisites

1. ~~Hook scope reconciliation~~ — done; documented in `extensions.md` and
   `agent-runtime.md`.
2. ~~Identifier generators, binding, point registry, composition fakes~~ —
   landed in C4–C8.
3. ~~Profile, diagnostics, timeout~~ — landed in C9–C12.
4. `examples/CodingAgent` references `AgentKit.Hooks` but has no hook sample
   code yet.
5. Catalog reload at `HookReloadBoundary` remains configuration-only until a
   host refresh path consumes it.

## Spec coverage

| Contract                                                                                      | Spec                                   | Status                                                                |
| --------------------------------------------------------------------------------------------- | -------------------------------------- | --------------------------------------------------------------------- |
| identities                                                                                    | `extensions.md:49-63`                  | SPEC                                                                  |
| `HookRegistrationDescriptor`                                                                  | `extensions.md:72-82`                  | SPEC; `HookOrder`, `HookLifetime`, `HookReentrancyPolicy` NO-SPEC     |
| `HookCatalogSnapshot`, `HookDispatchMetadata`, `HookInvocationContext`, `HookDispatchContext` | `extensions.md:84-105`                 | SPEC (scope conflict)                                                 |
| `IHookInvocationTracker`, `IHookActivationLease`, `IHookInstanceFactory`                      | `extensions.md:107-128`                | SPEC; attempt/result/resolution types NO-SPEC                         |
| `AgentHookEventArgs`                                                                          | `extensions.md:130-138`                | SPEC (properties); capture/restore NO-SPEC                            |
| example tool point args                                                                       | `extensions.md:157-207`                | SPEC but uses WS4 types                                               |
| source, profile selector, order resolver, catalog                                             | `extensions.md:219-244`                | SPEC; request/result types NO-SPEC                                    |
| `HookPointDefinition<,>`, `HookInvoker<,>`, `IHookDispatcher`                                 | `extensions.md:246-273`                | SPEC; `HookPointKind`, validator members NO-SPEC                      |
| diagnostic sink and dispatcher                                                                | `extensions.md:283-295`                | SPEC; `HookInvocationDiagnostic` NO-SPEC                              |
| `HookDispatcher` ctor                                                                         | `extensions.md:340-345`                | SPEC (class name differs from `DefaultHookDispatcher`)                |
| `AgentHookOptions`, `HookProfileOptions`, `ServiceExtensions`                                 | `extensions.md:373-437`                | IMPLEMENTED; catalog reload boundary configured, refresh path pending |
| `AgentDefinition.HookProfile`                                                                 | `composition-and-configuration.md:173` | SPEC                                                                  |
| composition requirements                                                                      | `extensions.md:523-533`                | prose; diagnostic codes NO-SPEC                                       |

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
