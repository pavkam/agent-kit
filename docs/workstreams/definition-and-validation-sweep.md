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
- [x] WS18-C2 `InstructionSource` (landed by WS9-C4; the transitional
      `AgentInstructionSources` bundle is deleted)
- [x] WS18-C3 init-only
      `Components`/`OptionalCapabilities`/`HookProfile`/`Toolsets`
- [x] WS18-C4 bridge readers in engine, factory, loop
- [x] WS18-C5 migrate Simple and all construction sites
- [x] WS18-C6 break: spec constructor, delete interim members
- [x] WS18-C7 validator engine-wide singular checks
- [x] WS18-C8 validator per-definition required selections
- [x] WS18-C9 validator optional-capability and toolset coherence
- [x] WS18-C10 keyed multiplicity, correspondence,
      `RepresentsCompleteRunnableGraph`
- [x] WS18-C11 run-profile publication, Simple `With*`, documentation

**Landing order.** C3–C5 existed only to stage C6 without a flag day. Nothing
outside this repository consumes `AgentDefinition`, so the bridge phase (flat
properties plus `Components.Loop ?? LoopKey` readers) would have been throwaway
code that the rules forbid shipping; C3–C6 therefore landed together as one
in-place break and C4/C5's deliverables are satisfied directly by the final
shape. Their "Landed" notes record what each would have covered.

## Verified current state

### `AgentDefinition` vs spec (after WS18)

| Spec field                                                                                                         | State                                                                                                   |
| ------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------- |
| `Id`, `Revision`, `DisplayName`, `SessionProfile`, `SecurityProfile`, `Models`, `RunDefaults`, `Extensions`        | yes                                                                                                     |
| `Components : AgentComponentSelection`                                                                             | yes; a non-null init-only member and constructor argument (`AgentDefinition.cs`)                        |
| `Components.ContinuationPolicy`, `Input`, `Output`, `OutputProcessor`, `Context`, `ModelSelector`, `ModelExecutor` | yes; the compiler resolves each under its exact key with no unkeyed fallback                            |
| `Components.BudgetProfile`                                                                                         | yes; the loop's run scope takes its limits from the profile, and the inline `BudgetLimits` list is gone |
| `HookProfile`                                                                                                      | yes, a required key; the publication carries the same key and validation compares them                  |
| `OptionalCapabilities`                                                                                             | yes; also carries the init-only `CompactionProfile` (settles the `Components.Compaction` conflict)      |
| `Instructions : ImmutableArray<InstructionSource>`                                                                 | yes; flat messages author through `InstructionSourceProjection.FromMessages`                            |
| `Toolsets`                                                                                                         | yes                                                                                                     |
| `Output : OutputDefinition` non-null                                                                               | yes; free text is `OutputDefinition.FreeText`                                                           |
| not in spec: `ModelRequirements`, `Settings`                                                                       | removed from the definition; carried by `ModelSelectionPolicy.Requirements`/`RequestSettings`           |

### Validator vs requirements (after WS18)

Engine-wide singulars are checked from frozen descriptors by
`ValidateRequiredFacadeServices` (`.missing` and `.ambiguous` codes): engine,
definition catalog, run-scope factory, run-profile publication reader, session
directory, session store catalog and selector, hook dispatcher/catalog/profile
selector/order resolver/instance factory/point definitions, security profile
selector, authority selector, policy catalog, approval broker and store, audit
dispatcher, decision store, grant store, model catalog, provider-profile runtime
selector, budget authority and profile catalog, `TimeProvider`,
`IRandomizerFactory`, `IContentHasher`, and the RunId and OperationId
generators. The composition validator is deliberately the static internal
`AgentCompositionValidator` rather than a service (see the owning document).

Per-definition checks live in `DefinitionCompositionValidator`: nine required
selections under exact keys, budget profile, session profile store existence and
capability compatibility, security authority installation, at least one
catalogued conversational model that satisfies the stated requirements, and
every optional-capability rule. `ComponentRegistrationSnapshot`'s
`RepresentsCompleteRunnableGraph` is computed.

### Construction sites

