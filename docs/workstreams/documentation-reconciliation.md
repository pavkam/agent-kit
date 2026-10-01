# WS20: Documentation reconciliation

Goal: no document, README, skill, or source remark describes an interim,
reduced, or stand-in state once the preceding workstreams have landed; every new
package appears in the repository map and package catalog; every acceptance list
matches code.

Owning documents: all of `docs/`, `AGENTS.md`, package READMEs,
`.agents/skills/*/SKILL.md`.

## Progress

- [x] WS20-C1 composition and runtime architecture prose
- [x] WS20-C2 context, compaction, tools, identity, durability architecture
- [x] WS20-C3 guides, getting started, index
- [x] WS20-C4 use cases
- [x] WS20-C5 package READMEs, examples, root README
- [x] WS20-C6 package catalog and `AGENTS.md` map
- [x] WS20-C7 skills
- [x] WS20-C8 source-remark sweep

## Method

Every claim was checked against source rather than against a workstream summary.
The sweep ran three mechanical checks over all Markdown (relative-link
resolution, identifier existence for every `Add*`/`Use*`/`With*`/`Replace*` call
and every type name in a code block, and member-name existence in use-case and
guide snippets) plus a read of each statement the original checklist named. The
three checks are not committed tooling; they were run as throwaway scripts.

## Findings that were not prose

Reading the code against the documents turned up work that no workstream closed.
Each is recorded where it belongs and left open:

