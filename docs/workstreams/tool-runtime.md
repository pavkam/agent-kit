# WS4: Tool runtime switch

Goal: replace the reduced `ITool` / `IToolCatalog.TryResolve` /
`AllowListToolAuthorizer` / `DefaultToolInvoker` path with the specified
runtime: per-tool `IToolInvoker`, a capture coordinator over the already
existing discovery/merge machinery, `IToolExecutor` with resolution, validation,
authorization through the security authority selector, scheduling, retries,
normalization, projection, and toolsets on the agent definition.

Owning documents: [Tools](../architecture/tools.md),
[Tool-call lifecycle](../concepts/tool-call-lifecycle.md),
[Tool scheduling and concurrency](../concepts/tool-scheduling-and-concurrency.md),
[Tool errors, retries, results](../concepts/tool-errors-retries-and-results.md).

## Progress

- [x] WS4-C1 executor and batch contracts
- [x] WS4-C2 loop and engine consume `IToolExecutor` through a legacy adapter
- [x] WS4-C3 catalog capture coordinator
- [x] WS4-C4 spec-shaped `IToolInvoker` with `ITool` bridge
- [x] WS4-C5a `DefaultToolExecutor` single-call pipeline
- [ ] WS4-C5b scheduler
- [ ] WS4-C5c retry policy
- [ ] WS4-C5d oversized result spill
- [ ] WS4-C6 hooks move into the executor; `IToolResultHook`
- [ ] WS4-C7a `AgentDefinition.Toolsets` and executor key
- [ ] WS4-C7b derive tool definitions from capture; remove `Tools`/`ToolChoice`
- [ ] WS4-C8 spec registration surface
- [ ] WS4-C9a/b/c migrate 16 tool packages
- [ ] WS4-C10a delete legacy authorizer, catalog, invoker
- [ ] WS4-C10b promote coordinator to `IToolCatalog.CaptureAsync`
- [ ] WS4-C11 Simple over toolsets
- [ ] WS4-C12 first-party `IWebSearchProvider`
- [ ] WS4-C13 documentation

## Verified current state

