# WS18: Definition and composition validation sweep

Goal: `AgentDefinition` matches the specified shape
(`Components : AgentComponentSelection`,
`OptionalCapabilities : AgentOptionalCapabilitySelection`, `HookProfile`,
`Toolsets`, typed `Instructions`), and `AgentCompositionValidator` enforces
every engine-wide and per-definition requirement listed in `AGENTS.md` and
`composition-and-configuration.md`.

Land C1–C3 **before WS2** so that later workstreams add keys to the final
records instead of interim flat properties. If they do land as flat properties,
C3/C5 become the migration.

Owning document:
[Composition and configuration](../architecture/composition-and-configuration.md)
(records at lines 134–181, validator requirements at 600–698).

## Progress

- [x] WS18-C1 `AgentComponentSelection` and `AgentOptionalCapabilitySelection`
- [ ] WS18-C2 `InstructionSource` (if not landed by WS9-C4)
- [ ] WS18-C3 init-only
      `Components`/`OptionalCapabilities`/`HookProfile`/`Toolsets`
- [ ] WS18-C4 bridge readers in engine, factory, loop
- [ ] WS18-C5 migrate Simple and all construction sites
- [ ] WS18-C6 break: spec constructor, delete interim members
- [ ] WS18-C7 validator engine-wide singular checks
- [ ] WS18-C8 validator per-definition required selections
- [ ] WS18-C9 validator optional-capability and toolset coherence
- [ ] WS18-C10 keyed multiplicity, correspondence,
      `RepresentsCompleteRunnableGraph`
- [ ] WS18-C11 run-profile publication, Simple `With*`, documentation

## Verified current state

### `AgentDefinition` vs spec

| Spec field                                                                                                         | Exists                                                                    |
| ------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------- |
| `Id`, `Revision`, `DisplayName`, `SessionProfile`, `SecurityProfile`, `Models`, `RunDefaults`, `Extensions`        | yes                                                                       |
| `Components : AgentComponentSelection`                                                                             | no; only `ComponentKey<IAgentLoop>? LoopKey` (`AgentDefinition.cs:199`)   |
| `Components.ContinuationPolicy`, `Input`, `Output`, `OutputProcessor`, `Context`, `ModelSelector`, `ModelExecutor` | no; resolved by loop key or un-keyed (`AgentRunServicesFactory.cs:53-65`) |
| `Components.BudgetProfile`                                                                                         | no; raw `ImmutableArray<BudgetLimit> BudgetLimits` (`:222`)               |
| `HookProfile`                                                                                                      | no (type exists, unused)                                                  |
| `OptionalCapabilities`                                                                                             | no (type absent)                                                          |
| `Instructions : ImmutableArray<InstructionSource>`                                                                 | partial: `ImmutableArray<AgentMessage>` (`:280`)                          |
| `Toolsets`                                                                                                         | no; `Tools` + `ToolChoice` (`:295,309`)                                   |
| `Output : OutputDefinition` non-null                                                                               | partial: nullable (`:212`)                                                |
| not in spec: `ModelRequirements`, `Settings`                                                                       | extra interim (`:265,323`)                                                |

### Validator today vs requirements

Present: registration snapshot (`AgentCompositionValidator.cs:42-47`),
declared-graph cycles/captive/cardinality
(`ComponentDependencyGraphValidator.cs`), DI correspondence (`:119`), singular
engine/catalog/publication reader/security profile selector/grant
store/`TimeProvider`/RunId and OperationId generators (`:146-153`), keyed loops
(`:168-196`), `IModelCatalog` (`:78`), fixed continuation key (`:81-89`),
catalog readiness (`:232-265`), per-definition profiles and publication
(`:269-292`), loop key registered (`:294-304`), per-definition session
coordinator/context assembler/tool invoker/model selector/model resolver
(`:310-314`).