- **Tools.** ~~`IToolExecutionPolicy`, `IToolExecutionPolicySelector`,
  `IToolCallRecorder`, `IToolEventSink`, and ten of their registration helpers
  are specified in `tools.md` and absent from the code.~~ Closed by
  [WS4-C14](tool-runtime.md#ws4-c14-recorder-execution-policy-and-event-sink);
  `tools.md` states the shipped surface and records each deviation.
- **Compaction.** ~~The keyed per-collaborator registration helpers and a
  registration consuming `CompactionProfileOptions` are specified and absent.~~
  Closed by
  [WS10-C9](context-compaction.md#ws10-c9-keyed-collaborator-registration-and-profile-consumption);
  `context-compaction.md` states the shipped surface.
- **Providers.** ~~First-party adapters send through an injected `HttpClient`
  and obtain no provider-egress or credential-read grant.~~ Closed for egress by
  [WS7-C11](provider-runtime.md#ws7-c11-provider-egress-through-inetworktransport):
  every adapter sends through `ProviderEgress` under provider-egress,
  resolution, and send grants. The credential-read grant and lease stay
  unshipped, and `model-and-embedding-providers.md` records both the closure and
  that remainder.
- **Component declarations.** `RepresentsCompleteRunnableGraph` stays false for
  first-party compositions, and that is now documented as a consequence of the
  declaration contract rather than a pending step (see C1).

## State of the original checklist

Every row below was reworded to accurate present tense or removed, except where
noted.

| File                                            | Original lines                                         | Result                                                                                                                                                                                    |
| ----------------------------------------------- | ------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `architecture/agent-runtime.md`                 | 54, 60, 72, 83, 104, 286, 298, 301, 304, 305, 363, 376 | Rewritten around `AgentLoopRunRequest`, `AgentRunServices`, `AgentLoopResult`; the unimplemented `AgentRunInvocation`, `TurnContext`, `AgentRunProgress` removed from the normative block |
| `architecture/composition-and-configuration.md` | 445, 448, 472                                          | The "Current admission surface" prose was already replaced by WS18-C11; the remaining `AgentRunPlan` hook note and the declaration paragraph were rewritten                               |
| `architecture/context-compaction.md`            | 540, 571, 586, 1118, 1359                              | Generator seam documented as shipped; loop triggers and registration surface stated; unshipped helpers listed                                                                             |
| `architecture/context.md`                       | 347, 351, 356, 361                                     | Request and result shapes replaced with the shipped ones; evidence-less constructor removed from code and prose                                                                           |
| `architecture/tools.md`                         | 471, 500                                               | Descriptor and `AddToolset` prose made present tense; shipped registration surface and unimplemented contracts added                                                                      |
| `architecture/input-and-output.md`              | 402                                                    | Reworded; `DefaultOutputPublisher` is subscribable, which the old prose denied                                                                                                            |
| `architecture/identity.md`                      | 146                                                    | "Migrating the reduced identity contract" replaced by "Constructing an identity"                                                                                                          |
| `architecture/durable-execution.md`             | 335                                                    | "the shipped" removed                                                                                                                                                                     |
| `concepts/agent-loop-state-machine.md`          | 195                                                    | "reduced loop" reworded                                                                                                                                                                   |
| `concepts/extensions-hooks-and-middleware.md`   | 44, 75                                                 | Verified intentional ("not yet established" describes an identity that does not exist at that stage); unchanged                                                                           |
| `guides/permissions.md`                         | 192                                                    | Audit paragraph rewritten; the "remaining coverage" pointer was stale                                                                                                                     |
| `use-cases/web-support-assistant.md`            | 184                                                    | Status section rewritten                                                                                                                                                                  |
| `packages/index.md`                             | 159                                                    | Planned-packages sentence removed; seven missing packages and ten test projects added                                                                                                     |
| `getting-started.md`                            | 21–23                                                  | Admission paragraph rewritten                                                                                                                                                             |
| `src/AgentKit.Loop/README.md`                   | one "reduced"                                          | Reworded                                                                                                                                                                                  |

## Chunks

### WS20-C1: Composition and runtime architecture prose

- Depends on: WS1, WS18. Size: M.
- Done when: `rg -i "reduced|remaining steps|Current admission surface"` over
  `composition-and-configuration.md`, `agent-runtime.md`, `input-and-output.md`
  is empty.
- **Landed:** `agent-runtime.md` now describes the shipped request-based loop:
  `AgentLoopRunRequest` (which pins the definition and derives its settings from
  it), `AgentRunServices`, and `AgentLoopResult` replace the specification-era
  `AgentRunInvocation`, `TurnContext`, `AgentRunProgress`, and
  `RunPolicySnapshot`, none of which exist. The continuation policy is the key
  the definition selects. That also fixed a code inconsistency the prose
  exposed: the run policy version hashed the fixed default policy key, so two
  definitions selecting different policies shared a version; it now hashes
  `Components.ContinuationPolicy` in the loop and the engine.
  `composition-and-configuration.md` drops the stale `HookDispatchContext` note
  from `AgentRunPlan` and records the declaration contract (see the finding
  above). `input-and-output.md` no longer says `DefaultOutputPublisher` is not
  subscribable. Done-when check:
  `rg -i "reduced|remaining steps|Current admission surface"` over the three
  files returns nothing.

### WS20-C2: Context, compaction, tools, identity, durability architecture

- Depends on: WS4, WS9, WS10, WS12, WS16. Size: M.
- Done when: each listed statement is removed or reworded as accurate present
  tense.
- **Landed:** `context.md` documents the shipped `ContextAssemblyRequest` (one
  constructor over atomic `ContextAssemblyEvidence`; `History` derived from it;
  `Output` init property) and `LlmRequestContext` as the provider-facing result.
  `context-compaction.md` documents the shipped generator seam
  (`ModelCompactionStrategy` over `ModelBackedSummaryGenerator`), the three loop
  triggers beyond context pressure, the shipped registration surface, and the
  helpers that remain specification only. `tools.md` states the shipped executor
  and capability shapes, registration surface, and the unimplemented recorder,
  event-sink, and execution-policy contracts. `identity.md`, `artifacts.md`,
  `durable-execution.md`, `model-and-embedding-providers.md`, and
  `permissions-and-human-control.md` lost their "interim", "migrating", and
  workstream-chunk phrasing; the provider document also records the egress
  deviation. Concept pages that named `ModelRequestContext` or a placeholder
  registration block were corrected.

### WS20-C3: Guides, getting started, index

- Depends on: WS1, WS4, WS18. Size: M.
- Deliverables: `getting-started.md` current-status section,
  `guides/composition.md`, `guides/storage.md` (11 interim-API hits),
  `guides/permissions.md`, `guides/file-system.md`, `docs/index.md`.
- **Landed:** `getting-started.md` no longer calls admission pending;
  `guides/composition.md`, `guides/file-system.md`, and `guides/storage.md` show
  the written-out compositions that actually build (`AgentEngine.CreateBuilder`,
  keyed registrations, toolset publication), with the JSON session store added;
  `guides/permissions.md` describes audit coverage as it is, and every
  `ISecurityPolicy` snippet in the repository now takes the
  `SecurityPolicyContext` parameter; `docs/index.md` points at the workstreams
  as history rather than a plan.

### WS20-C4: Use cases

- Depends on: all. Size: M.
- Deliverables: 13 files; `web-support-assistant.md` (12 hits),
  `testing-agents-offline.md` (7), `quickstart-example.md` (6),
  `coding-agent-example.md` (5), the rest one or two each.
- Done when: no `SendAsync`, `AgentSendRequest`, `LlmToolDefinition`, or
  `IConversationSession` unless still real.
- **Landed:** all thirteen use-case files were checked. Corrections:
  `domain-tool-integration.md` now writes an `IToolInvoker` with a static
  descriptor, publishes and selects a toolset (the old text used a nonexistent
  `ITool` and `AgentToolsOptions` allow-list); `ops-runbook-executor.md`,
  `web-research-assistant.md`, and `testing-agents-offline.md` select the
  toolsets of the tools they register (registering a tool no longer exposes it);
  `coding-agent-example.md` shows the real `AgentRuntime.Create` composition;
  `web-support-assistant.md` and `ticket-triage-worker.md` register the decision
  and approval stores the engine requires. The done-when grep still matches
  `SendAsync`, `AgentSendRequest`, `LlmToolDefinition`, and
  `IConversationSession` because each is real: `Agent.SendAsync`,
  `AgentSendRequest`, and `IConversationSession` are public API and
  `LlmToolDefinition` is the provider-neutral tool shape.

### WS20-C5: Package READMEs, examples, root README

- Depends on: all. Size: M.
- Deliverables: `AgentKit.Conversations` (9 hits), `AgentKit.Simple` (8),
  `AgentKit` (4), `AgentKit.Tools` (3), `AgentKit.Loop`, `AgentKit.Budgets`,
  `examples/CodingAgent` (5), `examples/QuickStart`, root README; new READMEs
  for every added package.
- **Landed:** `AgentKit.Conversations`, `AgentKit.Simple`, `AgentKit`,
  `AgentKit.Tools`, `AgentKit.Loop`, `AgentKit.IO`, `AgentKit.Mcp.Client`,
  `AgentKit.Permissions.Sqlite`, the Cohere and chat-provider defaults, and the
  CodingAgent README were reworded against the code. 61 package READMEs linked a
  nonexistent `docs/implementation-progress.md`; they now link the workstream
  index. Thirteen test and example-test projects had no README; each has one,
  and the broken test-file links in three existing test READMEs were fixed. The
  root README needed one sentence; the QuickStart README was already accurate.

### WS20-C6: Package catalog and `AGENTS.md` map

- Depends on: all. Size: M.
- Deliverables: `docs/packages/index.md:159`; `AGENTS.md` repository map gains
  `AgentKit.Durability`, `.Memory`, `.Goals.Hosting`, `.Context.Project`,
  `.Observability.OpenTelemetry`, `.Evaluation` and their adapters.
- **Landed:** `docs/packages/index.md` lists `AgentKit.Memory` and its three
  adapters, `AgentKit.Context.Project`, `.Context.Retrieval`,
  `.Observability.OpenTelemetry`, the Evaluation example, and the missing test
  projects, and explains the linked `Storage.Shared`/`Storage.Durable` source
  directories. `project-structure.md` gains the same explanation plus the
  application leaves. `AGENTS.md`'s repository map names `AgentKit.Artifacts`,
  `.Durability`, `.Goals` as runtime packages, adds an application-leaf bullet
  for `Conversations`, `Simple`, `Goals.Hosting`, and `Evaluation`, and states
  the linked-source convention. `.Memory`, `.Context.Project`, `.Evaluation`,
  and `.Observability.OpenTelemetry` were already present.

### WS20-C7: Skills

- Depends on: all. Size: M.
- Deliverables: every `.agents/skills/*/SKILL.md` touched by WS1–19; at minimum
  architecture, evaluation, agent-loop, tools, context, input-output.
- **Landed:** the architecture, evaluation, agent-loop, tools, context, and
  input-output skills were checked against the documents above. Added: the
  atomic-evidence request and `LlmRequestContext` result (context), the
  request-based loop and definition-selected policy key (agent-loop), the opt-in
  declaration contract (architecture), and the shipped compaction registration
  surface (context-compaction), and the unimplemented recorder, event-sink, and
  execution-policy contracts (tools). A duplicated sentence in the input-output
  skill was removed. The evaluation skill needed no change.

### WS20-C8: Source-remark sweep

- Depends on: all, including WS21. Size: S.
- Deliverables: triage list of the remaining `stand-in`/`not yet` hits; remove
  or reword each that no longer describes reality. `Legacy*` types, `[Obsolete]`
  members, and compatibility branches are deleted by
  [WS21](obsolete-code-removal.md), not reworded here.
- **Landed:** `rg "stand-in|not yet" src --type cs` went from 79 hits in 41
  files to 53 in 42; the "stand-in" count is now zero. The remaining 53 are all
  legitimate: temporal state ("has not yet reached a terminal state", "not yet
  covered"), adapter scope statements for wire features a leaf does not
  translate, and a few runtime messages tests assert. Triage of what changed:
  - _Reworded because the thing now exists:_ `IAgentLoop`, `AgentRunServices`,
    `DefaultAgentLoop`, `NoOpModelResponseObserver`, `LlmRequestContext`,
    `ModelExecutionRequest`, `IContextAssembler`, `OutputMode`,
    `OutputAlternative`, `OutputRepairInstruction`, `IOutputValidator`,
    `DefaultOutputProcessor`, `AgentOutputOptions.AllowProviderModeDowngrade`,
    `NetworkDestinationPolicy`, `INetworkTransport`,
    `BudgetUnknownCostBehavior`, `HistoryPreparationRequest`,
    `IHistoryPipeline`, `LlmToolDefinition`, `ModelCapabilities`,
    `IToolRunCatalogCaptureFactory`, the five compaction value types, and the
    Anthropic, Gemini, Mistral, Bedrock, and Cohere capability remarks (three
    claimed structured output was untranslated while their defaults declared it
    supported).
  - _Reworded to state the real deviation:_ `OpenAICompatibleLlmModelBase`
    (egress), `ToolExecutionCapability` and `IToolProgressReporter` (omitted
    contracts), `AgentEngineBuilder` (declarations), `DefaultCompactor`'s
    single-strategy constructor and its helper types.
  - _Deleted:_ `CompactionValidated(CompactionCandidate)`, a constructor that
    fabricated a `"legacy"` validation stamp and was used only by tests.
  - _Kept:_ `ModelUsageReportState.Interim` and similar domain terms,
    `AwsBedrockProfileCredentialSource` and the Ollama key (honest placeholders
    by design), `HookMutationDispatchMode.Concurrent` (reserved and rejected),
    runtime messages such as "not yet supported by the Bedrock Converse request
    translator". `DefaultCompactor`'s single-strategy constructor is used by
    tests only and remains a public convenience over
    `FixedCompactionStrategyResolver`, `EmptyServiceProvider`, and
    `InertSessionRunCoordinator`; removing it means rewriting about a dozen
    compactor tests and was not done.

## Code leftovers closed by the sweep

- **`ContextAssemblyRequest` evidence-less constructor.** Verified no live path
  used it: the loop's only construction site passes evidence, and the only other
  callers were tests. Removed together with the `Instructions` and `History`
  constructor arguments, the nullable `Evidence`, the matching
  `DefaultContextAssembler` branch (including `ValidateInstructions`, whose
  checks `DefaultInstructionResolver` already performs), and the redundant
  `AgentLoopRunRequest.Instructions` projection. `History` is now derived from
  the evidence. Tests were rebuilt around one evidence-bound fixture; the public
  API snapshots were regenerated. This is a breaking change.
- **`RepresentsCompleteRunnableGraph` for first-party compositions.**
  Investigated and found infeasible within the documented design, not deferred:
  see the composition architecture document. The decisive facts, verified
  against a real `AgentKit.Simple` composition, are that 14 of the 30 required
  addresses are opaque factories, that declarations cannot observe
  `Replace`/`RemoveAll`, and that storage-owned addresses are registered by leaf
  packages. The flag, its tests, and the WS18 note are unchanged apart from the
  explanation.

## Totals

S 1, M 7. All eight chunks landed.