| Item                                                                                                                                                                                                                                  | State                              | Evidence                                                                                                                                                                                                                |
| ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `AddAgentTools` wires `ToolCatalog` over `IEnumerable<ITool>`, `AllowListToolAuthorizer`, `DefaultToolInvoker`                                                                                                                        | EXISTS-AND-USED                    | `src/AgentKit.Tools/ServiceExtensions.cs:282-304`; users `AgentKit.Simple/AgentEngineBuilderExtensions.cs:68,740`, `examples/CodingAgent/AgentRuntime.cs:128`                                                           |
| `IToolInvoker` (legacy orchestrator `Task<ResolvedToolInvocation> InvokeAsync(ToolCallRequest)`)                                                                                                                                      | EXISTS, wrong shape, double-booked | `Abstractions/Tools/IToolInvoker.cs:70-80`; loop caller `DefaultAgentLoop.cs:1447`; also held by new-runtime `IToolInvokerLease`, `ToolProviderBindings`, `StaticToolProvider`; spec is per-tool (`tools.md:1061-1066`) |
| `ITool` implementations                                                                                                                                                                                                               | 18 across 16 packages              | all `sealed`, all take un-keyed `ISecurityAuthority`; `src/AgentKit.Tools.FileSystem/` is an empty directory                                                                                                            |
| `IToolAuthorizer`, `AllowListToolAuthorizer`                                                                                                                                                                                          | EXISTS-AS-REDUCED-STAND-IN         | `IToolAuthorizer.cs:91-97`; consumer `DefaultToolInvoker.cs:44,172`                                                                                                                                                     |
| legacy `IToolCatalog.TryResolve`                                                                                                                                                                                                      | EXISTS-AS-REDUCED-STAND-IN         | `IToolCatalog.cs:130-138`; name collides with spec `IToolCatalog.CaptureAsync` (`tools.md:793-798`)                                                                                                                     |
| `DefaultToolInvoker`                                                                                                                                                                                                                  | EXISTS-AS-REDUCED-STAND-IN         | `DefaultToolInvoker.cs:27`; always `ToolResultProjectionPolicyReference.Default`                                                                                                                                        |
| loop tool execution                                                                                                                                                                                                                   | sequential, no scheduling/retries  | `DefaultAgentLoop.cs:1380`; no `ToolSchedulingMode`, `ExecutionHints`, `Retryable` reference                                                                                                                            |
| `ToolRegistrationCatalog`, `ToolCatalogDiscovery`, `ToolDiscoveryCapture`, `ToolCatalogMerger`, `ToolCatalogCapture`, `StaticToolProvider`, `RejectingToolCatalogMergePolicy`, `AddToolset/AddToolProvider/AddStaticToolProvider/...` | EXISTS-UNWIRED                     | internal `ToolCatalogCoordinator` (C3) chains discovery, merge, schema preflight into `TransferToCatalog`; only tests resolve it; unreachable via `IToolCatalog` until C10b                                             |
| `IToolResultProjectionPolicyCatalog`                                                                                                                                                                                                  | EXISTS-UNWIRED                     | registered `ServiceExtensions.cs:313-320`, never resolved                                                                                                                                                               |
| `AgentDefinition.Tools`, `ToolChoice`; no `Toolsets`, no executor key                                                                                                                                                                 | CONFIRMED                          | `AgentDefinition.cs:36-37,95-96,295-320`; mirrored in `AgentLoopRunRequest.cs:263-269`, `ContextAssemblyRequest.cs:179`                                                                                                 |
| `ToolsetReference`, `ToolsetPublication`, `ToolDiscoveryRequest.Toolsets`                                                                                                                                                             | EXISTS (values)                    | `Abstractions/Tools/`                                                                                                                                                                                                   |
| `IToolExecutor`, `ToolBatchResult`, `ToolExecutionCapability`, `ToolInvocationContext`, `IToolProgressReporter` (C1 contracts)                                                                                                        | EXISTS-AND-USED via legacy adapter | loop resolves `IToolExecutor` → `LegacyToolInvokerExecutor` (`ServiceExtensions.cs:339-342`); spec-shaped `DefaultToolExecutor` registered but not default (C5a)                                                        |
| `IToolScheduler`, `ToolBatchFailureMode`, `UnknownSchedulingMode`                                                                                                                                                                     | EXISTS-UNWIRED                     | C1 contracts only; scheduler C5b                                                                                                                                                                                        |
| `DefaultToolExecutor`, `ToolCallResolver`, `ToolArgumentValidator`, `ToolResultNormalizer`, `ToolResultProjector`, `ToolInvocationSecurityBinding`                                                                                    | EXISTS-REGISTERED                  | `AddAgentTools` TryAdd (`ServiceExtensions.cs:326-330`); sequential resolve → validate → authorize → invoke → normalize; no scheduler/retries/recorder/hooks; projector not invoked by executor yet                     |
| `IToolCallRecorder`, `IToolEventSink`, `IToolExecutionPolicy(+Selector)`, `PreparedToolCall`, `ToolRuntimeOptions`, `ToolRetryPolicy`                                                                                                 | MISSING                            | C5b–d, C6, C8                                                                                                                                                                                                           |
| `ValidatedToolCall`, `ResolvedToolCall`, `IToolResolver`, `IToolArgumentValidator`, `IToolResultNormalizer`, `IToolResultProjector`                                                                                                   | EXISTS                             | `Abstractions/Tools/`; first-party implementations in `AgentKit.Tools` (C5a)                                                                                                                                            |
| `HookDispatchContext`, `IToolResultHook`, `ToolResultHookEventArgs`                                                                                                                                                                   | MISSING                            | only `BeforeToolInvocationEventArgs(ToolCallPart)`                                                                                                                                                                      |
| first-party `IWebSearchProvider`                                                                                                                                                                                                      | MISSING                            | consumer `WebSearchTool.cs:53`                                                                                                                                                                                          |
| Simple `WithTools`                                                                                                                                                                                                                    | MISSING                            | `UseWorkspace` registers 6 tools; `WithDelegation`; advertising via `IEnumerable<ITool>` at `SimpleAgentPlan.cs:277`                                                                                                    |