Every `new AgentDefinition(` site now uses the spec constructor, either directly
(`tests/AgentKit.Abstractions.Tests/Composition/AgentDefinitionTests.cs`) or
through `AgentDefinitionFixtures.Create` in `AgentKit.Test.Shared`; the
production site is `SimpleAgentPlan.BuildDefinition`. `with { }` users were
rewritten against the new members or
`AgentDefinitionTestExtensions.WithComponents`.

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
  Both records are now real `AgentDefinition` members (C3/C6), and
  `DefaultAgentRunPlanCompiler` reads them from the definition directly instead
  of rebuilding them from flat properties. `AgentOptionalCapabilitySelection`'s
  init-only `CompactionProfile` now validates its key. Snapshot: Abstractions.

### WS18-C2: `InstructionSource`

- Depends on: skip if WS9-C4 landed it. Risk: ADDITIVE. Size: S.
- **Landed**: `InstructionSource`, `LiteralInstructionSource`, and
  `InstructionSourceProjection` were already delivered by WS9-C4. WS18 finished
  the job: `AgentDefinition.Instructions` is `ImmutableArray<InstructionSource>`
  (the parallel `InstructionSources` and message-typed `Instructions` members
  and the `AgentInstructionSources` disambiguation bundle are deleted),
  `FromLegacyMessages` is renamed `FromMessages`, and message-level consumers
  (`AgentLoopRunRequest`, the run request builders, the context assembler's
  fallback path) project through `InstructionSourceProjection.ToMessages`.

### WS18-C3: Init-only bridge properties

- Depends on: C1, C2. Risk: medium (equality and hash at
  `AgentDefinition.cs:375-433`). Size: M.
- Deliverables: `Components`, `OptionalCapabilities`, `HookProfile`, `Toolsets`
  default to null/empty; both constructors retained; all nine construction sites
  compile unchanged.
- **Landed (folded into C6)**: no bridge phase shipped; see "Landing order".
  `Components`, `OptionalCapabilities`, `HookProfile`, and `Toolsets` are real
  members of the final shape. Equality and hashing (`AgentDefinition.cs`) cover
  every member, including `Components`; tests prove that differing component
  keys, output, model requirements and settings, instructions, toolsets, and
  optional capabilities each break equality.

### WS18-C4: Bridge readers

- Depends on: C3. Size: M.
- Deliverables: engine and factory read `Components.Loop ?? LoopKey`,
  `Components.BudgetProfile`, `Components.OutputProcessor`; per-definition keys
  instead of loop-key-only resolution; both shapes behave identically in
  `AgentEngineTests`.
- **Landed (folded into C6)**: `DefaultAgentRunPlanCompiler.CompileServices` and
  `AgentEngineRuntime` read `definition.Components.*` directly. The compiler
  resolves the loop, continuation policy, input coordinator, output processor,
  context assembler, model selector, and model executor under their exact keys
  with no unkeyed fallback (`GetRequiredKeyedService`). The output publisher is
  selected the same way but resolved by the engine right after it binds the
  run's `RunScopeIdentity` (the publisher is scoped to that identity, which does
  not exist until the `RunId` is minted); this is recorded in the owning
  document.
  `AgentEngineTests.RunAsync_WhenTwoDefinitionsSelectDifferentKeyedCollaborators_EachCompilesItsOwnBundle`
  proves two keyed loops each get a distinct complete collaborator set.

### WS18-C5: Migrate Simple and all construction sites

- Depends on: C3, C4. Size: M.
- Deliverables: `SimpleAgentPlan.cs:300-316` and every test factory use the new
  shape; nothing relies on `LoopKey`, `BudgetLimits`, or `Output` init props.
