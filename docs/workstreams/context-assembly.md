# WS9: Context assembly pipeline

Goal: `DefaultContextAssembler` becomes a keyed, contributor-driven pipeline:
ordered `IContextContributor`s produce trust-tagged candidates, a budget
allocator reserves mandatory content first, a manifest records what was
included, transformed, or omitted, instructions resolve from typed
`InstructionSource`s, history preparation and tool snapshots become replaceable
collaborators, and project instruction files plus skill inventories are
contributed through the same pipeline.

Owning documents: [Context](../architecture/context.md),
[Context assembly and instructions](../concepts/context-assembly-and-instructions.md),
[Messages and history](../architecture/messages-and-history.md).

## Progress

- [x] WS9-C1 contributor and manifest value contracts
- [ ] WS9-C2 budget allocation contracts and default allocator
- [ ] WS9-C3 keyed `AddAgentContext`, `ContextAssemblerServices`, contributors
      run
- [ ] WS9-C4 `InstructionSource` and definition migration
- [ ] WS9-C5 `IHistoryPipeline` and `IToolSnapshotProvider`
- [ ] WS9-C6 `AgentKit.Context.Project` leaf
- [ ] WS9-C7 skill inventory contributor
- [ ] WS9-C8 context hooks and validator check

## Verified current state

| Item                                                                                                                                                                                                                                                                                                                                                                                                                   | State                      | Evidence                                                                                  |
| ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------- | ----------------------------------------------------------------------------------------- |
| `IContextContributor`, `ContextContributionRequest`, `ContextContribution`, `ContextCandidate`, `ContextManifest(+Entry, Disposition)`, `IInstructionResolver`, `IHistoryPipeline`, `IToolSnapshotProvider`, `IContextBudgetAllocator`, `ContextBudget`, `AgentContextOptions`, `ContextOverflowBehavior`, `ContextCompactionCapability`, `ModelRequestContext`, `InstructionSource`, `ContextContributorRegistration` | MISSING                    | –                                                                                         |
| `ContextCandidateKind`, `ContextTrust`, `ContextScope`, `ContextCostEstimate`, `ContextFreshness`, `ContextDiagnostic(+Severity)`, `ContextEvaluationFrequency`, `ContextSourceKey/Namespace/Version/Reference`, `ContextContributorCatalogVersion`                                                                                                                                                                    | EXISTS-UNWIRED             | `src/AgentKit.Abstractions/Context/` (21 files)                                           |
| `DefaultContextAssembler`                                                                                                                                                                                                                                                                                                                                                                                              | EXISTS-AS-REDUCED, public  | ctors `()`/`(ILogger)` at `DefaultContextAssembler.cs:45,56,64`; no contributors          |
| `IContextAssembler`, `ContextAssemblyRequest`                                                                                                                                                                                                                                                                                                                                                                          | EXISTS-AS-REDUCED-STAND-IN | `IContextAssembler.cs:12-20`, `ContextAssemblyRequest.cs:17-26` (evidence ctor at `:115`) |
| keyed registration                                                                                                                                                                                                                                                                                                                                                                                                     | MISSING                    | `AddAgentContext()` is `TryAddSingleton` (`Context/ServiceExtensions.cs:15-18`)           |
| `AgentDefinition.Instructions : ImmutableArray<AgentMessage>`                                                                                                                                                                                                                                                                                                                                                          | CONFIRMED                  | `AgentDefinition.cs:280`; consumed `DefaultAgentLoop.cs:739`                              |
| project-file discovery, skill contributor                                                                                                                                                                                                                                                                                                                                                                              | MISSING                    | `Tools.Skill` has `ISkillCatalogContextSource.cs` only                                    |
| loop call                                                                                                                                                                                                                                                                                                                                                                                                              | –                          | `services.Context.AssembleAsync` at `DefaultAgentLoop.cs:760`; request built `:730-758`   |

Test doubles: `IContextAssembler` (`Loop.Tests/RecordingContextAssembler.cs`,
`Test.Shared/UnsupportedContextAssembler.cs`, inline ×3); `IContextContributor`
none.

## Hidden prerequisites

1. `AgentDefinition.Components.Context` (WS18); interim key by `LoopKey` in
   `AgentRunServicesFactory.cs:30-34`.
2. `HookDispatchContext` (WS2) for `ContextAssemblyRequest.Hooks` and
   `IHistoryPipeline.PrepareAsync`; ship reduced overloads.
3. `ContextCompactionCapability` needs session and budget capabilities
   (WS10/WS11); defer to WS10-C6.
4. Making `DefaultContextAssembler` internal breaks two test files; add
   `InternalsVisibleTo` for `AgentKit.Context.Tests` and switch loop tests to DI
   or `UnsupportedContextAssembler`.
5. The token estimator lives in the loop (`EstimateTokens`); extract into
   Context and share with WS10.
6. `AgentKit.Context.Project` needs the file-system contracts in
   `Abstractions/Host/`.

## Spec coverage

