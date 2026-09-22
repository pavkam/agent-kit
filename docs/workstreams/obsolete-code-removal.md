# WS21: Obsolete, deprecated, and compatibility code removal

Goal: no source, test, example, or snapshot carries an `[Obsolete]` member, a
`Legacy*` type, a nullable "legacy/unpinned/compatibility-created" branch, or a
bridge that exists only to keep a superseded shape working. AgentKit has no
released consumers, so superseded surfaces are deleted rather than deprecated;
the spec shape is the only shape. A repository test keeps it that way.

Owning documents:
[Composition and configuration](../architecture/composition-and-configuration.md),
[File system](../architecture/file-system.md),
[Tools](../architecture/tools.md),
[Permissions and human control](../architecture/permissions-and-human-control.md),
and every architecture document whose acceptance list names a legacy path.

Scope boundary: "deliberately reduced stand-in" types that are interim shapes of
a still-specified contract (`IAgentLoop`, `IContextAssembler`, `IToolCatalog`,
`IOutputProcessor`, and the rest) are expanded by their owning workstream and
reconciled by [WS20](documentation-reconciliation.md). This workstream removes
code whose only purpose is compatibility with a shape the spec has already
replaced.

## Progress

- [ ] WS21-C1 inventory guard and allowlist
- [ ] WS21-C2a migrate remaining legacy host consumers
- [ ] WS21-C2b delete legacy host file-system surface
- [ ] WS21-C3 delete legacy tool orchestration path
- [ ] WS21-C4a security contracts: remove the unpinned path
- [ ] WS21-C4b security stores and codecs: remove the unpinned path
- [ ] WS21-C5 budget compatibility-created results
- [ ] WS21-C6 definition, run-request, and publication legacy shapes
- [ ] WS21-C7 conversations, session, loop, and hook compatibility members
- [ ] WS21-C8 build settings, pragmas, and guard closure

## Verified current state

Baseline today: 37 `[Obsolete]` attributes in 23 `src` files, 301 in 18 `tests`
files, 1 in `examples`; 32 `src` files declare or use a `Legacy*` identifier.
`Directory.Build.props:17` adds `CS0618` to `WarningsNotAsErrors`, so obsolete
use compiles silently. Two files suppress `CS0612` locally
(`ProjectInstructionContributor.cs:6`, `ListDirectoryTool.cs`). Public API
snapshots carry 73 legacy/obsolete lines across eight packages, 39 of them in
`AgentKit.Abstractions.verified.txt`.

### Legacy host file-system surface

| Member                                                                                                                                             | State                                                                                                                                                                                                                                                                                               |
| -------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `IFileSystem` `[Obsolete]`                                                                                                                         | EXISTS-AND-USED `Host/IFileSystem.cs:23`; deletion already planned in WS5-C8                                                                                                                                                                                                                        |
| `LegacyFileReadRequest`, `LegacyFileWriteResult`, `LegacyFileWritten`, `LegacyFileWriteDenied`, `LegacyFileAlreadyExists`, `LegacyFileWriteFailed` | EXISTS-AND-USED `src/AgentKit.Abstractions/Host/Legacy*.cs`; only `LegacyFileReadRequest` is named by WS5-C8                                                                                                                                                                                        |
| `ILegacyDirectoryReader` `[Obsolete]`                                                                                                              | EXISTS-AND-USED `Host/ILegacyDirectoryReader.cs:12`; not named by any chunk                                                                                                                                                                                                                         |
| `SandboxedFileSystem` `[Obsolete]` (six members)                                                                                                   | EXISTS-AND-USED `SandboxedFileSystem.cs:25,131,232,353,481,1017`                                                                                                                                                                                                                                    |
| `OperatingSystemFileSystemLegacyHost`                                                                                                              | EXISTS (uncommitted, in progress under WS5-C8) `src/AgentKit.FileSystem/OperatingSystemFileSystemLegacyHost.cs:9`                                                                                                                                                                                   |
| `AddSandboxedFileSystem`, un-keyed `AddInMemoryFileSystem`                                                                                         | EXISTS `FileSystem/ServiceExtensions.cs:35`, `FileSystem.InMemory/ServiceExtensions.cs:26`                                                                                                                                                                                                          |
| Simple overload                                                                                                                                    | EXISTS `Simple/AgentEngineBuilderExtensions.cs:369` ("Prefer keyed `AddOperatingSystemFileSystem`")                                                                                                                                                                                                 |
| Tool-level legacy constructors                                                                                                                     | EXISTS `ReadFileTool.cs:123`, `WriteFileTool.cs:105`, `GlobTool.cs:106`, `SearchTool.cs:111`, `EditTool.cs:115`; `ListDirectoryTool.cs` pragma                                                                                                                                                      |
| `ProjectInstructionContributor` legacy reads                                                                                                       | EXISTS-AND-USED `Context.Project/ProjectInstructionContributor.cs:11,26,59,86,117`                                                                                                                                                                                                                  |
| Example                                                                                                                                            | EXISTS `examples/CodingAgent/AgentRuntime.cs:57`                                                                                                                                                                                                                                                    |
| Test fixtures                                                                                                                                      | `SandboxedFileSystemTests` (~110 obsolete tests), `InMemoryFileSystemTests` (~57), `PatchToolTests` (19), both FS `ServiceExtensionsTests`, `AgentEngineBuilderExtensionsTests` (10), `ReplacementFileSystem` ×2, `StubFileSystem`, List `TestDoubles`, `Legacy*Tests`/`File*Tests` in Abstractions |