- **Landed**: `SimpleAgentPlan.BuildDefinition` builds every hosted definition
  from `DefaultComponents()` (the first-party default keys). The plan's spine
  now registers `AddAgentIO` with its session-backed queue and an
  `AddBudgetProfile` default profile whose limits read the plan lazily, so
  `WithBudget` in any order still applies and an unbudgeted engine selects an
  empty profile. The test-side construction sites moved to
  `AgentDefinitionFixtures`.

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
- **Landed**: `AgentDefinition` is the spec record with the spec constructor.
  Deleted: `LoopKey`, `InputCoordinatorKey`, `OutputPublisherKey`, `Tools`,
  `ToolChoice` (already absent from the definition), `Settings`,
  `ModelRequirements`, `BudgetLimits`, `BudgetProfile`, `InstructionSources`,
  the nullable `Output`, the 9- and 11-argument constructors, and
  `AgentInstructionSources`; `AgentEngineBuilderTests`' legacy-shape test is
  deleted. `AgentLoopRunRequest`'s definition constructor derives requirements,
  settings, budget profile, and message instructions from the new members, and a
  request pinned to a definition rejects inline budget limits. **Deviation**:
  the workstream says `Settings`/`ModelRequirements` "move to the executor
  profile" (WS7), but WS7 shipped no definition-level executor profile. The
  smallest coherent home is `ModelSelectionPolicy`, which already is the
  definition's model-choice policy; it gains optional `Requirements` and
  `RequestSettings` (recorded in `composition-and-configuration.md` and
  `model-and-embedding-providers.md`). The literal grep
  `rg "LoopKey|\.BudgetLimits" src` still finds `LoopKey` only inside the
  `AgentLoopComponentDefaults` constants and `.BudgetLimits` only on
  `AgentLoopRunRequest`, `ConversationSessionOptions`, and the Simple plan,
  which serve the definition-less conversation host; no `AgentDefinition` member
  remains. Break note for the commit: `AgentDefinition` constructor and members
  replaced; `AgentOutputDefaults` renamed `AgentOutputComponentDefaults` and
  moved to Abstractions; `AgentContextComponentDefaults.AssemblerKey` no longer
  aliases the loop key; `AgentRunProfilePublication` constructors take hook and
  budget profile keys.

### WS18-C7: Engine-wide singular checks

- Depends on: contracts existing (WS1 run-scope factory, WS2 hook catalog and
  selector, WS3 policy catalog, WS7 profile runtime selector; decide ownership
  of `IRandomizerFactory`/`IContentHasher`). Size: M.
- Deliverables: extend `ValidateRequiredFacadeServices` (`:132-156`) and the
  required spine (`ComponentRegistrationSnapshot.cs:106-123`); one failing test
  per diagnostic code.
- **Landed**: `ValidateRequiredFacadeServices` gained the model catalog,
  run-scope factory, session directory, session store catalog and selector,
  budget authority and profile catalog, `IRandomizerFactory`, and
  `IContentHasher` (hook kernel, security authority selector, policy catalog,
  approval broker, and provider-profile selector were already present); the
  build-time resolution check gained the same additions. The required spine in
  `ComponentRegistrationSnapshot` lists the same contracts. One failing test per
  diagnostic code exists for both `.missing` and `.ambiguous`
  (`AgentCompositionValidatorTests`), including the previously untested
  `run-profile-reader.missing`, `runid.missing`, `operationid.missing`,
  `model-catalog.missing`, and `engine.ambiguous`. The retired
  `continuation-policy.missing` code is now the per-definition
  `agentkit.definition.continuation-policy.missing`. **Ownership decision for
  the specification conflict**: `IRandomizerFactory`, `IContentHasher`, and
  their value types are owned by `AgentKit.Abstractions` (foundation contracts),
  with first-party defaults (`SecureRandomizerFactory`, `Sha256ContentHasher`)
  installed by `AddAgentKit` and replaceable through `ReplaceRandomizerFactory`
  / `ReplaceContentHasher`. The existing algorithm-agnostic `ContentHash` is
  kept; the hasher returns self-describing text so algorithm and
  canonicalization stay part of equality.

### WS18-C8: Per-definition required selections

- Depends on: C4, C7. Size: M.
- Deliverables: `ValidateCatalog` (`:226-318`) requires input coordinator,
  output publisher, output processor, context assembler (by
  `Components.Context`), model selector, model request executor, hook profile,
  security authority and profile, budget profile, and at least one compatible
  conversational model.