Test doubles: `IToolInvoker` 3 class fakes (`Loop.Tests/FakeToolInvoker.cs`,
`Test.Shared/CaptureTestToolInvoker.cs`, `AgentRunServicesTests.cs`) plus lambda
uses across `Test.Shared`, `Tools.Tests`, `Conformance`,
`CompositionTestData.cs:92`, `Conversations.Tests`; `IToolAuthorizer` 3; `ITool`
10 (`FakeTool`, `StubTool` in Read/Write tests, Simple, Conformance);
`ToolInvocationRequest` referenced in 20 test projects; legacy `IToolCatalog` 1
plus conformance fixture; new-runtime contracts one callback fake each in
`Test.Shared`.

## Hidden prerequisites

1. `IToolInvoker` is double-booked: reshape (C4) must precede any spec-shaped
   executor.
2. `IToolCatalog` name collision: the coordinator stays internal until the
   legacy type is deleted (C10a → C10b).
3. No durable accepted-call session entry exists for `IToolCallRecorder`
   (`tools.md:1094-1105`); storage shape is NO-SPEC and belongs to sessions.
   Interim: the loop's post-batch commit.
4. `AgentKit.Tools` and `AgentKit.Loop` may reference only Abstractions and
   Observability; all collaborators (`ISecurityAuthoritySelector`,
   `IHookDispatcher`, `IArtifactCoordinator`) already live there. Leaf tool
   packages add an `AgentKit.Tools` reference (leaf → runtime is permitted).
5. WS1 may change `AgentRunServices.Tools` first; coordinate. WS2
   `HookDispatchContext` is missing, so the executor takes `IHookDispatcher`
   directly until then. Executor-level authorization can use
   `ISecurityAuthoritySelector` from day one.
6. Spec `ToolInvocationContext` lacks `ExecutionIdentity`,
   `SecurityAuthorizationContext`, `SessionProfileSnapshot?` that Plan/Todo and
   every tool's inner `SecurityRequest` need; decide before C1.
7. New `ToolLog` events need unique IDs (`LoggerMessageEventIdTests`).

## Spec coverage

| Contract                                                                                                                          | Spec                                               |
| --------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------- |
| per-tool `IToolInvoker`                                                                                                           | `tools.md:1061-1066`                               |
| `IToolScheduler`, `IToolExecutor`, `ToolExecutionCapability`, `ToolExecutionPolicyBinding`                                        | `tools.md:1068-1092`                               |
| `IToolCallRecorder`, projection catalog, projector, event sink                                                                    | `tools.md:1094-1127`                               |
| `ToolInvocationContext`, `ValidatedToolCall`, `PreparedToolCall`, `ResolvedToolCall`, spec `ToolCallRequest`                      | `tools.md:528-603`                                 |
| providers, captures, registration catalog, spec `IToolCatalog`, leases, resolver                                                  | `tools.md:769-821`                                 |
| `IToolArgumentValidator`, `IToolExecutionPolicy(+Selector)`                                                                       | `tools.md:851-873`                                 |
| `ToolExecutor` dependency shape; `ToolRuntimeOptions`, `ToolsetOptions`, `ServiceExtensions`                                      | `tools.md:1176-1190,1231-1391`                     |
| `AgentDefinition.Toolsets`, executor key                                                                                          | `composition-and-configuration.md:160,178,674-677` |
| scheduling algorithm; retry precedence                                                                                            | concepts, prose only                               |
| `IToolResultHook`, `ToolResultHookEventArgs`, new `BeforeToolInvocationEventArgs`                                                 | `extensions.md:160-205`                            |
| `ToolBatch`, `ToolBatchResult`, `ToolBatchFailureMode`, `UnknownSchedulingMode`, recorder entry, web-search provider, `WithTools` | NO-SPEC                                            |