### Legacy tool orchestration path

All EXISTS-AND-USED through `Tools/ServiceExtensions.cs:450-460`, which
registers `DefaultToolInvoker` as `ILegacyToolCallOrchestrator` and
`LegacyToolInvokerExecutor` as the default `IToolExecutor`:
`ILegacyToolCallOrchestrator`, `LegacyToolCallRequest`,
`ResolvedToolInvocation`, `DefaultToolInvoker`, `LegacyToolInvokerExecutor`,
`LegacyToolCallResultFactory` (fabricates `legacy:` fingerprints and a `legacy`
execution policy, `:19-74`), `LegacyToolCatalogSnapshotFactory`,
`LegacyToolRunCatalogCaptureFactory`, `LegacyToolCatalogCapture`,
`IToolRunCatalogCaptureFactory`, `RunToolCatalogCaptureRequest`,
`ToolInvokerBridge` (legacy `ITool` → `IToolInvoker`), and the opt-in remark at
`Tools/ServiceExtensions.cs:82`. WS4-C10a deletes the authorizer, catalog, and
invoker; WS4-C10b promotes capture. Neither chunk names the capture factory,
request, result factory, bridge, or `ToolExecutionCapabilityFactory.cs:27`.

### Unpinned security path

Nullable authorization evidence exists solely for callers that predate captured
authorization: `SecurityRequest.cs:124`, `SecurityEnforcementRequest.cs:98`,
`SecurityGrant.cs:151`, `AgentPermissionOptions.cs:12`, the receiptless
`GrantConsumptionResult` constructor (`GrantConsumptionResult.cs:9`),
`SqliteSecurityGrantStore.cs:216` (legacy operation without intent receipt),
`SqliteSecurityGrantCodecReader.cs:129`/`Writer.cs:165`,
`SqliteBudgetSecurityCodecReader.cs:119`/`Writer.cs:159`,
`JsonSecurityEnforcementRequest.cs:15-68`, `JsonSecurityGrantLogRecord.cs:46`.
EXISTS-AND-USED; no workstream removes it.

### Budget compatibility values

`BudgetCommitResult.cs:42,81` and `BudgetCorrectionResult.cs:28,64` allow a null
`accountingRevision` "only for compatibility-created values". EXISTS-UNWIRED for
the ledger; used by tests and fakes.

### Definition, run request, publication, session page

`AgentDefinition.cs:117,234-238,365-376` (retained legacy unrunnable shape and
`InstructionSourceProjection.FromLegacyMessages`),
`InstructionSourceProjection.cs:8-60`, `AgentLoopRunRequest.cs:170,192` (null
for "legacy reduced request"), `AgentRunProfilePublication.cs:49`,
`SessionPage.cs:76` (null prefix for a "legacy producer"),
`AgentRunOutcome.cs:33` and `AgentRunFinished.cs:26` (legacy same-variant
copies). WS18-C6 deletes interim definition members but does not name the
projection, run request, publication, or session page.

### Conversations, session, loop, hooks

`ConversationSessionOptions.cs:17` (legacy decomposed options path),
`ConversationToolCallEvent.cs:56` and `ConversationToolResultEvent.cs:61`
(legacy constructors leaving a default call identity),
`AgentSessionOptions.cs:40` (legacy process busy default),
`DefaultAgentLoop.cs:2435-2448` (legacy `AgentLoopRunRequest.Observer`
delivery), `DefaultAgentLoop.cs:119` (interim hook deadline until
`AgentHookOptions.DefaultHookTimeout`), `AgentHookOptions.cs:9` (legacy
`DispatchAsync` overload with an explicit hook sequence).