- **Landed**: `ValidateCatalog` is replaced by `DefinitionCompositionValidator`.
  It requires, per definition and under exact keys, the loop, continuation
  policy, input coordinator, output publisher, output processor, context
  assembler, model selector, and model executor (`.missing`/`.ambiguous` per
  role); the selected budget profile in the catalog; the session profile's store
  in the session store catalog with matching capabilities, durability, and
  fencing; the hook profile (via the hook validator); the security authority key
  via the new `ISecurityAuthorityCatalog` (registered by `AgentKit.Permissions`
  from key markers, never activating an authority; skipped, not failed, when a
  composition removes the catalog); and at least one catalogued conversational
  model satisfying the policy's requirements. Loop-scoped collaborators the
  selection does not name keep their documented keyed-or-shared fallback.

### WS18-C9: Optional capabilities and toolset coherence

- Depends on: C8, WS4/12/13/14/15. Size: M.
- Deliverables: rules at `:662-677`; each with a diagnostic and a test.
- **Landed**: toolsets without an executor key, an executor key without
  toolsets, a selected executor key with no keyed registration (an unkeyed
  executor does not satisfy it), a toolset with no registered publication, a
  compaction profile with no compactor, and every neutral
  `AgentCapabilityReference` each have a diagnostic and a test. Capability
  references resolve through the new additive `IAgentCapabilityProfileSource`
  contract (one source per capability identity; unregistered, ambiguous, and
  profile-missing cases are distinct codes); `AgentKit.Mcp.Client` registers the
  MCP source. Durability, memory, goals, and artifact profile rules were already
  enforced by their own validators and are unchanged.

### WS18-C10: Keyed multiplicity and graph truthfulness

- Depends on: C7–C9. Size: S.
- Deliverables: correspondence and materializer coverage for new keyed
  contracts; `RepresentsCompleteRunnableGraph` computed; two keyed loops each
  selecting distinct collaborators.
- **Landed**: `RepresentsCompleteRunnableGraph` is computed as "no unrepresented
  required address". `UnrepresentedRequiredSpine` now lists the engine-wide
  contracts plus every keyed registration of a definition-selectable contract
  (loop, continuation policy, input, output, processor, context, selector,
  executor, tool executor) that no explicit component declaration covers;
  registrations that publish no descriptors are therefore honestly incomplete,
  and declaring everything flips the flag (tests cover both and the one-omitted
  case). Keyed ambiguity is reported per role, and
  `EngineTests.RunAsync_WhenTwoDefinitionsSelectDifferentKeyedCollaborators_EachCompilesItsOwnBundle`
  runs two keyed loops that select distinct collaborators for all eight keyed
  roles. First-party registrations publish no component declarations, so the
  flag is false for a first-party composition. WS20 investigated publishing them
  and found it incompatible with the documented design (it would make every
  standard DI replacement of a first-party default a composition error, 14 of
  the 30 required addresses are opaque factories with package-internal graphs,
  and the storage-owned addresses are registered by leaves the runtime cannot
  name); `composition-and-configuration.md` records the reasoning.

### WS18-C11: Publication, Simple, documentation

- Depends on: C6–C10. Size: M.
- Deliverables: `AgentRunProfilePublication` gains hook and budget profile
  references; `composition-and-configuration.md:445-473` removed;
  `guides/composition.md`; architecture skill.
- **Landed**: `AgentRunProfilePublication` carries `HookProfile` and
  `BudgetProfile`, and the validator rejects a publication whose keys differ
  from the definition's (`agentkit.run-profile.key-mismatch`). The obsolete
  "current admission" prose in `composition-and-configuration.md` is replaced by
  the implemented shape and a "recorded deviations" list;
  `guides/composition.md` gained a worked keyed-definition example; `AGENTS.md`,
  the architecture skill, and the package READMEs (`AgentKit`, `AgentKit.IO`,
  `AgentKit.Budgets`) were updated. Simple's `With*` surface is unchanged
  because the keyed selection is composed by the plan; `WithBudget` now
  configures the default profile.

## Specification conflicts

- ~~WS10 expects `Components.Compaction`; the spec record has no such field.~~
  Resolved: compaction is optional, so it is selected by
  `AgentOptionalCapabilitySelection.CompactionProfile` and the required
  `AgentComponentSelection` stays as specified.
- ~~`IRandomizerFactory` and `IContentHasher` are required by the spec but owned
  by no workstream.~~ Resolved: owned by `AgentKit.Abstractions`, defaults in
  the facade (see C7).

## Totals

S 3, M 7, L 1.
