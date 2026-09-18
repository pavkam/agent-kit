# Identity values versus object references

**Status:** Applied for every genuine duplicate-storage bug found (sections A
and C3). The rule itself is stated in
[design principles](concepts/design-principles.md). Section B and C5 were
evaluated and deliberately not applied — see "Evaluated and declined" below —
because they are cosmetic tuple-to-composite reshapes, not fixes to actual
duplicate storage. The architecture guard-rail was evaluated and deliberately
not built; see "Guard rail: evaluated and declined" below for why.

## Applied — pass 1 (section A: redundant identity storage)

The first pass implemented every row in section A that a constructor already
proved redundant (the authorization/correlation value already carried the same
identity), by turning the duplicate stored property into a computed accessor
over the value that carries it. This removed the duplicate storage with **no
public API shape change** in every case except one:

- `AgentRunRequest.AgentId`, `.SessionId`, `.RunId` now read through
  `Authorization.Scope`.
- `AcceptedToolCall` and `ToolCallResult`: `AgentId`, `SessionId`, `RunId`,
  `TurnId`, `OperationId` now read through `Authorization.Scope` and a new
  shared `AcceptedToolCall.RequireCorrelation` helper. `CallId` is unchanged
  (not derivable; it is the actual causal anchor).
- `ToolExecutionContext` and `SessionOperationContext`: `AgentId`, `SessionId`,
  and `Correlation` now read through `Authorization.Scope`.
- `BeforeToolInvocationEventArgs`, `BeforeModelRequestEventArgs`,
  `RunStartedEventArgs`: `RunId`/`TurnId` now read through the base
  `Correlation` instead of a second copy.
- `SessionRunLease`: all six identity members (`TenantId`, `AgentId`,
  `SessionId`, `ExecutionLaneId`, `OperationId`, `RunId`) now read through the
  retained `SessionOperationContext` instead of six separate fields.
- `TaskDelegationPrompt.ParentRunId` now reads through `Correlation.RunId`. This
  one **did** require a constructor-level fix first: nothing previously enforced
  `parentRunId == correlation.RunId`, so the constructor now validates that
  before computing the property. This is the only row in this pass with a public
  API change: `ParentRunId` lost its `init` accessor (confirmed by
  `AgentKit.Compatibility.Tests`; the snapshot was updated).

Every other row kept its constructor signature; only backing storage and
`<value>` documentation changed. `AgentKit.Abstractions.Tests` (5986 tests),
`AgentKit.Loop.Tests`, `AgentKit.Session.Tests`, `AgentKit.Tools.Tests`,
`AgentKit.Tests`, `AgentKit.Conversations.Tests`, `AgentKit.Hooks.Tests`,
`AgentKit.Goals.Tests`, and `AgentKit.Tools.Task.Tests` all pass unchanged.

Fixing `TaskDelegationPrompt` also caught a real test-data bug: the
`EngineDelegationChannelTests.Prompt` helper generated two _unrelated_ random
`RunId`s for `parentRunId` and the correlation, which only started failing once
the constructor enforced the invariant. Fixed the helper to derive both from one
`RunId`, which is what every production caller already did.

## Applied — pass 2 (C3: hook base carried `AgentId`/`SessionId`)

[Extensions, hooks, and middleware](concepts/extensions-hooks-and-middleware.md)
states the shared `AgentHookEventArgs` base "MUST NOT require `AgentId`,
`SessionId`, `RunId`, `TurnId`, ... or any other fact that may not exist when an
earlier stage dispatches." The base required both. Fixed by splitting the
identity out into a new intermediate type instead of duplicating it per concrete
hook-args class:

- Added `AgentScopedHookEventArgs: AgentHookEventArgs` carrying `AgentId` and
  `SessionId?`, with the constructor validation the base never had
  (`ArgumentOutOfRangeException` for a default `agentId` or a
  present-but-default `sessionId` — the original base stored both without
  checking either).
- `AgentHookEventArgs` itself now carries only `Correlation`, `Timestamp`, and
  `InvocationId` — facts that exist for every dispatch regardless of stage.
