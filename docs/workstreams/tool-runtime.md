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
- [x] WS4-C5b scheduler
- [x] WS4-C5c retry policy
- [x] WS4-C5d oversized result spill (truncation path; artifact spill deferred
      to WS15)
- [x] WS4-C6 hooks move into the executor; `IToolResultHook` (executor
      dispatches `BeforeToolInvocation` and `ToolResult`; loop hook block
      removed)
- [x] WS4-C7a `AgentDefinition.Toolsets` and executor key
- [x] WS4-C7b derive tool definitions from capture; remove `Tools`/`ToolChoice`
- [x] WS4-C8 spec registration surface (`AddToolDiscoveryRuntime`,
      `AddTool`/`ReplaceTool`, keyed
      `AddAgentTools(ComponentKey<IToolExecutor>)`)
- [x] WS4-C9a migrate workspace file tools (List, Read, Write, Edit, Patch,
      Glob, Search)
- [x] WS4-C9b/c migrate remaining tool packages
- [x] WS4-C10a delete legacy authorizer, catalog, invoker
- [x] WS4-C10b promote coordinator to `IToolCatalog.CaptureAsync`
- [x] WS4-C11 Simple over toolsets (`WithTools`, `UseWorkspace` toolset
      publication, keyed `DefaultToolExecutor`; model advertising still legacy
      until C7b)