| Contract                                                                                                                                                                                                                   | Spec                                          |
| -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------- |
| enums, value types, `ContextCandidate`                                                                                                                                                                                     | `context.md:~120-195`                         |
| `ContextContributionRequest`, `ContextContribution`, `IContextContributor`                                                                                                                                                 | `:196-218`                                    |
| `ContextAssemblyEvidence`, full `ContextAssemblyRequest`                                                                                                                                                                   | `:220-240`                                    |
| `ContextManifestEntry`, `ContextManifest`, `ModelRequestContext`                                                                                                                                                           | `:242-274`                                    |
| `IContextAssembler`, `IContextBudgetAllocator`, `ContextCompactionCapability`                                                                                                                                              | `:283-302`                                    |
| `ContextAssemblerServices`, `DefaultContextAssembler` ctor; `AgentContextOptions`; DI                                                                                                                                      | `:322-336,418-460`                            |
| `IHistoryPipeline`, `HistoryView`, prepared/rejected history, preparation request                                                                                                                                          | `messages-and-history.md:549-600`             |
| `ContextBudget`, `ContextBudgetPlan`, `ContextBudgetRequest`, `ContextManifestDisposition`, `InstructionSource`, `IInstructionResolver`, `IToolSnapshotProvider`, `ContextContributorRegistration`, `ModelRequestSettings` | NO-SPEC                                       |
| project instruction discovery                                                                                                                                                                                              | NO-SPEC (prose in the coding-harness profile) |

## Chunks

### WS9-C1: Contributor and manifest value contracts

- Depends on: –. Risk: ADDITIVE. Size: M.
- Deliverables: `Abstractions/Context/ContextCandidate.cs`,
  `ContextContribution.cs`, `ContextContributionRequest.cs`,
  `IContextContributor.cs`, `ContextManifest.cs`, `ContextManifestEntry.cs`,
  `ContextManifestDisposition.cs`, `ContextOverflowBehavior.cs`; guard tests.
  Snapshot: Abstractions.

### WS9-C2: Budget allocation contracts and default allocator

- Depends on: C1. Risk: ADDITIVE. Size: S.
- Deliverables: `ContextBudget.cs`, `ContextBudgetRequest.cs`,
  `ContextBudgetPlan.cs`, `IContextBudgetAllocator.cs`;
  `DefaultContextBudgetAllocator.cs` (mandatory first, priority order,
  `ReservedOutputTokens`, safety margin), `AgentContextOptions.cs`, snapshot;
  token estimator moved from the loop. Snapshot: Abstractions, Context.

### WS9-C3: Keyed `AddAgentContext` and contributor execution

- Depends on: C1, C2. Risk: DENSE-MODIFY `DefaultContextAssembler.cs:45-120`,
  `Context/ServiceExtensions.cs:15-18`. Size: M.
- Deliverables: assembler becomes internal with ctor
  `(ContextAssemblerServices, TimeProvider, ILogger)`; internal
  `ContextAssemblerServices` (contributors, allocator; later collaborators added
  in C5); keyed `AddAgentContext(key, configure)` with the un-keyed overload
  forwarding to a default key;
  `AddContextContributor<T>(key, ContextContributorRegistration)`,
  `ReplaceContextBudgetAllocator<T>(key)`; contributors run in registration
  order filtered by frequency with trust enforced; manifest attached to the
  request context (additive nullable property). Snapshots: Context,
  Abstractions.

### WS9-C4: `InstructionSource` and definition migration

- Depends on: –. Risk: ADDITIVE (new `InstructionSources` property; existing
  `Instructions` becomes a projection of literal sources). Size: M.
- Deliverables: `InstructionSource.cs`, `LiteralInstructionSource.cs`,
  `IInstructionResolver.cs`, resolution results; `DefaultInstructionResolver`;
  definition ctor overload. Full replacement deferred as a documented break.
  Snapshot: Abstractions.

### WS9-C5: `IHistoryPipeline` and `IToolSnapshotProvider`

- Depends on: C3. Risk: ADDITIVE. Size: M.
- Deliverables: `IHistoryPipeline.cs` (reduced overload without hooks),
  `HistoryPreparationRequest/Result`, `PreparedHistory`, `RejectedHistory`,
  `IToolSnapshotProvider.cs`; `DefaultHistoryPipeline` (moves `RepairHistory`
  out of the assembler), `StaticToolSnapshotProvider` over
  `ToolCatalogSnapshot`. Snapshot: Abstractions, Context.

### WS9-C6: `AgentKit.Context.Project` leaf

- Depends on: C3, `AgentKit.FileSystem`. Risk: ADDITIVE new project. Size: M.
- Deliverables: `ProjectInstructionContributor` (trust `Workspace`, discovers
  AGENTS.md/CLAUDE.md through protected file-system contracts),
  `ProjectInstructionOptions` (filenames, roots, max bytes),
  `AddProjectInstructionContributor(key, …)`; tests over
  `AgentKit.FileSystem.InMemory`; architecture-test leaf list; snapshot file;
  `AgentKit.slnx`. Write the discovery rules into `context.md` first (NO-SPEC).

### WS9-C7: Skill inventory contributor

- Depends on: C3. Risk: ADDITIVE. Size: S.
- Deliverables: `SkillInventoryContextContributor` over `ISkillCatalog`;
  `AddSkillTool` registers tool and contributor atomically under the assembler
  key. Snapshot: Tools.Skill.

### WS9-C8: Context hooks and validator check

- Depends on: WS2, C3. Risk: ADDITIVE. Size: S.
- Deliverables: `AgentHookPoints.ContextAssembled` and event args (omit or
  tighten only); contributor catalog cycle/duplicate diagnostics in the
  validator.

## Totals

S 3, M 5. Confidence medium: nine referenced types have no C# bodies, and the
definition `Instructions` migration is intentionally left additive.