- `RunStartedEventArgs`, `BeforeModelRequestEventArgs`,
  `BeforeToolInvocationEventArgs` now derive from `AgentScopedHookEventArgs`
  instead of `AgentHookEventArgs`. Their public constructor signatures and
  properties are unchanged; only the base class in the type hierarchy changed.
- `DefaultHookDispatcher`'s activity tagging (`AgentKitTagNames.AgentId`/
  `.SessionId`) now pattern-matches `args as AgentScopedHookEventArgs` instead
  of reading the base unconditionally, so it degrades to a null tag instead of a
  compile error (or a fabricated identity) for a hook point that fires before an
  agent is known. Every current hook point is still agent-scoped, so no activity
  tag actually changes for existing hook points; this only changes what a
  _future_, earlier-stage hook point is allowed to look like.

This is a public API change: `AgentHookEventArgs.AgentId`/`.SessionId` were
removed from the base (confirmed by `AgentKit.Compatibility.Tests`; the snapshot
was updated) and now live on the new `AgentScopedHookEventArgs`. Anything that
referenced them through the base type — nothing in this repository did outside
`DefaultHookDispatcher`, which was fixed — needs to either narrow to
`AgentScopedHookEventArgs` or accept that they may be absent.

**Deliberately not part of this fix:** `AgentHookEventArgs.InvocationId` is
still typed `HookInvocationId` and still shared across every hook invoked in one
dispatch. [Extensions](concepts/extensions-hooks-and-middleware.md) and
[the architecture](architecture/extensions.md) specify a `HookDispatchId` on the
shared base plus a separate `HookInvocationId` minted per registration execution
and delivered through a dispatcher-created `HookInvocationContext` — neither
`HookDispatchId` nor `HookInvocationContext` exist in code today, and the
architecture doc's full blueprint also specifies `IHookActivationLease`,
`IHookInstanceFactory`, and `IHookInvocationTracker` machinery that has no
implementation at all. That is a complete hook-activation-lifecycle architecture
project, not an identity/object-reference dedupe, and hooks are freshly landed
code (second-to-last commit on `main` at the time of this guide). Treat it as
its own task with its own design review against
`docs/architecture/extensions.md`; do not fold it into a future "finish the
hooks identity cleanup" pass without reading that full blueprint first.

Tests: added `AgentScopedHookEventArgsTests` (new fixture, mirrors the trimmed
`AgentHookEventArgsTests`), added a dispatcher test proving a non-agent-scoped
args type gets null `AgentId`/`SessionId` tags
(`DispatchAsync_WhenObserved_EmitsCorrelatedTerminalActivity`, extended) and one
proving an agent-scoped type still gets them
(`DispatchAsync_WhenArgsAreAgentScoped_TagsAgentAndSessionIdentity`, new).
`TestHookEventArgs` (the hooks test double for generic dispatcher mechanics) now
derives from `AgentHookEventArgs` directly, deliberately modeling an
earlier-stage, non-agent-scoped point. `AgentKit.Abstractions.Tests` (5990),
`AgentKit.Hooks.Tests` (72), and `AgentKit.Loop.Tests` (209) all pass.

## Corrected on inspection (do not apply as originally written)

Implementing section A surfaced four rows that looked like duplication from the
survey but are not, once the actual invariants were checked:

- **`AgentRunRequest.Agent` nullability / the two constructors.** Not
  duplication: `Agent` is genuinely optional (a "reduced" request has no pinned
  `AgentDefinition`), and `DefaultAgentLoop.cs:539-540` reads
  `request.Agent?.Models ?? request.ModelPolicy` specifically because `Agent`
  can be absent. Do not collapse the two constructors.
- **`ConversationSessionOptions.Agent` and `.AgentId`.** Not duplication:
  `DefaultConversationSession` requires `AgentId` unconditionally and only
  cross-checks it against `Agent.Id` when `Agent` is supplied
  (`DefaultConversationSession.cs:344-356`). This is the same reduced/full
  duality as `AgentRunRequest`, expressed as a mutable options object. Leave
  both properties settable.