### Retained on purpose (allowlist)

These match the search terms but describe external or domain facts and stay:

- `McpProtocolEra.Legacy`, `McpProtocolVersions.cs:18`,
  `McpClientVersionPolicy.cs:9-16`: MCP protocol-era vocabulary.
- `OpenAICompatibilityProfile.cs:33,46,150,170`, `OpenAIProviderOptions.cs:46`,
  `AzureOpenAIProviderOptions.cs:60`: wire field and role choices that a
  provider still requires.
- `CohereProviderDefaults.cs:15`: names an unsupported vendor dialect.
- `KnownModelStatus.Deprecated`, `KnownModel.cs:37,98`,
  `KnownModelCatalogDocument.cs:91`: vendor model lifecycle data.
- `"deprecated"` in `ToolSchemaProcessor.cs:108`,
  `BoundedToolSchemaEngine.cs:35`, `StructuralOutputSchemaEngine.cs:16,351`:
  JSON Schema annotation keyword.
- `Json*LogRecordKind` and `JsonBudget*Kind` remarks: persisted-schema evolution
  rules, not compatibility code.
- `AgentKit.Providers.OpenAICompatible`: a wire-family package, not a shim.

## Hidden prerequisites

1. WS5-C8 must land first; it owns the `IFileSystem` and `LegacyFileReadRequest`
   deletion and the in-progress `OperatingSystemFileSystemLegacyHost` and
   `OperatingSystemDirectoryReader`. WS21-C2 removes only what C8 leaves.
2. WS4-C9b/c, C10a, and C10b must land before C3; the bridge cannot go while
   first-party `ITool` packages remain.
3. WS18-C6 must land before C6; the definition break and this chunk touch the
   same constructors.
4. Removing the unpinned security path changes the persisted grant, approval,
   and budget security formats. There are no consumers, so the stores keep no
   migration or reader for the old layout.
5. Several legacy test fixtures are the only coverage of behavior that the spec
   surfaces also need (symlink rejection, patch conflict handling). Before any
   deletion, confirm the equivalent case exists against the spec contract or the
   conformance suite; port it otherwise.

## Spec coverage

| Contract                                      | Spec                                                                                 |
| --------------------------------------------- | ------------------------------------------------------------------------------------ |
| Removal of any contract in this file          | none needed; each target is already superseded by a `SPEC` contract                  |
| Non-null `Authorization` on security requests | `permissions-and-human-control.md` captured-authorization block (verify line in C4a) |
| Repository guard against obsolete surfaces    | NO-SPEC; the rule lives in `AGENTS.md` ".NET and public API rules"                   |

## Chunks

### WS21-C1: Inventory guard and allowlist

- Depends on: –. Risk: ADDITIVE. Size: S.
- Deliverables: `tests/AgentKit.Architecture.Tests/ObsoleteSurfaceTests.cs`
  scanning `src`, `tests`, and `examples` for `[Obsolete`, `Legacy[A-Z]`
  identifiers, and `#pragma warning disable CS0612|CS0618`; a checked-in
  baseline that may only shrink, and the "Retained on purpose" allowlist above.
- Done when: the test passes today and fails if any new obsolete member or
  `Legacy*` type is added.

### WS21-C2a: Migrate remaining legacy host consumers

- Depends on: WS5-C8. Risk: DENSE-MODIFY
  `ProjectInstructionContributor.cs:59-117`. Size: M.
- Deliverables: `ProjectInstructionContributor` reads through `IFileReader` from
  a keyed profile; the Simple overload at `AgentEngineBuilderExtensions.cs:369`
  and `examples/CodingAgent/AgentRuntime.cs:57` compose keyed
  `AddOperatingSystemFileSystem`; legacy constructors on the six file tools
  removed. Snapshots: Context.Project, Simple, Tools.Read, Tools.Write,
  Tools.Glob, Tools.Search, Tools.Edit, Tools.List.

### WS21-C2b: Delete legacy host file-system surface

- Depends on: C2a. Risk: CONTRACT-BREAK. Size: M.
- Deliverables: delete `Legacy*` write results, `ILegacyDirectoryReader`,
  `SandboxedFileSystem` (after extracting any P/Invoke still used by
  `OperatingSystemFileReader/Writer`), `OperatingSystemFileSystemLegacyHost`,
  `AddSandboxedFileSystem`, and the un-keyed `AddInMemoryFileSystem`; delete or
  port every `[Obsolete]` test and fake listed above (prerequisite 5).
  Snapshots: Abstractions, FileSystem, FileSystem.InMemory.