- [x] WS4-C12 first-party `IWebSearchProvider` (`NetworkWebSearchProvider` over
      `INetworkNameResolver` and `INetworkTransport` under per-attempt search,
      resolution, and send grants; no `HttpClient`; closes the web-search gap
      the
      [WS7-C11](provider-runtime.md#ws7-c11-provider-egress-through-inetworktransport)
      provider-egress closure left open)
- [x] WS4-C13 documentation
- [x] WS4-C14 recorder, execution policy, and event sink (closes the WS20 gap)

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
| `Toolsets`, `OptionalCapabilities.ToolExecutor`; loop derives model tools from run catalog capture                                                                                                                                    | EXISTS-AND-USED                    | C7b removed `AgentDefinition.Tools`/`ToolChoice`; `DefaultAgentLoop` opens run capture when toolsets are authored and projects `ToolDescriptor` → `LlmToolDefinition` for context assembly                              |
| `ToolsetReference`, `ToolsetPublication`, `ToolDiscoveryRequest.Toolsets`                                                                                                                                                             | EXISTS (values)                    | `Abstractions/Tools/`                                                                                                                                                                                                   |
| `IToolExecutor`, `ToolBatchResult`, `ToolExecutionCapability`, `ToolInvocationContext`, `IToolProgressReporter` (C1 contracts)                                                                                                        | EXISTS-AND-USED via legacy adapter | loop resolves `IToolExecutor` → `LegacyToolInvokerExecutor` (`ServiceExtensions.cs:339-342`); spec-shaped `DefaultToolExecutor` registered but not default (C5a)                                                        |
| `IToolScheduler`, `ToolBatchFailureMode`, `UnknownSchedulingMode`                                                                                                                                                                     | EXISTS-UNWIRED                     | C1 contracts only; scheduler C5b                                                                                                                                                                                        |
| `DefaultToolExecutor`, `ToolCallResolver`, `ToolArgumentValidator`, `ToolResultNormalizer`, `ToolResultProjector`, `ToolInvocationSecurityBinding`                                                                                    | EXISTS-REGISTERED                  | `AddAgentTools` TryAdd (`ServiceExtensions.cs:326-330`); sequential resolve → validate → authorize → invoke → normalize; no scheduler/retries/recorder/hooks; projector not invoked by executor yet                     |
| `IToolCallRecorder`, `IToolEventSink`, `IToolExecutionPolicy(+Selector)`, `PreparedToolCall`, `ToolRuntimeOptions`, `ToolRetryPolicy`                                                                                                 | EXISTS-AND-USED                    | WS4-C14: `DefaultToolExecutor` selects a policy, records the accepted call before invocation, retries under the planned policy, and publishes events; `ToolRuntimeOptions` predates C14                                 |
| `ValidatedToolCall`, `ResolvedToolCall`, `IToolResolver`, `IToolArgumentValidator`, `IToolResultNormalizer`, `IToolResultProjector`                                                                                                   | EXISTS                             | `Abstractions/Tools/`; first-party implementations in `AgentKit.Tools` (C5a)                                                                                                                                            |
| `HookDispatchContext`, `IToolResultHook`, `ToolResultHookEventArgs`                                                                                                                                                                   | EXISTS-UNWIRED                     | contracts + hook kernel (C6 partial); executor dispatch and loop removal open                                                                                                                                           |
| first-party `IWebSearchProvider`                                                                                                                                                                                                      | EXISTS-AND-USED (network leaf)     | `NetworkWebSearchProvider`, `AddNetworkWebSearchProvider`; sends only through `INetworkTransport`                                                                                                                       |
| Simple `WithTools`                                                                                                                                                                                                                    | EXISTS (partial)                   | `AgentEngineBuilderExtensions.WithTools`, `UseWorkspace` selects `SimpleWorkspaceToolsets`; LLM tool list still from `IEnumerable<ITool>` until C7b                                                                     |

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
3. ~~No durable accepted-call session entry exists for `IToolCallRecorder`
   (`tools.md:1094-1105`); storage shape is NO-SPEC and belongs to sessions.
   Interim: the loop's post-batch commit.~~ Resolved by C14:
   `ToolCallAcceptedSessionEntry` and `ToolCallTerminalSessionEntry` with
   `AgentKit.Session` codecs.
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
  `ToolRuntimeOptions.cs`, `ApplicationToolProvider.cs`; the old `AddAgentTools`
  stays unmarked until C10a deletes it. Snapshot: Tools.
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
- Landed: the provider takes the authority selector, grant store, audit
  dispatcher, resolver, transport, four replaceable identity generators, a
  `TimeProvider`, validated options, and an optional logger; it owns no
  `HttpClient` and the registration creates none. Per attempt it validates the
  tool-issued grant against the exact request, consumes it with required audit
  under its own enforcement intent, then obtains a resolution grant, resolves,
  obtains a send grant bound to the resolved addresses, and sends one bodyless
  `GET` with `Accept: application/json`, zero redirects, a streamed response
  bound (`MaximumResponseBytes`), the connect/resolution timeout clamped to the
  remaining deadline, and a host-configured classification (default
  `Confidential`). The query appears in grants and audit only through hashed
  fingerprints and a query-fingerprinted resource, and never in a log, tag, or
  refusal message. Authority, enforcement, or audit that is missing, throws, or
  refuses yields `WebSearchDenied` (tool status `Denied`, effect certainty
  definitely-not-performed); deadline expiry yields `WebSearchFailed`; caller
  cancellation propagates. Redirects fail the attempt. Fixed in passing:
  `published_at` was never parsed because the wire property name was not mapped.
  Tests use `HandlerNetworkTransport`, `CallbackNetworkTransport`, and
  `FixedAddressNameResolver` from `AgentKit.Test.Shared`.
- Remaining limits: no credential or API-key support (the provider sends no
  authentication); the endpoint family is still host-chosen JSON (`q`,
  `maximum_results`, `freshness`, `domains`); the attempt has structured log
  events (`34410`-`34413`) but no activity or meter of its own, because the
  `execute_tool` activity and the network leaf's own signals already cover it.

### WS4-C13: Documentation

- Depends on: all. Size: S.
- Deliverables: `tools.md:471,1427-1431`, 16 READMEs, tools skill.

### WS4-C14: Recorder, execution policy, and event sink

- Depends on: C5a–c, C8; WS12 durability boundary. Risk: CONTRACT-BREAK
  (`ToolExecutionCapability`, `ToolBatchEntry`, `ToolInvocationContext`,
  `DefaultToolExecutor` and scheduler constructors, `ToolRuntimeOptions`
  rename). Size: L.
- Landed: contracts in `AgentKit.Abstractions` (`IToolCallRecorder`,
  `ToolCallRecordResult`, `IToolEventSink` with `ToolEvent`,
  `IToolExecutionPolicy`, `IToolExecutionPolicySelector`, `PreparedToolCall`,
  `ToolExecutionPlan`, `ToolRetryPolicy`, `ToolCallSessionTarget`,
  `IIdempotencyEnforcingToolInvoker`, and the two session entries); the
  executor, scheduler, recorder, dispatcher, default policy, selector, and the
  ten registration helpers in `AgentKit.Tools`; the two entry codecs in
  `AgentKit.Session`, registered by the Json and Sqlite leaves; capability
  wiring in the loop and the MCP server; Simple composes through
  `AddAgentTools(key)`. Tests cover guards, DI replacement and keyed selection,
  ordering, fail-closed recording, retry safety, cancellation, sink isolation,
  log event IDs, activities, metrics, and content absence, and the Simple
  end-to-end test proves an accepted and a terminal record reach a real session
  store.
- Deviations (full list in the
  [tools architecture](../architecture/tools.md#configuration-and-dependency-injection)):
  the recorder takes a `ToolCallSessionTarget`; the terminal session entry is
  content-free evidence; terminal-record failure never changes an outcome; the
  accepted-record lookup is a bounded window; retry confirmation is
  `IIdempotencyEnforcingToolInvoker`; `InvocationTimeout` stays advisory; the
  executor reserves no budget; `MaximumRetryAttempts` is renamed
  `MaximumAttempts`.
- Relationship to durable execution: the loop's `agentkit.loop.tool_call`
  checkpoint stays as the journal's manifest that a requested call entered the
  pipeline; the session accepted entry is the call's authoritative acceptance
  fact. Neither duplicates the other (see
  [`durable-execution.md`](../architecture/durable-execution.md)).

## Remaining limits

- Budget dimensions for attempted, concurrent, retry, result-byte, and
  successful calls are still not reserved by the executor.
- `ToolCallTerminalSessionEntry` carries no content, usage, or extension data; a
  result carrying usage or extensions is refused by the v1 codec and its
  terminal record is reported as not recorded.
- MCP server dispatch needs a loadable session and a store that accepts the
  default lane's append; it fails closed otherwise.
- `InvocationTimeout` is not enforced by the executor.
- A session store whose codec catalog lacks the two tool-call codecs (a custom
  store composed without the Json or Sqlite leaf registrations) rejects the
  accepted append, which fails the call closed rather than invoking it
  unrecorded.

## Totals

S 2, M 14, L 3. Confidence medium-low: the two name collisions, the missing
recorder storage shape, and the `ToolInvocationContext` identity gap can each
add a chunk; the recorder storage shape and the identity gap landed in C14.