## Chunks

### WS4-C1: Executor and batch contracts

- Depends on: –. Risk: ADDITIVE. Size: M.
- Deliverables: `Abstractions/Tools/IToolExecutor.cs`, `ToolBatch.cs`,
  `ToolBatchResult.cs`, `ToolExecutionCapability.cs`, `ToolBatchFailureMode.cs`,
  `UnknownSchedulingMode.cs`, `ToolInvocationContext.cs`,
  `IToolProgressReporter.cs`, `IToolScheduler.cs`; guard tests. Snapshot:
  Abstractions.
- Open: ship without the `HookDispatchContext` parameter until WS2; whether
  `ToolInvocationContext` carries `SessionProfileSnapshot`.

### WS4-C2: Loop and engine consume `IToolExecutor` through a legacy adapter

- Depends on: C1. Risk: CONTRACT-BREAK (`AgentRunServicesTests` 10 sites,
  `DefaultAgentLoopTests` 2 + `FakeToolInvoker`,
  `DefaultConversationSessionTests.cs:1928`, `ServiceExtensionsTests.cs:115`,
  `CompositionTestData.cs:92`, `CaptureTestToolInvoker.cs`). Size: M.
- Deliverables: `AgentRunServices.Tools : IToolExecutor`;
  `AgentRunServicesFactory.cs:56`, `AgentCompositionValidator.cs:312`,
  `DefaultConversationSession.cs:237,311`; `InvokeToolsAsync`
  (`DefaultAgentLoop.cs:1347-1529`) builds a `ToolBatch`, calls `ExecuteAsync`,
  maps results, keeps budget count and hook dispatch for now;
  `src/AgentKit.Tools/LegacyToolInvokerExecutor.cs` (sequential over
  `DefaultToolInvoker`); `Test.Shared/CaptureTestToolExecutor.cs`,
  `Loop.Tests/FakeToolExecutor.cs`. Snapshots: Abstractions, Loop, Tools,
  Conversations, AgentKit.
- Done when: `services.Tools.InvokeAsync(ToolCallRequest)` has zero production
  callers outside the adapter.

### WS4-C3: Catalog capture coordinator

- Depends on: –. Risk: ADDITIVE. Size: M.
- Deliverables: internal `src/AgentKit.Tools/ToolCatalogCoordinator.cs` chaining
  `ToolCatalogDiscovery.DiscoverAsync` (`:47`), `ToolCatalogMerger.MergeAsync`
  (`:42`), per-descriptor `IToolSchemaEngine.Compile` preflight,
  `ToolDiscoveryCapture.TransferToCatalog` (`:87`); registration; tests for
  release on merge reject, release on preflight reject, cancel between stages,
  single owner.

### WS4-C4: Spec-shaped `IToolInvoker` with `ITool` bridge

- Depends on: C2. Risk: CONTRACT-BREAK (`CaptureTestToolInvoker`,
  `ToolCaptureTestData`, `CallbackToolInvokerLease`, six `Tools.Tests` files,
  three conformance suites, `UnreadableToolInvokerLease`). Size: M.
- Deliverables:
  `IToolInvoker.InvokeAsync(ToolInvocationContext, CT) → ValueTask<ToolInvocationResult>`;
  `DefaultToolInvoker` stops implementing it; internal `ToolInvokerBridge` over
  an `ITool`; `ITool` unchanged for now. Snapshots: Abstractions, Tools.

### WS4-C5a: `DefaultToolExecutor` single-call pipeline

- Depends on: C1, C3, C4. Risk: ADDITIVE plus DENSE-MODIFY `AddAgentTools`.
  Size: L (sequential only; scheduler/retries/spill are C5b–d).