- **`ToolProviderBinding.SourceId` beside `Provider.SourceId`.** The type's own
  remarks say reading `SourceId` deliberately does not call the provider, even
  though `IToolProvider.SourceId` is contractually side-effect-free. Treat this
  as an intentional defensive cache, not a bug.
- **`ArtifactStorePrepareRequest.TenantId`/`.CreatedBy` beside `.Identity`.**
  **Confirmed intentional, not merely unverified**: this constructor has no
  cross-validation tying `tenantId`/`createdBy` to `identity.TenantId`/
  `identity.PrincipalId`, and
  `InMemoryArtifactStoreTests.PrepareAsync_WhenDeclaredTenantDiffersFromIdentity_DeniesBeforeStateMutation`
  deliberately constructs a request where they differ, to prove the _store's_
  authorization path — not the constructor — rejects the mismatch
  (`ArtifactFailureKind.Denied`). The type must stay lenient at construction so
  that denial path stays reachable and testable. Do not add a constructor-level
  equality check here.

The lesson: before turning a stored identity into a computed accessor, confirm
the constructor actually enforces equality with the value you intend to compute
it from. Where it doesn't, check for a test that depends on the values being
allowed to differ before assuming it's a missing invariant. Two of the four rows
above turned out to be deliberately lenient by design, not merely unvalidated.

## Evaluated and declined

- **Section B (loose id tuples into `SessionAddress`/
  `SecurityAuthorizationScope` composites, e.g.
  `Artifact{Prepare,Finalize,Abort,Read,Delete}Request`,
  `HumanQuestionRequest`/`HumanQuestionPrompt`, `ToolDiscoveryRequest`,
  `ToolCatalogSnapshot`, `CompactionOperationContext`,
  `ProtectedSemanticOperationContext`, `InputAdmissionRequest`,
  `InputPromotionRequest`/`InputPromotionContext`, `AdmissionReceipt`).** None
  of these store an identity that's _also_ held somewhere else on the same type
  — they simply take `AgentId` and `SessionId` (and sometimes a correlation) as
  separate constructor parameters instead of one composite value. That is a
  style preference, not the duplicate-storage bug this guide exists to fix.
  Reshaping ~10 constructors — several of which are reached from persisted
  session-entry codecs (`CompactionOperationContext`, `AdmissionReceipt`) — for
  a readability improvement carries real regression risk for no behavior fix.
  Declined; revisit only if a specific type in this list is being touched for an
  unrelated reason anyway.