Missing: run-scope factory and validator, session directory and store selector,
hook kernel/catalog/profile selector, security authority selector/policy
catalog/approval broker, provider-profile runtime selector, engine-wide budget
authority, `IRandomizerFactory`/`IContentHasher` (`:604-605`), per-definition
input coordinator/output publisher/output processor/model request executor/hook
profile/security authority/budget profile/≥1 compatible model,
optional-capability collaborators, toolset ⇔ executor coherence.
`RepresentsCompleteRunnableGraph` is hard-coded `false`
(`ComponentRegistrationSnapshot.cs:63`). Diagnostic codes with no tests:
`run-profile-reader.missing`, `runid.missing`, `operationid.missing`,
`model-catalog.missing`, `continuation-policy.missing`,
`definition.collaborator.missing`, `engine.ambiguous`.

### Construction sites (C5/C6 blast radius)

`src/AgentKit.Simple/SimpleAgentPlan.cs:300`;
`tests/AgentKit.Loop.Tests/TestFactory.cs:87`;
`tests/AgentKit.Tests/AgentEngineBuilderTests.cs:662` (11-arg legacy, delete);
`tests/AgentKit.Tests/AgentTests.cs:104,275,632`;
`tests/AgentKit.Conversations.Tests/ConversationSessionOptionsFactory.cs:40`;
`tests/AgentKit.Abstractions.Tests/Context/ContextAssemblyEvidenceTests.cs:47`;
`ContextAssemblyRequestTests.cs:113,191`; `AgentLoop/LoopTestData.cs:101`;
`tests/AgentKit.Tests/CompositionTestData.cs:23`;
`tests/AgentKit.Abstractions.Tests/Composition/CompositionTestData.cs:10`;
`AgentDefinitionTests.cs:157`; `with { }` users at `AgentTests.cs:584`,
`AgentEngineTests.cs:150,151,240,269`, `AgentDefinitionTests.cs:65,73,80,113`.
Readers: `AgentLoopRunRequest.cs:143-160`, `AgentEngine.cs:275,477,721-724`,
`DefaultAgentLoop`, `DefaultContextAssembler`,
`DefaultConversationSession.cs:609-610`, `ConversationSessionOptions.cs`,
`RunPolicyVersioning.cs` (22 source files, 14 test files reference the type).

## Chunks

### WS18-C1: `AgentComponentSelection` and `AgentOptionalCapabilitySelection`

- Depends on: `IModelRequestExecutor` (WS7-C1) and `IToolExecutor` (WS4-C1)
  types existing, or use forward-declared keys. Risk: ADDITIVE. Size: S.
- Deliverables: two records per `composition-and-configuration.md:148-165` with
  validating constructors; tests; snapshot.
- **Landed**: pulled forward as a WS1-C9 prerequisite — `AgentRunPlan`
  (`composition-and-configuration.md:513-522`) has an
  `AgentOptionalCapabilitySelection OptionalCapabilities` field, and by the time
  WS1-C9 needed to compile that plan, both of C1's own listed dependencies
  (`IModelRequestExecutor`, `IToolExecutor`) already existed from WS7/WS4 work
  landed by a concurrent session, so no forward-declaration was needed.
  `src/AgentKit.Abstractions/Composition/AgentComponentSelection.cs` and
  `AgentOptionalCapabilitySelection.cs`, both matching the spec shape exactly.
  `AgentOptionalCapabilitySelection` gained one addition beyond the spec: a
  shared `None` singleton (every field absent/empty) for the common case of an
  agent with no optional capabilities, used by `DefaultAgentRunPlanCompiler`.
  Neither record is wired into `AgentDefinition` yet — that remains C3's job
  (`Components`/ `OptionalCapabilities` as real properties, migrating off the
  interim `LoopKey`/`InputCoordinatorKey`/`OutputPublisherKey` flat properties).
  `DefaultAgentRunPlanCompiler` (WS1-C9) builds an `AgentComponentSelection` and
  `AgentOptionalCapabilitySelection` freshly from the definition's existing flat
  properties and defaults rather than reading them from the definition directly,
  so this landing does not depend on or block C3. Snapshot: Abstractions.