- Deliverables: `DefaultToolExecutor.cs`, `ToolCallResolver.cs` (alias map from
  `ToolCatalogSnapshot.ProviderAliases`), `ToolArgumentValidation.cs` (reuse
  `BoundedToolSchemaEngine`), `ToolResultNormalizer.cs`,
  `ToolResultProjector.cs` (resolves the projection catalog); authorization via
  `ISecurityAuthoritySelector`; pre-invocation rejection yields exactly one
  terminal record; tests: unknown alias, invalid args, denial, invoker throw,
  cancellation.

### WS4-C5b: Scheduler

- Depends on: C5a. Risk: ADDITIVE. Size: M.
- Deliverables: `BarrierSegmentToolScheduler : IToolScheduler` per
  `tool-scheduling-and-concurrency.md:49-64`, reading
  `ToolDescriptor.ExecutionHints`,
  `ToolRuntimeOptions.MaximumParallelInvocations` and `UnknownSchedulingMode`;
  `ReplaceToolScheduler<T>`; tests for segment ordering, same-key serialization,
  limiter, exclusive modes, cancellation mid-segment.
- Open: canonical multi-key sort vs reject.

### WS4-C5c: Retry policy

- Depends on: C5a. Risk: ADDITIVE. Size: M.
- Deliverables: `ToolRetryPolicy.cs` and options using
  `ToolCallResult.Retryable`, `ToolEffects`, `SideEffectCertainty`,
  `ToolError.RetryAfter`, `TimeProvider`; attempt count on
  `ToolInvocationContext.Attempt`; tests per
  `tool-errors-retries-and-results.md:44-58`.

### WS4-C5d: Oversized result spill and truncation evidence

- Depends on: C5a. Risk: ADDITIVE. Size: M.
- Deliverables: `ToolResultNormalizer` spills to `IArtifactCoordinator` when
  resolvable (`ToolResultArtifactContent`), else truncates with
  `ToolResultNormalizationInfo`; `MaximumResultBytes` enforced.
- Open: who supplies the artifact write grant (WS15).

### WS4-C6: Hooks move into the executor; `IToolResultHook`

- Depends on: C5a; WS2 optional. Risk: ADDITIVE plus DENSE-MODIFY
  `DefaultAgentLoop.cs:1423-1445`. Size: M.
- Deliverables: `Abstractions/Hooks/IToolResultHook.cs`,
  `ToolResultHookEventArgs.cs`, `AgentHookPoints.ToolResult`; executor
  dispatches `BeforeToolInvocation` and the result hook; loop loses its tool
  hook block and constructor injection. Snapshots: Abstractions, Loop.

### WS4-C7a: `AgentDefinition.Toolsets` and executor key

- Depends on: C1, WS18-C3 if landed. Risk: ADDITIVE. Size: M.
- Deliverables: `ImmutableArray<ToolsetReference> Toolsets` and
  `ComponentKey<IToolExecutor>? ToolExecutorKey` on the definition (or on
  `Components`/`OptionalCapabilities`); validator rule toolsets ⇔ executor key.
  Snapshot: Abstractions.

### WS4-C7b: Derive tool definitions from capture; remove `Tools`/`ToolChoice`

- Depends on: C3, C5a, C7a. Risk: CONTRACT-BREAK (`new AgentDefinition(` in 6
  test files, `AgentLoopRunRequest.Tools`, `ContextAssemblyRequest.Tools`,
  `DefaultContextAssembler.cs:82`, `DefaultAgentLoop.cs:741,755`). Size: L.
- Deliverables: loop captures the catalog per run and passes
  `snapshot.Tools.ToLlmToolDefinitions()`; delete `AgentDefinition.Tools`,
  `ToolChoice`.
- Open: `ToolChoice` has no spec home.

### WS4-C8: Spec registration surface