- Done when: `rg '\[Obsolete|Legacy'` over `src/AgentKit.Abstractions/Host`,
  `src/AgentKit.FileSystem*`, and `tests/AgentKit.FileSystem*` is empty.

### WS21-C3: Delete legacy tool orchestration path

- Depends on: WS4-C9b/c, WS4-C10a, WS4-C10b. Risk: CONTRACT-BREAK plus
  DENSE-MODIFY `Tools/ServiceExtensions.cs:440-465`. Size: M.
- Deliverables: delete every type in "Legacy tool orchestration path" that
  WS4-C10a left behind; `DefaultToolExecutor` is the only registered
  `IToolExecutor`; remove the opt-in remark at `:82` and the comment at
  `ToolExecutionCapabilityFactory.cs:27`. Snapshots: Abstractions, Tools.
- Done when: `rg 'Legacy|ToolInvokerBridge|IToolRunCatalogCaptureFactory'` over
  `src` and `tests` (C# only) is empty.

### WS21-C4a: Security contracts: remove the unpinned path

- Depends on: WS3. Risk: CONTRACT-BREAK. Size: M.
- Deliverables: `Authorization` non-null on `SecurityRequest`,
  `SecurityEnforcementRequest`, and `SecurityGrant`; `AgentPermissionOptions`
  binding required and validated at composition; receiptless
  `GrantConsumptionResult` constructor removed; every caller supplies captured
  evidence. Argument tests for the new `ArgumentNullException` cases. Snapshots:
  Abstractions, Permissions.

### WS21-C4b: Security stores and codecs: remove the unpinned path

- Depends on: C4a. Risk: CONTRACT-BREAK. Size: M.
- Deliverables: remove the legacy operation at
  `SqliteSecurityGrantStore.cs:216`, the null branches in both SQLite security
  codecs (Permissions and Budgets), `JsonSecurityEnforcementRequest`, and
  `JsonSecurityGrantLogRecord`; the persisted layouts require the evidence, with
  no reader for the old layout. Conformance suites for all three adapters
  updated. Snapshots: Permissions.Sqlite, Permissions.Json, Budgets.Sqlite,
  Storage.Json.

### WS21-C5: Budget compatibility-created results

- Depends on: –. Risk: CONTRACT-BREAK. Size: S.
- Deliverables: `accountingRevision` required and positive on
  `BudgetCommitResult` and `BudgetCorrectionResult`; fakes updated. Snapshot:
  Abstractions.

### WS21-C6: Definition, run-request, and publication legacy shapes

- Depends on: WS18-C6. Risk: CONTRACT-BREAK. Size: M.
- Deliverables: remove the legacy unrunnable `AgentDefinition` shape and
  `InstructionSourceProjection.FromLegacyMessages`/`LegacyInstructionsKey`;
  non-null definition and snapshot on `AgentLoopRunRequest`; non-null snapshot
  on `AgentRunProfilePublication`; non-null prefix on `SessionPage`; drop legacy
  outcome copies from `AgentRunOutcome`/`AgentRunFinished`. Snapshots:
  Abstractions, Loop, Session.

### WS21-C7: Conversations, session, loop, and hook compatibility members

- Depends on: C6. Risk: DENSE-MODIFY `DefaultAgentLoop.cs:2435-2460`. Size: M.
- Deliverables: remove the decomposed path on `ConversationSessionOptions`, the
  legacy constructors on both conversation tool events, the legacy busy default
  on `AgentSessionOptions`, legacy observer delivery in `DefaultAgentLoop`, the
  interim hook deadline at `:119` (use `AgentHookOptions.DefaultHookTimeout`),
  and the explicit-sequence `DispatchAsync` overload. Snapshots: Conversations,
  Session, Loop, Hooks.

### WS21-C8: Build settings, pragmas, and guard closure

- Depends on: C1–C7. Risk: ADDITIVE (build tightening). Size: S.
- Deliverables: remove `CS0618` from `WarningsNotAsErrors` in
  `Directory.Build.props:17`; delete remaining `CS0612`/`CS0618` pragmas; reduce
  the C1 baseline to the allowlist only, so the guard enforces the `AGENTS.md`
  no-compatibility rule from then on; regenerate all snapshots.
- Done when:
  `rg '\[Obsolete|Legacy[A-Z]|compatibility-created|unpinned' src tests examples`
  returns only allowlisted lines and `make format lint build test` passes.

## Totals

S 3, M 7. Confidence medium: most targets are well located, but the removal
order depends on WS4, WS5, and WS18 landing first.