### WS18-C2: `InstructionSource`

- Depends on: skip if WS9-C4 landed it. Risk: ADDITIVE. Size: S.

### WS18-C3: Init-only bridge properties

- Depends on: C1, C2. Risk: medium (equality and hash at
  `AgentDefinition.cs:375-433`). Size: M.
- Deliverables: `Components`, `OptionalCapabilities`, `HookProfile`, `Toolsets`
  default to null/empty; both constructors retained; all nine construction sites
  compile unchanged.

### WS18-C4: Bridge readers

- Depends on: C3. Size: M.
- Deliverables: engine and factory read `Components.Loop ?? LoopKey`,
  `Components.BudgetProfile`, `Components.OutputProcessor`; per-definition keys
  instead of loop-key-only resolution; both shapes behave identically in
  `AgentEngineTests`.

### WS18-C5: Migrate Simple and all construction sites

- Depends on: C3, C4. Size: M.
- Deliverables: `SimpleAgentPlan.cs:300-316` and every test factory use the new
  shape; nothing relies on `LoopKey`, `BudgetLimits`, or `Output` init props.

### WS18-C6: The break

- Depends on: C5, WS4 (`Toolsets`), WS7 (`Settings`/`ModelRequirements` move to
  the executor profile), WS11 (`BudgetProfile`). Risk: HIGH public break on
  Abstractions. Size: L.
- Deliverables: spec constructor (`:167-181`); delete `LoopKey`, `Tools`,
  `ToolChoice`, `Settings`, `ModelRequirements`, `BudgetLimits`, the legacy
  constructor; rewrite `AgentLoopRunRequest`'s definition constructor and the
  readers; delete `AgentEngineBuilderTests.cs:662`; snapshot; break note in the
  commit.
- Done when: `rg "LoopKey|\.BudgetLimits" src` is empty.

### WS18-C7: Engine-wide singular checks

- Depends on: contracts existing (WS1 run-scope factory, WS2 hook catalog and
  selector, WS3 policy catalog, WS7 profile runtime selector; decide ownership
  of `IRandomizerFactory`/`IContentHasher`). Size: M.
- Deliverables: extend `ValidateRequiredFacadeServices` (`:132-156`) and the
  required spine (`ComponentRegistrationSnapshot.cs:106-123`); one failing test
  per diagnostic code.

### WS18-C8: Per-definition required selections

- Depends on: C4, C7. Size: M.
- Deliverables: `ValidateCatalog` (`:226-318`) requires input coordinator,
  output publisher, output processor, context assembler (by
  `Components.Context`), model selector, model request executor, hook profile,
  security authority and profile, budget profile, and at least one compatible
  conversational model.

### WS18-C9: Optional capabilities and toolset coherence

- Depends on: C8, WS4/12/13/14/15. Size: M.
- Deliverables: rules at `:662-677`; each with a diagnostic and a test.

### WS18-C10: Keyed multiplicity and graph truthfulness

- Depends on: C7–C9. Size: S.
- Deliverables: correspondence and materializer coverage for new keyed
  contracts; `RepresentsCompleteRunnableGraph` computed; two keyed loops each
  selecting distinct collaborators.

### WS18-C11: Publication, Simple, documentation

- Depends on: C6–C10. Size: M.
- Deliverables: `AgentRunProfilePublication` gains hook and budget profile
  references; `composition-and-configuration.md:445-473` removed;
  `guides/composition.md`; architecture skill.

## Specification conflicts

- WS10 expects `Components.Compaction`; the spec record has no such field.
- `IRandomizerFactory` and `IContentHasher` are required by the spec but owned
  by no workstream.

## Totals

S 3, M 7, L 1.