- **C5 (`RunEventHub`'s four loose identity fields).** Same reasoning: the hub
  is the _original_ owner of these four values, not a copy of something held
  elsewhere. Bundling them into `SessionAddress` + `RunId` would be a pure shape
  change to an `internal sealed class`. Declined for the same cost/benefit
  reason as section B.
- **Section D (per-identity keep/drop calls).** Decisions only; recorded below
  for reference. No code change is implied by a decision in this table alone.
  possible.
- **Section D (per-identity keep/drop calls)**: decisions only, no code changes
  implied by this guide alone.
- **The architecture guard-rail test** described below: not implemented. Worth
  adding as a follow-up so a future PR can't reintroduce a stored duplicate
  silently.

## Why this guide exists

A survey of `src/` (September 2026) found:

- 115 identity value types (`readonly record struct … Id`/`… Key`), 110 of them
  in `AgentKit.Abstractions`.
- ~444 members named `<Other>Id` on ~213 types that reference _another_ entity's
  identity. The most repeated are `AgentId` (45), `SessionId` (42), `RunId`
  (24), `BranchId` (17), `ToolCallId` (23), `ExecutionLaneId` (14), `TurnId`
  (13).
- Many of those types carry the same identity twice: once as a loose `XxxId`
  property and again inside a richer value they also hold (`Correlation`,
  `Authorization.Scope`, `Address`, `Identity`, `Agent`, `Provider`).
- Hot paths such as `DefaultAgentLoop` build 5–7-argument tuples of loose ids
  from a request object that already contains every one of them.

The observation "we have `<Object>.<Related>Id` everywhere" is correct. The
conclusion "every reference should be an object reference, and ids are only
needed for persisted things" is correct in part and needs two corrections before
it is applied, otherwise it would break causal correlation, security scoping,
and the wire protocols.

## What the current design already requires (and why it stays)

These are settled in the normative docs and are not up for change here:

- Every domain identity is a dedicated immutable value type; public contracts
  never pass raw strings, GUIDs, or integers for one
  ([design principles](concepts/design-principles.md),
  [message and content model](concepts/message-and-content-model.md)).
- Causal relationships ("this result answers that call", "this prompt resumed
  that run") **must** use ids, never adjacency, text, or ambient state
  ([design principles](concepts/design-principles.md#make-causality-data)).
- A bare `SessionId` never crosses an isolation boundary; the complete key is
  `SessionAddress` (`AgentId` + `SessionId`)
  ([sessions](architecture/sessions.md)).
- Hook arguments carry only identities their stage has established; the base
  args type **must not** require `AgentId`/`SessionId`/`RunId`/`TurnId`
  ([extensions](concepts/extensions-hooks-and-middleware.md)).
  `AgentHookEventArgs` violated this until "Applied — pass 2" below fixed it by
  introducing `AgentScopedHookEventArgs`.

So ids are required in more places than "persisted artifacts, tool calls and
messages". They are required wherever the referent is:

1. **Durable or mutable** — sessions, branches, grants, artifacts, budget
   scopes, leases. There is no immutable in-process object to reference; the
   truth lives behind a store and the id is the reference.
2. **Remote or serialized** — `RunEvent` streams, provider requests/responses,
   session entries, checkpoints, audit records. Object graphs do not travel.
3. **A causal anchor across time** — `ToolCallId`, `OperationId`, `RunId`,
   `SessionEntryId.CausalParentId`, `SecurityRequestId`. The referent may be
   finished, evicted, or in another process when the reference is consumed.
4. **Authority-bearing** — a `SecurityGrant` is bound to identities, not to a
   live object that could be swapped after authorization.
5. **An observability tag** — logs, activities, and audit need bounded typed
   ids, not objects. (Metrics need bounded dimensions and use neither.)

Everything else is an in-process handoff, and the user's instinct applies there.

## Rule

Inside one process and one call chain:

1. **Reference the value you have.** If the referent is an immutable in-process
   value that the producer already holds (`AgentDefinition`, `ToolCallPart`,
   `OperationCorrelation`, `SecurityAuthorizationContext`, `ExecutionIdentity`,
   `SessionOperationContext`, `AgentRunRequest`), pass that value, not its id.
   Consumers that need the id read it from the value.
2. **Never store an identity twice on one type.** If a type holds a value that
   exposes an identity, it does not also hold that identity as a sibling
   property. A pass-through `=>` accessor is allowed when it removes churn for
   consumers; a second stored field that must be cross-validated is not.
3. **Use the existing composite when the referent is durable.** Sessions are
   addressed by `SessionAddress`. Protected work is scoped by
   `SecurityAuthorizationScope`. Operations are placed by `OperationCorrelation`
   / `InRunOperationCorrelation`. Budget and durable work use
   `BudgetScopeAddress` / `DurableOperationAddress`. Do not spell
   `AgentId, SessionId, RunId, TurnId, OperationId` out as five parameters when
   one of these already carries them.
4. **Give a thing an identity type only if something resolves or correlates by
   it.** An `XxxId` type is justified when at least one of these is true: it
   keys a store; it appears in a persisted record; it crosses a wire; it is a
   causal anchor consumed later; it is a log/activity/audit tag that the
   [observability](architecture/observability.md) contract calls for. A
   `readonly record struct` that only rides along in a request record and is
   never read back is not an identity, it is noise.
5. **Persisted and wire shapes keep ids.** Session entries, `AgentMessage`,
   `RunEvent`, provider contexts, grants, audit, checkpoints, artifact
   references, and usage entries are unchanged by this guide except where a
   loose tuple should become an existing composite (rule 3).

Test for rule 1 vs. rule 3: _could this referent change, be released, or be
reloaded from a store while the holder is alive?_ If yes, hold the id (or
composite address). If no, hold the value.

## Inventory and target shapes

Categories are ordered by value-to-risk ratio. Each item names the file, the
current shape, and the target **as surveyed**. This is the original research
record; it is not re-edited row by row as work lands. For what actually happened
to a given row, cross-reference "Applied — pass 1", "Applied — pass 2",
"Corrected on inspection", and "Evaluated and declined" above — several targets
described here turned out to need a different shape than first proposed
(`AgentRunRequest.Agent` stayed nullable, `ParentAgentId`/ `ParentSessionId`
were not bundled into a `SessionAddress`, hooks got an
`AgentScopedHookEventArgs` base class rather than an `IAgentScopedHookEventArgs`
interface).

### A. Redundant identity stored beside the value that already carries it

Remove the stored duplicate; keep a pass-through accessor only where many
consumers read it.

| Type                                                                                                                                   | Duplicate                                                         | Source of truth already held                                           | Target                                                                                                                                                                                                                                                                                                                                         |
| -------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------- | ---------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Abstractions/Loop/AgentRunRequest.cs:166,173,179`                                                                                     | `AgentId`, `SessionId`, `RunId`                                   | `Authorization.Scope.AgentId/SessionId/Correlation.RunId`; `Agent?.Id` | Make `Agent` non-nullable (drop the "legacy reduced request" constructor); expose `AgentId => Agent.Id`, `RunId => Authorization.Scope.Correlation` (typed as `InRunOperationCorrelation`), `SessionId` from the scope. Delete `ThrowIfInvalidRunAuthorization` cross-checks that only exist because of the duplication.                       |
| `Abstractions/Tools/AcceptedToolCall.cs:53–65` and `ToolCallResult.cs:121–129`                                                         | `AgentId`, `SessionId`, `RunId`, `TurnId`, `OperationId`          | `Authorization.Scope` (agent, session, correlation)                    | Keep `CallId`, `ToolId`, `GrantId` (causal anchors / persisted). Replace the five loose ids with `Scope` accessors; delete `ValidateIdentities`/`ValidateAuthorization` agreement checks. `ToolCallResult` is the authoritative terminal record and its projection is persisted, so keep it serializable: the scope value is already a record. |
| `Abstractions/Hooks/BeforeToolInvocationEventArgs.cs:45–46,52–55`, `BeforeModelRequestEventArgs.cs:57–60`, `RunStartedEventArgs.cs:52` | `RunId`, `TurnId` copied from `correlation`                       | base `Correlation`                                                     | Type the derived args' correlation as `InRunOperationCorrelation` and expose `RunId => Correlation.RunId`, `TurnId => Correlation.TurnId`. No stored copies.                                                                                                                                                                                   |
| `Abstractions/Sessions/SessionOperationContext.cs:69–73`                                                                               | `AgentId`, `SessionId`                                            | `Authorization.Scope`                                                  | Accessors over the scope; drop constructor cross-checks at `:50–56`.                                                                                                                                                                                                                                                                           |
| `Abstractions/Tools/ToolExecutionContext.cs:61–64`                                                                                     | `AgentId`, `SessionId?`                                           | `Authorization.Scope`                                                  | Same. Accept a `SessionOperationContext` (or the scope + identity) rather than seven loose arguments.                                                                                                                                                                                                                                          |
| `Session/SessionRunLease.cs:20–37`                                                                                                     | `AgentId`, `SessionId`, `ExecutionLaneId`, `OperationId`, `RunId` | `_context` (`SessionOperationContext`)                                 | Implement `ISessionRunLease` members as pass-throughs over `_context`.                                                                                                                                                                                                                                                                         |
| `Abstractions/Delegation/TaskDelegationPrompt.cs:76–78`                                                                                | `ParentRunId`                                                     | `Correlation.RunId`                                                    | Remove `ParentRunId`. `ParentAgentId`/`ParentSessionId` become one `SessionAddress Parent`.                                                                                                                                                                                                                                                    |
| `Abstractions/Tools/ToolProviderBinding.cs:28,32`                                                                                      | `SourceId`                                                        | `Provider.SourceId`                                                    | Accessor or removal.                                                                                                                                                                                                                                                                                                                           |
| `Conversations/ConversationSessionOptions.cs:18,26`                                                                                    | `AgentId` beside `Agent`                                          | `Agent`                                                                | Make it either/or; an options type must not expose two writable ways to say the same thing.                                                                                                                                                                                                                                                    |
| `Abstractions/Artifacts/ArtifactStorePrepareRequest.cs:70,72`                                                                          | `TenantId`, `CreatedBy`                                           | `Identity` (`ExecutionIdentity`)                                       | Accessors over `Identity`.                                                                                                                                                                                                                                                                                                                     |
| `Abstractions/Sessions/SessionDescriptor.cs:68–77`, `Session.InMemory/SessionRecord.cs:43–52`                                          | `Address` + `TenantId` + `OwnerId` split                          | —                                                                      | Not a duplicate (tenant/owner are not in `SessionAddress`), but hold an `ExecutionIdentity Owner` value instead of two loose ids.                                                                                                                                                                                                              |

### B. Loose id tuples where a composite value already exists

Replace `(AgentId, SessionId)` with `SessionAddress`, and
`(AgentId, SessionId?, Correlation)` with `SecurityAuthorizationScope` or the
`SessionOperationContext` that already wraps it, in:

- `Abstractions/Artifacts/Artifact{Prepare,Finalize,Abort,Read,Delete}Request.cs`
- `Abstractions/Input/HumanQuestionRequest.cs:62–70`,
  `HumanQuestionPrompt.cs:57–65`
- `Abstractions/Tools/ToolDiscoveryRequest.cs:73–79`,
  `ToolCatalogSnapshot.cs:93–99`
- `Abstractions/Compaction/CompactionOperationContext.cs:63–69`
- `Abstractions/Providers/ProtectedSemanticOperationContext.cs`
- `Abstractions/Input/InputAdmissionRequest.cs`, `InputPromotionRequest.cs`,
  `InputPromotionContext.cs`, `AdmissionReceipt.cs:36–39`
- `Abstractions/Sessions/SessionRunLeaseRequest.cs`,
  `SessionRunReleaseRequest.cs`, `SessionRunStartRequest.cs`
- `Session/DefaultSessionCoordinator.cs:531` (already has `location.Address`)

Where the type is persisted (`AdmissionReceipt` via `StoredAdmission`,
`CompactionOperationContext` via `CompactionSessionEntry`), the composite is
still the right shape; `SessionAddress` and `SecurityAuthorizationScope` are
already serialized elsewhere. Add the codec fields once, in the owning package's
codec.

### C. Producers that have the object and pass its id

These are call-site changes that follow from A and B; listed so the loop
refactor is done once, not per type.

- **C1** `Loop/DefaultAgentLoop.cs:1341–1348` — build `ToolExecutionContext`
  from `turnSessionContext` and `toolCall` (the `ToolCallPart`), not from
  `request.AgentId, request.SessionId, toolCall.CallId, …`.
- **C2** `Loop/DefaultAgentLoop.cs:678–690` — `ContextAssemblyRequest` takes
  `agent` (`AgentDefinition`) and the turn correlation; drop `AgentId`, `RunId`,
  `TurnId` parameters. It already receives the definition inside
  `ContextAssemblyEvidence`.
- **C3** `Abstractions/Hooks/AgentHookEventArgs.cs:36–56` — remove `AgentId` and
  `SessionId?` from the base to match the concept doc. **Done** in "Applied —
  pass 2" above, via a new intermediate `AgentScopedHookEventArgs` base rather
  than moving the properties onto each of the three concrete hook-args types
  individually. `HookInvocationId` stays on the base; it is a log/activity
  correlation tag ([extensions](architecture/extensions.md)) — renaming it to
  match the architecture doc's `HookDispatchId`/per-invocation
  `HookInvocationId` split was evaluated and explicitly deferred (see "Applied —
  pass 2" for why).
- **C4** `Loop/DefaultAgentLoop.cs:323–329,644–650`,
  `AgentKit/AgentEngine.cs:289,548` — construct `SessionOperationContext` from
  the authorization value and identity only.
- **C5** `IO/RunEventHub.cs:79` — `RunEventStream` takes the run's
  `SessionAddress` and `RunId` once (or the accepted-run state), not four loose
  ids.
- **C6** `Goals/DefaultTaskDelegationBroker.cs:183–202` — read
  `Prompt.Correlation.RunId` and `Prompt.Parent`.

### D. Identity types with no store, no wire, no consumer

Decide per type using rule 4. Survey result:

| Identity                                                                                                                             | Consumed by                                                 | Decision                                                                                                                                                                                                                                                                                       |
| ------------------------------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `HookInvocationId`                                                                                                                   | `HookLog`, `DefaultHookDispatcher` activity tags            | Keep. Note: the architecture blueprint wants this split into a shared `HookDispatchId` plus a per-registration `HookInvocationId`; current code still uses one `HookInvocationId` for the whole dispatch. See "Applied — pass 2" for why that rename is out of scope here.                     |
| `NetworkOperationId`, `ProcessOperationId`, `LanguageQueryId`, `WebSearchRequestId`                                                  | host-boundary logs/activities and in-memory traces          | Keep; these are the per-effect correlation the host-access specs require for audit. Confirm each is actually tagged on the activity; if a type is generated but never emitted, drop it.                                                                                                        |
| `QuestionId`, `QuestionOptionId`                                                                                                     | `DefaultHumanQuestionBroker` (in-memory)                    | `QuestionId` is a causal anchor between prompt and answer across an await and may be surfaced to a UI; keep. `QuestionOptionId` correlates a chosen option back to the prompt; keep only if a channel adapter sends it over the wire, otherwise reference the `HumanQuestionOption` value.     |
| `EmbeddingRequestId`, `EmbeddingInputId`                                                                                             | provider pipeline correlation                               | Keep; they cross the provider boundary and correlate batch items to outcomes.                                                                                                                                                                                                                  |
| `WorkspaceMutationId`                                                                                                                | `WorkspacePatchEntry`, `AtomicFileReplaceRequest`; no store | Keep only if it appears in the mutation audit record; otherwise drop.                                                                                                                                                                                                                          |
| `ArtifactPreparationId`                                                                                                              | in-memory prepare map key                                   | Keep; it is the handle between prepare and finalize across calls.                                                                                                                                                                                                                              |
| `DeferredRequestId`, `ApprovalResponseId`, `UsageEntryId`, `MediaId`, `ExternalRequestId`, `CheckpointId`, `GoalId`, `GoalAttemptId` | one or two files, no generator or no store in `src/`        | Either the owning feature is unfinished (goals, checkpoints, deferral) or the id is decorative. Do not delete ahead of the owning design; mark each with a `// TODO(identity)` and resolve when that feature's store lands. `MediaId` and `ExternalRequestId` have no reader today and can go. |

Do **not** remove an identity because its store is in-memory. In-memory adapters
are storage adapters; the contract is the same as SQLite.

### E. Unchanged

`AgentMessage`, `ContentPart` (`ToolCallPart.CallId`, `ToolResultPart.CallId`),
`SessionEntry` and every subclass, `RunEvent` and subclasses,
`CommittedToolResultReference`, `SecurityGrant`, `SecurityAuditRecord`,
`SecurityEnforcementIntent`, `BudgetScopeAddress`, `DurableOperationAddress`,
`DurableCheckpoint`, `RecoverableOperationDescriptor`, `ArtifactReference`,
`UsageAccountingEntry`, `ProviderResponseIdentity`, `AssistantResponseMetadata`,
`LlmRequestContext`, `ModelResponse*`, `ISessionRunLease` (interface shape; only
the implementation changes). These are persisted, wire, causal, or authority
shapes and keep identity references by design.

## How to make one change

1. Pick one row from A or B. Load the owning skill (tools, sessions, hooks,
   loop) and read the owning concept doc; confirm the type is not in a persisted
   or wire path you missed by searching the `Session`, `*.Sqlite`, and
   `PortableSessionJsonPolymorphism` codecs for it.
2. Write the failing test first in the existing `<Type>Tests` fixture: assert
   the accessor reads through the held value, and delete the tests that asserted
   `args.RunId == correlation.RunId`-style agreement between two stored copies.
3. Change the type: remove the duplicate stored property, add the pass-through
   accessor if consumers need it, delete the cross-validation guard and its
   `ArgumentExceptionExtensions` helper if nothing else uses it. Keep
   constructor parameter order stable for the parameters that remain; removed
   parameters are a breaking change and are listed in the compatibility snapshot
   diff.
4. Fix producers (section C) so the call site passes the value, then fix
   consumers to read through it. Do not leave a shim that reconstructs the
   tuple.
5. Update XML docs on the type. The `<param>` for a removed id goes away; the
   `<remarks>` should say which held value is the source of the identity.
6. Update `tests/AgentKit.Compatibility.Tests/Snapshots` by running the snapshot
   test and reviewing the diff; every removed member must be one you intended.
7. Run `make format`, `make lint`, `make build`, `make test`.

Batch by owner, not by identity type: one change for `AgentRunRequest` + loop
call sites, one for the tool trio (`ToolExecutionContext`, `AcceptedToolCall`,
`ToolCallResult`), one for hooks (C3 + the three derived args), one for session
contexts and lease, one for artifacts/questions/discovery requests. That keeps
each compatibility diff reviewable.

## Guard rail: evaluated and declined

The originally proposed guard rail was a `tests/AgentKit.Architecture.Tests`
check that fails when a public type declares a stored property of identity type
`T` and also declares a stored property whose type exposes a property of type
`T` with the same name.

`AgentKit.Architecture.Tests` only evaluates the MSBuild project-reference graph
today (`ProjectReferenceGraph.cs`); it has no dependency on
`Microsoft.CodeAnalysis` and never loads or reflects over compiled production
assemblies. Building this guard rail for real means one of:

- **Reflection over the built `AgentKit.Abstractions.dll`.** This can find the
  candidate property pairs, but cannot distinguish a computed forwarding
  property (`AgentId => Authorization.Scope.AgentId`, safe) from a genuinely
  duplicated stored one (`AgentId { get; }` assigned in the constructor, unsafe)
  — both compile to a `get`-only property in metadata. Telling them apart needs
  IL inspection of the getter body, which is a second, separate piece of
  infrastructure this repository does not have.
- **A Roslyn syntax walker over `src/AgentKit.Abstractions/**/*.cs`.** This can
  see the difference (an arrow body calling through another property vs. a field
  assigned in the constructor), but this repository has no precedent for a
  syntax-based policy test today — the XML-doc-coverage requirement, the closest
  analogous rule, is enforced by the compiler (`CS1591` as an error via
  `GenerateDocumentationFile`), not by a custom test — and a first version of
  such a walker is easy to get wrong: `ArtifactStorePrepareRequest` this pass
  and `ConversationSessionOptions`/`ToolProviderBinding` are all found
  legitimate exceptions to the "one copy" instinct on closer inspection, and a
  rule tight enough to catch a real duplicate but loose enough to allow those
  three (and any future one with a similarly good reason) needs real design and
  iteration, not a first draft added as a follow-up.

Given the practical regression protection already in place —
`tests/AgentKit.Compatibility.Tests` freezes every public property shape, so
reintroducing a stored duplicate that changes visible shape shows up as a
snapshot diff a reviewer has to consciously approve — a bespoke static analyzer
was evaluated and declined as disproportionate to the risk it removes. If a
second contributor reintroduces this pattern and it becomes a recurring problem,
revisit the Roslyn-syntax-walker option specifically, not the reflection option.

`docs/concepts/design-principles.md` was extended with the "Reference the value
you hold; never store an identity twice" rule regardless, so reviewers have a
normative sentence to cite by hand.

## Non-goals

- Replacing identity value types with raw primitives. Not proposed and not
  allowed.
- Holding live mutable objects (session records, leases, stores) as references
  inside requests or events. That reintroduces ambient state and breaks the "no
  service locator, no current-agent" invariants.
- Passing `AgentDefinition` or other large immutable values across the
  `RunEvent` stream or into persisted entries to avoid an id. Wire and storage
  shapes keep ids.
- Renaming `<Other>Id` properties that survive to `<Other>`; a property that is
  an id is named as one.