- Depends on: C5a, C5b. Risk: ADDITIVE. Size: M.
- Deliverables:
  `AddAgentTools(ComponentKey<IToolExecutor>, Action<ToolRuntimeOptions>?)`,
  `AddTool<TInvoker>(ToolDescriptor, ServiceLifetime)`, `ReplaceTool<TInvoker>`,
  `ReplaceToolExecutor<T>(key)`, `ReplaceToolScheduler<T>`;
  `ToolRuntimeOptions.cs`, `ApplicationToolProvider.cs`; old `AddAgentTools`
  marked obsolete. Snapshot: Tools.
- Open: scope ownership per invocation for scoped lifetimes.

### WS4-C9a/b/c: Migrate 16 tool packages

- Depends on: C4, C8. Risk: DENSE-MODIFY each `ServiceExtensions.cs`; each
  csproj adds an `AgentKit.Tools` reference. Size: M each. C9a
  Read/Write/Edit/Patch/Glob/Search/List; C9b Command/Web/WebSearch/Language;
  C9c Plan (two tools)/Question/Task/Resource/Skill.
- Deliverables per package: implement `IToolInvoker` natively, register via
  `AddTool<T>(Descriptor)` and `AddToolset(ToolsetPublication)` under the
  existing `ToolSourceId`; README; tests move to `ToolInvocationContext`.
  Snapshots: each tool package.

### WS4-C10a: Delete legacy authorizer, catalog, invoker

- Depends on: C9a–c, C11. Risk: CONTRACT-BREAK (`DefaultToolInvokerTests`,
  `ToolCatalogTests`, `AllowListToolAuthorizerTests`, `FakeTool`,
  `ToolCatalogConformanceFixture`, `ToolCatalogConformanceTests`, Simple tests
  using `AgentToolsOptions`, `StubTool`s). Size: M.
- Deliverables: delete `ITool`, `IToolAuthorizer`, legacy `IToolCatalog`,
  `ToolAuthorization*`, `ResolvedToolInvocation`, legacy `ToolCallRequest`,
  `AllowListToolAuthorizer`, `ToolCatalog`, `DefaultToolInvoker`,
  `LegacyToolInvokerExecutor`, `ToolInvokerBridge`; `AgentToolsOptions`
  allow-list members; `examples/CodingAgent/AgentRuntime.cs:128,199`. Snapshots:
  Abstractions, Tools, Simple.
- Done when: no `ITool` symbol in the repository.

### WS4-C10b: Promote coordinator to `IToolCatalog.CaptureAsync`

- Depends on: C10a. Risk: ADDITIVE. Size: S.
- Deliverables: spec `Abstractions/Tools/IToolCatalog.cs`;
  `ToolCatalogCoordinator : IToolCatalog`; `ReplaceToolCatalog<T>`.

### WS4-C11: Simple over toolsets

- Depends on: C7a, C8, C9a. Risk: DENSE-MODIFY
  `AgentEngineBuilderExtensions.cs:68,367-385,595-604,740,761-770`,
  `SimpleAgentPlan.cs:223,277-296`, `SimpleAgentDefinitionSource.cs:16-22`.
  Size: M.
- Deliverables: `WithTools(params ToolsetKey[])`; `UseWorkspace` publishes an
  `agentkit.simple.workspace` toolset; `WithDelegation` allow-list becomes
  toolset membership. Snapshot: Simple.

### WS4-C12: First-party `IWebSearchProvider`

- Depends on: –. Risk: ADDITIVE. Size: M.
- Deliverables: `Tools.WebSearch/NetworkWebSearchProvider.cs` over
  `INetworkTransport` with a required endpoint option (no default);
  `AddNetworkWebSearchProvider`; tests with `ScriptedNetworkTransport`.
- Open: which endpoint family is normative.

### WS4-C13: Documentation

- Depends on: all. Size: S.
- Deliverables: `tools.md:471,1427-1431`, 16 READMEs, tools skill.

## Totals

S 2, M 14, L 2. Confidence medium-low: the two name collisions, the missing
recorder storage shape, and the `ToolInvocationContext` identity gap can each
add a chunk.
