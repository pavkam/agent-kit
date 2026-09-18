# Full-wiring implementation plan

This plan takes AgentKit from a partially wired runtime to the complete
composition described by [`docs/architecture`](architecture/index.md) and the
normative [concept specifications](concepts/index.md). Those documents are the
design authority; this plan orders the work, records the decisions needed to
close each gap, and defines what "done" means per workstream. The
[implementation ledger](implementation-progress.md#component-coverage) remains
the per-owner status table and is updated as each workstream lands.

The plan was produced from a docs-versus-source survey at commit `7ee67aeb`.
Every gap below was verified by the absence of a non-comment source reference or
by a source remark declaring itself a "deliberately reduced stand-in".

## Ground rules

- `AgentEngine` is the process-level multi-agent facade. One engine hosts many
  immutable agent definitions, many sessions, and many concurrent runs. Nothing
  in this plan introduces a second runtime, a per-agent engine, or a hidden
  default composition.
- Dependency direction is unchanged: `AgentKit.Abstractions` references nothing
  concrete; runtime packages reference Abstractions (and the exporter-free
  `AgentKit.Observability`); the `AgentKit` facade references Abstractions;
  leaves (`.InMemory`, `.Sqlite`, `.Json`, providers, transports, hosting,
  evaluation, goal hosting) sit at the edge. `AgentKit.Simple` is an application
  leaf and may reference anything.
- Every persistent store family ships `.InMemory`, `.Sqlite`, and `.Json`
  adapters that run one shared conformance suite. Runtime packages register
  contracts, catalogs, and selectors but never a concrete store.
- Every new boundary is observable through the shared `AgentKit` activity
  source, meter, and source-generated `LoggerMessage` events, without logging
  content.
- Every protected effect fails closed, consumes its grant at the effecting
  boundary, re-enforces at the low-level host boundary, and emits the required
  audit records.
- Public API changes regenerate the compatibility snapshots. Contract breaks
  toward the documented shape are permitted; each is listed in the workstream's
  commit message and in the ledger.

## Per-workstream process

Each workstream is delivered as one or more commits in the order below. Within a
workstream:

1. Read the owning skill, architecture page, and concept specification.
2. Add or extend Abstractions contracts first, with focused argument-guard tests
   and XML documentation.
3. Implement the first-party runtime through public DI registrations. Add the
   `ComponentKey`/profile key to `AgentDefinition` and the matching check to
   `AgentCompositionValidator` in the same change.
4. Add conformance suites for any swappable contract and run them against every
   adapter.
5. Wire the consumer (loop, engine, tools, Simple) so the feature is reachable
   from `AgentEngine` without private access.
6. Update `AgentKit.Simple` sugar, use-case documents, guides, package READMEs,
   the owning skill, and the ledger row. Remove stale "stand-in" remarks.
7. Run the affected test projects, then
   `bash tests/AgentKit.Compatibility.Tests/update-snapshots.sh` if the public
   surface changed, then `dotnet format`, and commit.

`make format`, `make lint`, `make build`, and `make test` run before the final
push of a batch of workstreams.

## Ordered workstreams

The order follows dependencies. Later workstreams consume the contracts landed
earlier; a workstream is not started until its listed dependencies are merged.

| #   | Workstream                                    | Depends on | Size |
| --- | --------------------------------------------- | ---------- | ---- |
| 1   | Run envelope, admission, lanes, attach/cancel | –          | XL   |
| 2   | Hook kernel completion                        | –          | L    |
| 3   | Permissions algebra, approvals, audit         | 2          | L    |
| 4   | Tool runtime switch                           | 2, 3       | XL   |
| 5   | Host access completion                        | 3          | L    |
| 6   | MCP as a tool source                          | 4, 5       | XL   |
| 7   | Provider runtime and semantic operations      | 2          | L    |
| 8   | Structured output completion                  | 2, 7       | M    |
| 9   | Context assembly pipeline                     | 2          | L    |
| 10  | Context compaction completion                 | 7, 9       | M    |
| 11  | Budgets completion                            | 8, 10      | M    |
| 12  | Durable execution                             | 1, 3       | XL   |
| 13  | Goals, joins, worker hosting                  | 1, 12      | L    |
| 14  | Memory and retrieval                          | 7, 9       | XL   |
| 15  | Artifacts completion                          | 4, 12      | M    |
| 16  | Identity ingress and revalidation             | 1          | M    |
| 17  | Observability completion                      | 1          | M    |
| 18  | Definition and composition validation sweep   | 1–17       | M    |
| 19  | Evaluation                                    | 1, 18      | M    |
| 20  | Documentation reconciliation                  | 1–19       | M    |

Sizes are relative: M is a few focused commits, L is a package-scale change, XL
spans several packages and their conformance suites.

---

### 1. Run envelope, admission, lanes, attach/cancel

**Goal.** Every run enters through admission, executes under the durable session
lane protocol, publishes a settlement-aware final envelope, and can be attached
to or cancelled by `RunId`. Steering and follow-up input reach a running loop.

**Gaps closed.** `SendAgentAsync` appends `UserMessage` directly under the
process-local `SessionLaneRegistry` and throws on busy instead of returning
`AgentRunRejected<T>`; no first-party `IInputQueue`; session stores implement
`ProvisionLaneAsync`, `AdmitInputAsync`, `AcceptRunAsync`, `LoadRunStateAsync`,
`ReleaseRunAsync`, and `ISessionRunCoordinator.AcquireAsync` with zero
production callers; the loop never consumes `IInputCoordinator`; two disjoint
outcome families (`Loop/AgentRun*` versus `Results/Run*` +
`AgentRunFinished<T>`); `AgentLoopResult` carries no settlement, usage, or
cursor; `IRunEventSink` and the run-scope factory/validator/lease types do not
exist.

**Design decisions.**

- Unify outcomes on the documented `Results` family. `AgentLoopResult` becomes:

  ```csharp
  public sealed record AgentLoopResult(
      AgentId AgentId, SessionId SessionId, BranchId BranchId, RunId RunId,
      AgentRunOutcome Outcome, RunSettlementOutcome Settlement,
      MessageCursor PreviousCursor, ImmutableArray<AgentMessage> NewMessages,
      ValidatedOutput? Output, RunUsage Usage);
  ```

  The `Loop/AgentRun*` records become causes inside `RunFailed`,
  `RunLimitReached`, and `RunPolicyHalted` rather than parallel terminals. This
  is a documented break.

- `IAgentLoop.RunAsync(AgentRunInvocation, CancellationToken)` replaces the
  interim `(AgentRunRequest, AgentRunServices, …)` shape. `AgentRunServices`
  gains `IInputCoordinator`, `SessionExecutionCapability`,
  `IModelRequestExecutor` (placeholder adapter over `ILlmModelResolver` until
  workstream 7), `IToolExecutor` (adapter over `IToolInvoker` until workstream
  4), `IOutputPublisher`, `IHookDispatcher`, and `BudgetExecutionCapability`.
  Hooks stop being constructor-injected into `DefaultAgentLoop`.
- First-party `SessionBackedInputQueue` in `AgentKit.IO` persists admission
  through the selected session store's lane protocol. Capacity, delivery class,
  ordering, idempotency, and backpressure are options on `AgentIOOptions`.
- `AgentKit.IO` gains `AddAgentIO(Action<AgentIOOptions>?)`,
  `DefaultOutputPublisher`, `IRunEventSink` + `RunEventSinkRegistration` +
  `AddRunEventSink<TSink>`, and `IOutputBackpressurePolicy`. Required sinks
  block clean settlement on failure.
- `AgentKit` facade gains `AgentEngineRuntime`, `AgentResolution`,
  `AgentSessionCreateRequest`/`AgentSessionCreationResult`, `AgentRunPlan`,
  `IAgentRunPlanCompiler`, `IAgentRunScopeFactory`, `AgentRunScopeLease`,
  `AgentRunResult<TOutput>`, `AgentRunStreamStartResult`, and the public
  `Agent.RunAsync<TOutput>` / `Agent.StreamAsync<TOutput>` /
  `Agent.AttachAsync(RunId)` / `Agent.CancelAsync(RunId, …)` /
  `Agent.SteerAsync` / `Agent.FollowUpAsync` surface. `SendAsync` remains as a
  thin wrapper that returns `AgentRunRejected<T>` on lane contention.
- `SessionLaneRegistry` is replaced by the store-backed run coordinator; it is
  deleted, not kept as a fallback.
- `IConversationSession` becomes a per-session handle over the engine (created
  by `AgentEngine.OpenSessionAsync`), no longer a singleton compat path.
- Loop drains steering input at the turn boundary and follow-up input after
  settlement, per the state-machine specification (`PromotingInput`).

**Deliverables.** Abstractions: `AgentRunInvocation`, extended
`AgentRunServices`, unified outcome family, `IRunEventSink`,
`RunEventSinkRegistration`, `IAgentRunScopeFactory`, `IAgentRunScopeValidator`.
`AgentKit.IO`: `SessionBackedInputQueue`, `DefaultOutputPublisher`,
`AddAgentIO`. `AgentKit`: runtime, plan compiler, scope factory, public run API.
`AgentKit.Loop`: invocation shape, promotion, settlement envelope.
`AgentKit.Conversations`: per-session handle. `AgentKit.Simple`: `AskAsync`,
`AskAsync<T>`, `StreamAsync`, `AttachAsync`, `CancelAsync` over the new API.

**Tests.** Input-queue conformance suite run against InMemory, Sqlite, and Json
session stores; lane contention returns `AgentRunRejected<T>` without a store
append; attach receives replayed events plus live tail; cancel by `RunId`
settles the run and releases the lane; steering is visible on the next turn;
follow-up is admitted after settlement; recovery of an open run preserves
identities; required sink failure blocks clean settlement; observability for
admission, promotion, publication, and settlement.

**Docs.** `guides/composition.md`, `getting-started.md`, all `use-cases/*.md`,
`AgentKit.IO` and `AgentKit` READMEs, `agentkit-input-output` and
`agentkit-agent-loop` skills. Ledger rows: Engine, Loop, IO, Sessions (mark
SQLite present, JSON present).

**Acceptance.** `docs/architecture/composition-and-configuration.md` L272–445
compiles as written against the public surface; no production code references
`SessionLaneRegistry`; `AgentRunFinished<T>` is produced by the facade for every
terminal run.

---

### 2. Hook kernel completion

**Goal.** Typed hook points with a closed `HookPointDefinition`, a dispatch
context that flows through every component, hook profiles selected per
definition, and the full documented set of first-party points.

**Gaps closed.** `IHookDispatcher.DispatchAsync` takes an enumerable plus a
delegate instead of a `HookPointDefinition<THook,TEventArgs>`;
`HookDispatchContext`, `HookPointDefinition`, `HookInvoker`,
`IHookMutationValidator`, `HookInvocationContext`, `HookDispatchMetadata`,
`HookPointKind`, `IHookProfileSelector`, `IHookCatalog`,
`IHookRegistrationSource`, `IHookOrderResolver`, `IHookInstanceFactory`,
`IHookActivationLease`, `IHookInvocationTracker`, and diagnostic sinks are
absent; `HookProfileKey` is unused; only three points exist and only the loop
dispatches.

**Design decisions.**

- Points are declared as `static readonly HookPointDefinition<THook,TArgs>`
  values in `AgentHookPoints` (and in owning contract assemblies for third
  parties). The definition carries point id, kind (observational, mutating,
  short-circuiting), minimum failure mode, and the mutation validator.
- `HookDispatchContext` is created per run scope by the engine, carries the
  selected hook profile, invocation tracker, and identities established so far,
  and is passed explicitly to every component that dispatches.
- Hook registrations are captured into an immutable `IHookCatalog` at build
  time; `IHookProfileSelector` resolves `AgentDefinition.HookProfile` to a
  profile that filters and orders registrations. Ordering resolution validates
  soft before/after, hard dependencies, singleton anchors, contradictions, and
  cycles at composition.
- Points added in this workstream (interfaces plus `EventArgs`): turn
  started/committed, input admitted/promoted, session appended/branched, history
  validated, context assembled, model selected/sent/response/stream event, tool
  discovered/validated/authorized/result, output validated/published, security
  presented/decided/approved/audited, compaction proposed/activated, memory and
  goal points as reserved definitions consumed by later workstreams.
- Failure policy is monotonic across point, host option, profile, registration,
  and narrower scope. Only observational points may isolate.

**Deliverables.** Abstractions hook contracts and points; `AgentKit.Hooks`
kernel rewrite, catalog, profile selector, order resolver, tracker;
`AddAgentHooks(Action<AgentHookOptions>?)`, `AddHookProfile`,
`Add<Point>Hook<T>`/`Replace*`; `AgentDefinition.HookProfile`; validator check
for one kernel, one point catalog, one profile selector, and a resolvable
profile per definition.

**Tests.** Ordering matrix, mutation validation after each hook, reentrancy
depth, failure-mode precedence, isolation leaks nothing, profile selection per
definition, each new point dispatched from its component (added as the component
lands), observability for dispatch and invocation.

**Docs.** `architecture/extensions.md`,
`concepts/extensions-hooks-and- middleware.md` scenario list, `agentkit-hooks`
skill, ledger Hooks row.

---

### 3. Permissions algebra, approvals, audit

**Goal.** Policy snapshots resolvable by reference, constraint intersection,
durable approval deferral and resolution, revocation reasons, and required audit
at every grant-consuming boundary.

**Gaps closed.** `ISecurityPolicyCatalog`, `ISecurityPolicySelector`,
`SecurityPolicySnapshotResult`, `IApprovalHandlerDispatcher`,
`ISecurityGrantIssuer`, `ISecurityDecisionStore`, `RevocationReason`,
`GrantRevocationResult`, `SecurityRevocationTrigger` absent;
`ISecurityAuthority.AuthorizeAsync` lacks the hook context;
`IApprovalBroker.ResolveAsync` absent; no SQLite or JSON approval store; no
durable deferral; approver identity not bound into grants; allow-constraint
intersection not implemented; revocation generation is static; audit emitted
only for `GrantIssued`, `Approval`, and session-store consumption; `Request`,
`Decision`, `GrantLifecycle`, and `Enforcement` never emitted;
`ISecurityAuthoritySelector` used only by `DefaultSessionCoordinator`; no
bounded infrastructure bootstrap capability.

**Design decisions.**

- `SecurityAuthority` takes `ISecurityPolicyCatalog`, `ISecurityGrantIssuer`,
  `ISecurityDecisionStore`, `ISecurityAuditDispatcher`, and an
  `IHookDispatcher`; it evaluates the selected snapshot, intersects bounded
  scope, expiry, use count, and effect constraints across allow proposals, and
  denies on empty intersection. Managed constraints are non-overridable.
- Approval requests persist through `IApprovalStore` before any handler runs.
  `DefaultApprovalBroker.ResolveAsync(ApprovalResponse)` completes deferred
  approvals and binds the authorized responder identity into the issued grant. A
  `DeferredOperationRequest` is recorded when no inline handler answers.
- New leaves `AgentKit.Permissions.Sqlite` (approval store added beside the
  grant store) and `AgentKit.Permissions.Json` approval store; approval-store
  conformance runs on all three.
- Audit is emitted for `Request` and `Decision` in the authority, for
  `GrantLifecycle` in every grant store (consume, expire, revoke), and for
  `Enforcement` in every effecting boundary: file system, network, processes,
  artifacts, goals, human questions, plan state, tools, MCP, durability. Each
  boundary receives `ISecurityAuditDispatcher` and honors `Required` delivery by
  failing closed.
- All feature tools and the artifact coordinator move from an unkeyed
  `ISecurityAuthority` to `ISecurityAuthoritySelector` resolved per definition.
- A `SecurityControlPlaneBootstrap` capability authorizes the authority's own
  grant/audit persistence through a bounded, non-recursive path.

**Deliverables.** Contracts above; `AgentKit.Permissions` authority, broker,
policy catalog, decision store, bootstrap; `.InMemory`, `.Sqlite`, `.Json`
approval and decision stores; audit wiring across the packages listed; validator
check for one authority selector, one policy catalog, one approval broker.

**Tests.** Algebra matrix (deny precedence, intersection, empty intersection,
managed constraints), approval defer/resolve/replay across store adapters,
responder binding, revocation reasons, audit assertions per boundary (record
kind, safe fields, no content), required-audit failure closes the boundary,
selector resolution per definition.

**Docs.** `guides/permissions.md#audit`,
`architecture/permissions-and-human- control.md` acceptance list,
`agentkit-tools-and-permissions` skill, ledger Permissions row.

---

### 4. Tool runtime switch

**Goal.** Replace the legacy `ITool` catalog path with the documented tool
runtime: authored toolsets resolved through a materialized registration catalog,
provider captures, invoker leases, an executor that owns scheduling, retries,
normalization, and the authoritative `ToolCallResult`.

**Gaps closed.** `AddAgentTools` wires `ToolCatalog` over `IEnumerable<ITool>`,
`AllowListToolAuthorizer`, and `DefaultToolInvoker`; the documented
`AddAgentTools(ComponentKey<IToolExecutor>, Action<ToolRuntimeOptions>?)` and
`AddTool<TInvoker>(ToolDescriptor, ServiceLifetime)` do not exist;
`IToolExecutor` is absent; the loop iterates tool calls sequentially and never
reads `ToolSchedulingMode`; retries are not honored; `AgentDefinition` carries
`ImmutableArray<LlmToolDefinition> Tools` instead of `Toolsets`;
`IToolAuthorizer` is a reduced stand-in for the security authority;
`Tools.WebSearch` has no first-party provider.

**Design decisions.**

- `AgentDefinition.Toolsets : ImmutableArray<ToolsetReference>` replaces
  `Tools`/`ToolChoice`; the engine resolves toolsets at composition into
  captured publications and validates source identity and membership. The
  `LlmToolDefinition` list becomes a derived projection.
- `DefaultToolExecutor` owns: schema validation under the compiled profile,
  security authorization through `ISecurityAuthoritySelector`, hook dispatch
  (validate, authorize, result), scheduling segments (parallel with concurrency
  keys and a bounded limiter; `Sequential` barrier), retry policy for
  `retryable` outcomes with idempotency classification, normalization of raw
  invoker evidence into `ToolCallResult`, and the loss-aware `ToolResultPart`
  projection. Every identified call reaches one terminal record, including
  pre-invocation rejection.
- Oversized results spill to `IArtifactCoordinator` when composed
  (`ToolResultArtifactContent`), otherwise truncate with evidence.
- `IToolAuthorizer`/`AllowListToolAuthorizer` are removed; allow-listing becomes
  a toolset selection concern.
- `Tools.WebSearch` gets a first-party provider over `INetworkTransport` against
  a configurable search endpoint, with no fabricated default endpoint.

**Deliverables.** `IToolExecutor`, `ToolRuntimeOptions`, executor, scheduler,
retry policy, projection, `AddAgentTools(key, …)`, `AddTool<TInvoker>`,
`AddToolset`, definition and validator changes, migration of all 17
`AgentKit.Tools.*` packages to descriptor + invoker registration and the
selector-based authority, Simple `WithTools` over toolsets.

**Tests.** Scheduling (parallel segments, sequential barrier, concurrency keys,
limiter), retries with idempotency, one terminal record per call across
rejection paths, projection bounds, spill, capture ownership and lease
lifecycle, merge-policy collision handling, DI replacement, observability.

**Docs.** `architecture/tools.md` acceptance list, tool package READMEs,
`agentkit-tools` skill, ledger Tools row.

---

### 5. Host access completion

**Goal.** File-system, network, and process contracts match their
specifications: stream-based bounded reads, explicit write dispositions with
fingerprints and atomic target-state semantics, keyed profiles, directory
creation as a declared effect, partitioned connection reuse, stream uploads,
handle-based process execution with streamed output and termination, and
deterministic backend conformance.

**Gaps closed.** File system: `FileTarget`, `ResolvedFileTarget`,
`AuthorizedFileRead/Write`, `FileWriteContent`, `FileReadBounds`,
`FileWriteDisposition`, `FileWriteOutcomeKind`, `FileWriteSuccess` with
fingerprints, `IFileReader`/`IFileReadHandle`/`IFileWriter`/
`IFileMetadataReader`/`IFileChangeSource`/`ITemporaryFileStore`/
`IFileSystemSelector`, capabilities, policies, keyed registration, directory
create operation, audit, arm64 `O_NOFOLLOW`/`O_DIRECTORY` constants. Network:
stream upload with staged fingerprint, separate resolution/request/response
bounds, route-partitioned pooling, proxy/TLS/decompression descriptors,
multi-profile selection, audit, sent-bytes evidence. Processes: handle-based
`IProcessExecutor.StartAsync` returning `IProcessHandle` with `ReadOutputAsync`,
`Completion`, `TerminateAsync`; `IExecutableResolver`; executor and sandbox
selectors; shared `SideEffectCertainty`; exited versus signalled status; child
effect scope intersection; concurrency permits; audit; authority reevaluation; a
shared process-executor conformance suite.

**Design decisions.**

- The reduced `IFileSystem.ReadAsync/WriteAsync` and `IProcessRunner.RunAsync`
  remain as thin adapters over the new contracts for one release, marked
  obsolete, so tools migrate incrementally within this workstream.
- Handle-based process execution is the prerequisite for MCP stdio transports
  (workstream 6) and long-running terminals.
- `AgentKit.Processes.Scripted` and `AgentKit.Network.InMemory` implement the
  new contracts and run the conformance suites; tests that require a sandbox
  binary skip explicitly instead of passing vacuously.

**Deliverables.** Contract additions, `AgentKit.FileSystem` (+`.InMemory`),
`AgentKit.Network` (+`.InMemory`), `AgentKit.Processes` (+`.Scripted`), tool
migrations (`ReadFile`, `WriteFile`, `Edit`, `Patch`, `Glob`, `Search`,
`ListDirectory`, `Command`, `WebFetch`), conformance suites for file system,
network transport, and process executor.

**Tests.** Disposition matrix with precondition fingerprints, bounded stream
reads at fragment boundaries, directory creation denied without its own grant,
symlink/TOCTOU cases, upload fingerprint enforcement, pool partitioning,
redirect re-authorization, process termination certainty, output streaming with
truncation evidence, permit exhaustion, audit per boundary.

**Docs.** `guides/file-system.md`, host architecture pages,
`agentkit-host- access` skill, ledger rows for File system, Network, Processes.

---

### 6. MCP as a tool source

**Goal.** MCP endpoints are configured, authorized, transported through the host
boundaries, negotiated, and exposed as `IToolProvider` sources in the tool
runtime, with resource and prompt adapters and a first-party server.

**Gaps closed.** No `McpEndpoint`, `McpServerKey`, `McpTransportProfile`,
`McpAuthenticationReference`, `McpEndpointBounds`, session/request identities,
capability profiles, `IMcpClientSession`, `IMcpClientSessionFactory`,
`IMcpEndpointCatalog`, `IMcpCapabilityProfileCatalog`, request/response
envelopes, `McpToolProvider`/`McpToolInvoker`, transport factories,
resources/prompts/sampling/elicitation/roots adapters, OAuth binding,
namespace/instruction/root rules, catalog generation, `IMcpServer` primitives,
or security/audit integration. No `ToolSourceId` is registered by MCP.

**Design decisions.**

- Stdio transports launch through the process executor (workstream 5) under a
  process grant; HTTP/streamable transports send through `INetworkTransport`
  under a network grant. The SDK transport is wrapped, never handed raw sockets
  or processes.
- `McpToolProvider` produces one independent discovery capture per request with
  explicit `McpCatalogVersion`, borrowed invokers, and no alias inference. Tool
  metadata is untrusted and never grants permission.
- Capability profiles pin protocol era/version, negotiated capabilities, and
  bounds per endpoint revision; a changed server catalog invalidates the capture
  and triggers re-preflight.
- OAuth callback and audience binding are endpoint-bound and use the credential
  profile machinery from workstream 7 where available, otherwise the existing
  `IOAuthAccessTokenProvider`.
- `AgentKit.Mcp.Server` exposes `IMcpServer` with `AgentKitPrimitiveHandler`
  over the engine's tool runtime and resource sources.

**Deliverables.** `AgentKit.Mcp` contracts, `AgentKit.Mcp.Client` sessions,
transports, provider, adapters, `AddMcpClient`, `Add/ReplaceMcpStdioEndpoint`,
`Add/ReplaceMcpHttpEndpoint`, `Add/ReplaceMcpCapabilityProfile`;
`AgentKit.Mcp.Server` runtime; Simple `WithMcpServer`.

**Tests.** Loopback stdio and HTTP servers, negotiation matrix, catalog change
detection, capture ownership, grant denial before transport start, OAuth
refresh/rotation, unknown notification policy, resources/prompts round trips,
server primitive conformance, observability.

**Docs.** `architecture/mcp.md` acceptance list, `agentkit-mcp` skill, ledger
MCP row, use case additions.

---

### 7. Provider runtime and semantic operations

**Goal.** Model requests flow through a request executor bound to endpoint and
credential profiles with fallback and resilience; embeddings and reranking have
selectors and executors; provider leaves are observable.

**Gaps closed.** `IModelRequestExecutor`, `ModelExecutionRequest/Result`,
`ModelFallbackRequired`, `IProviderProfileRuntimeSelector`,
`IProviderProfileRuntimeLease` absent; `ProviderOperationBinding` and profile
references defined but unconsumed; no resilience pipeline or same-model retry;
no reranker in Cohere or OpenRouter; embedding selector/executor and the
semantic-operation result families absent; `EmbeddingSpaceIdentity` lacks
normalization/truncation/endpoint fields; provider leaves emit no activities,
logs, or metrics of their own.

**Design decisions.**

- `DefaultModelRequestExecutor` in `AgentKit.Providers` binds the selected model
  to endpoint and credential profile snapshots before the attempt, runs a
  `Microsoft.Extensions.Http.Resilience` pipeline configured per profile, maps
  failures to the taxonomy, and returns `ModelFallbackRequired` for the loop to
  reselect under `ModelSelectionPolicy`.
- Credential-profile references are audit evidence and never enter assistant
  message metadata.
- `IEmbeddingModelSelector`, `IEmbeddingRequestExecutor`, `IRerankerSelector`,
  `IRerankRequestExecutor`, and the `Rerank*` contracts are added to
  Abstractions; `AgentKit.Providers` provides defaults; Cohere and OpenRouter
  register rerankers.
- `EmbeddingSpaceIdentity` grows to the documented shape; storage contracts
  (workstream 14) key vectors on it.
- Each leaf instruments its HTTP send with the shared `provider.*` activity
  names, bounded metrics, and safe log events.

**Deliverables.** Contracts, executor, profile runtime selector, resilience
options, semantic selectors/executors, rerankers, leaf instrumentation,
`AgentDefinition.Components.ModelExecutor`, validator checks for one profile
runtime selector, one executor per definition, and at least one compatible
conversational model per definition.

**Tests.** Binding captured before attempt, fallback path, resilience with
recorded fixtures, failure taxonomy retention, reranker wire fixtures, embedding
selection by space identity, leaf activity/log/metric assertions with content
absent.

**Docs.** `architecture/model-and-embedding-providers.md`,
`providers/semantic-operations.md`, provider READMEs,
`agentkit-provider- adapters` skill, ledger Providers row.

---

### 8. Structured output completion

**Goal.** All documented output modes, end strategies, downgrade policy,
budget-backed repair, and hook integration.

**Gaps closed.** `NativeSchema` validated locally but never sent to a provider;
`SyntheticTool`, `Media`, `Union` rejected; `OutputEndStrategy` and
`AllowProviderModeDowngrade` inert; repair budget is attempt counting; no hook
context; `OutputProcessingRequest` lacks `AgentRunView` and
`BudgetExecutionCapability`; `ModelRequirements.RequiresStructuredOutput` is
never derived from the definition.

**Design decisions.**

- `LlmRequestContext` carries `OutputContract` so translators emit
  `response_format`/`json_schema` (OpenAI family), `responseSchema`/
  `responseMimeType` (Gemini/Vertex), and forced tool schema for `SyntheticTool`
  (Anthropic, Bedrock, Cohere, Mistral). Capability descriptors are corrected to
  match what each translator actually sends.
- Mode negotiation happens in the output processor preflight using the selected
  model's capabilities; downgrade follows `AllowProviderModeDowngrade` and is a
  configuration failure otherwise.
- `Union` selects among alternatives through the schema engine; `Media`
  validates media parts against the declared content types.
- Repair attempts reserve against a child `IBudgetScope` (workstream 11 adds the
  profile; a run-scoped reservation is used until then).

**Deliverables.** Processor, registry, translator changes across provider
leaves, `IOutputProcessor.ProcessAsync(request, HookDispatchContext?, …)`,
Simple `WithOutput<T>` mode options.

**Tests.** Per-provider wire fixtures for each mode, negotiation and downgrade
matrix, union selection, end-strategy behavior, repair budget exhaustion, hook
tightening.

**Docs.** `architecture/structured-output.md`, `agentkit-structured-output`
skill, ledger Output row.

---

### 9. Context assembly pipeline

**Goal.** Ordered, trusted, budgeted contributors produce a manifest-backed
model request; instructions resolve from sources; project instruction files and
skills contribute.

**Gaps closed.** `IContextContributor`, `ContextContributionRequest`,
`ContextContribution`, `ContextCandidate`,
`ContextManifest(+Entry, Disposition)`, `IInstructionResolver`,
`IHistoryPipeline`, `IToolSnapshotProvider`, `IContextBudgetAllocator`,
`ContextBudget`, `AgentContextOptions`, `ContextOverflowBehavior`, keyed
`ComponentKey<IContextAssembler>`, `ContextCompactionCapability`,
`ModelRequestContext` absent; `DefaultContextAssembler` takes no contributors;
`AgentDefinition.Instructions` is `AgentMessage` rather than
`InstructionSource`; no AGENTS.md/CLAUDE.md discovery; `Tools.Skill` has no
contributor.

**Design decisions.**

- `DefaultContextAssembler(ContextAssemblerServices, TimeProvider, ILogger)`
  becomes internal and keyed; it loads history through the session cursor,
  resolves instructions, runs contributors in authored order with trust and
  frequency rules, allocates budget, records the manifest, and dispatches the
  context hooks.
- `InstructionSource` replaces raw instruction messages on the definition; a
  literal source preserves today's behavior.
- `AgentKit.Context.Project` (new leaf) discovers workspace instruction files
  per the coding-harness resource rules through the file-system boundary; it is
  opt-in and trust-classified.
- `Tools.Skill` registers an `IContextContributor` for the skill inventory.

**Deliverables.** Contracts, assembler, allocator, instruction resolver, history
pipeline, `AddAgentContext(key, …)`, `AddContextContributor<T>`,
`ReplaceContextBudgetAllocator<T>`, project contributor leaf, skill contributor,
definition `Components.Context`, validator check.

**Tests.** Ordering/trust/frequency matrix, budget overflow behavior (mandatory
versus optional), manifest completeness, instruction precedence, project file
discovery through a fake file system, DI replacement.

**Docs.** `architecture/context.md`, coding-harness profile page,
`agentkit-context` skill, ledger Context row.

---

### 10. Context compaction completion

**Goal.** All triggers, summary generation as a resolvable component, activation
as a coordinator, keyed profiles, events, and budget reservation.

**Gaps closed.** `ProviderOverflow`, `ExplicitMaintenance`, and
`InstructionEpochChanged` never raised; `CompactionRetryContinuationCause` never
produced; `ICompactionSummaryGenerator(+Resolver)`,
`ICompactionActivationCoordinator`, `ICompactionStrategyResolver`, event
sink/dispatcher, `CompactionProfileKey`, profile options, keyed registration
absent; `ModelCompactionStrategy` resolves models directly; no budget
reservation.

**Design decisions.** Overflow failures from the executor (workstream 7) raise
`ProviderOverflow` and the loop continues with
`CompactionRetryContinuationCause` once per turn. Instruction epoch changes come
from the context manifest (workstream 9). Summary generation goes through the
request executor with its own budget reservation.

**Deliverables.** Contracts,
`AddAgentContextCompaction(ComponentKey<ICompactor>, …)`,
`AddCompactionStrategy<T>`, `AddCompactionProfile`, `AddCompactionEventSink<T>`,
`Replace*`, loop wiring, definition `Components.Compaction`, validator check
when selected.

**Tests.** Trigger matrix, retry cause once per turn, activation conflict,
summary generator replacement, event emission, budget exhaustion during
compaction.

**Docs.** `architecture/context-compaction.md`, `agentkit-context-compaction`
skill, ledger Compaction row.

---

### 11. Budgets completion

**Goal.** Budget profiles selected per definition, unknown-cost enforcement, and
reservations from every consumer.

**Gaps closed.** `BudgetProfileKey` unused (definition carries raw
`BudgetLimits`); `IBudgetProfileCatalog`/`IBudgetPolicyCatalog` stand-in in
`BudgetScopeRequest`; `BudgetUnknownCostBehavior` not enforced; output repair,
compaction, and context allocation do not reserve through the authority.

**Deliverables.** Profile and policy catalogs, `AddBudgetProfile`,
`AgentDefinition.Components.BudgetProfile` (raw limits remain as an inline
profile), unknown-cost enforcement in the ledger authority, consumer
reservations, validator check for one authority and a resolvable profile per
definition.

**Tests.** Profile resolution, unknown-cost modes, reservation from each
consumer, exact aggregate accounting across concurrent children.

**Docs.** `architecture/budgets.md`, `agentkit-budgets` skill, ledger Budgets
row.

---

### 12. Durable execution

**Goal.** A provider-neutral coordinator that records, fences, recovers, and
reconciles durable operations, with durable journals and first-party consumers.

**Gaps closed.** No `AgentKit.Durability` package;
`IDurableExecutionCoordinator`, backend, runtime selector, lease,
dispatch/reconciliation requests, event sinks, `UnknownEffectRecoveryMode`,
`DurableCheckpointMode`, and typed results absent; no `IRecoveryPolicy`
implementation; journal methods carry no grant or audit; `RecoveryEvidence`
lacks the recorded result and not-before instant; no `.Sqlite`/`.Json` journal;
no fenced decorator; zero consumers.

**Design decisions.**

- Resolve the documented specification gap: `RecoveryEvidence` gains
  `DurableOperationResult? RecordedResult` and `DateTimeOffset? NotBefore`; the
  journal gains `RecordWaitingAsync` with `ExternalOperationReference`.
- Journal ingress takes `AuthorizedDurableRequest<T>` (grant + enforcement
  intent) with audit, mirroring the session-store wrapper.
- Consumers: loop checkpoints at model request, tool call, message commit, and
  settlement; engine admission/promotion; approval waits; compaction activation.
  Storage fencing is never treated as proof that an external effect stopped.

**Deliverables.** Contracts, `AgentKit.Durability` runtime, `.InMemory` updates,
`.Sqlite`, `.Json`, fenced decorator, default recovery policy,
`AddAgentDurability`, `AddDurabilityProfile`, backend/journal/lease/policy
registrations, `AgentDefinition.OptionalCapabilities.DurabilityProfile`,
validator checks when selected, journal conformance suite.

**Tests.** Recovery decision matrix per `SideEffectCertainty`, fencing under
concurrent workers, lease loss mid-operation, torn journal recovery (Json),
grant denial before record, audit, consumer checkpoints, process-loss replay
through the engine.

**Docs.** `architecture/durable-execution.md`, concept acceptance scenarios,
`agentkit-durable-execution` skill, ledger Durability row.

---

### 13. Goals, joins, worker hosting

**Goal.** Goals and attempts are durable domain state; delegation flows through
target selection, policy, dispatch, and joins; detached child work is drained by
a host worker through public admission.

**Gaps closed.** 25 of 30 normative types absent (`AgentGoal`, `GoalAttempt`,
`GoalTransition`, canonical `DelegationRequest/Result`, `IGoalStore`,
`IGoalCoordinator`, `IDelegationTargetProvider/Catalog/Selector`,
`IDelegationPolicy(+Pipeline)`, `IDelegationDispatcher(+Selector)`,
`IDelegationCoordinator`, `IGoalBudgetManager`, `IGoalJoinStrategy(+Selector)`,
built-in join strategies, event sinks, `AgentKit.Goals.Hosting`, agent-to-agent
communication); `EngineDelegationChannel` runs children inline.

**Deliverables.** Contracts, `AgentKit.Goals` coordinator and pipeline,
session-backed goal store projection plus `.InMemory`/`.Sqlite`/`.Json`, join
strategies, `AgentKit.Goals.Hosting` worker leaf, `Tools.Task` migration,
`AddAgentGoals`, `AddGoalProfile`, `AddGoalStore<T>`,
`AddDelegationDispatcher<T>`, `AddGoalJoinStrategy<T>`, definition
`OptionalCapabilities.GoalProfile`, Simple `WithDelegation` over the pipeline.

**Tests.** Transition validity, idempotent transitions, attempt lifecycle, join
strategy matrix, budget reservation for children, host worker drain with
process-loss recovery, communication through admission, goal-store conformance
across adapters.

**Docs.** `architecture/goals-and-delegation.md`,
`use-cases/delegating-to- specialists.md`, `agentkit-goals-and-delegation`
skill, ledger Goals row.

---

### 14. Memory and retrieval

**Goal.** Durable memory, document storage, vector indexing, retrieval, and
policy as first-class packages with a retrieval context contributor.

**Gaps closed.** Everything except `MemoryProfileKey`: identities, records,
`IMemoryStore`, `IDocumentStore`, `IVectorIndex`, catalogs/selectors, profile
runtime lease/selector, `IMemoryPolicy(+Dispatcher)`, `IMemoryCoordinator`,
`IRetrievalSource(+Selector)`, `IQueryRewriter`, `IRetrievalPipeline`,
`IRetrievalBudgetPolicy`, event sinks, chunking, provenance, tombstones, purge,
generic `DataClassification`, `AgentKit.Memory` and its adapters.

**Design decisions.** Retrieval is a `IContextContributor` (workstream 9) under
a budget and trust classification; embeddings and reranking use the workstream 7
executors; vectors are keyed by the full `EmbeddingSpaceIdentity`; deletion
separates logical invisibility from physical purge with receipts.

**Deliverables.** Contracts, `AgentKit.Memory`, `.InMemory`, `.Sqlite`, `.Json`,
retrieval contributor, `AddAgentMemory`, `AddMemoryProfile`,
`AddMemoryStore<T>`, `AddDocumentStore<T>`, `AddVectorIndex<T>`,
`AddRetrievalSource<T>`, `AddMemoryPolicy<T>`, `AddQueryRewriter<T>`,
`AddMemoryEventSink<T>`, definition `OptionalCapabilities.MemoryProfile`, Simple
`WithMemory`.

**Tests.** Store/index/document conformance across adapters, space-identity
mismatch rejection, retrieval budget and provenance, tombstone visibility, purge
receipts, policy denial before write, contributor ordering.

**Docs.** `architecture/memory-and-retrieval.md`, `agentkit-memory-and-storage`
skill, new use case, ledger Memory row.

---

### 15. Artifacts completion

**Goal.** Keyed artifact profiles with selectable stores, events, external
ownership, reference commitment, retention, and reconciliation.

**Gaps closed.** `.Sqlite`, `.Json`, `.FileSystem` stores; `ArtifactBackendKey`,
`IArtifactStoreSelector`, profile snapshot/options,
`IArtifactIntegrityValidator`, `IArtifactRetentionPolicy`, event contracts,
`ExternalArtifactOwnership`, reference-commit intent, orphan reconciliation,
`ISecurityAuthoritySelector` use, observability; consumers for session export
and memory documents.

**Deliverables.** Contracts, coordinator rewrite, adapters,
`AddAgentArtifacts( key, profile, …)`, `AddArtifactProfile`,
`AddArtifactStore<T>(backendKey)`, `AddArtifactEventSink<T>`, definition
`OptionalCapabilities.ArtifactCoordinator`, `artifact.*` activity names.

**Tests.** Conformance across four adapters, finalize/abort race, fenced
collection, retention sweep, orphan reconciliation, integrity mismatch, audit.

**Docs.** `architecture/artifacts.md`, `agentkit-artifacts` skill, ledger
Artifacts row.

---

### 16. Identity ingress and revalidation

**Goal.** Trusted ingress resolves identity for every channel; downstream work
revalidates evidence; reusable conformance covers issuers, validation policies,
derivers, and the resolver.

**Deliverables.** `IExecutionIdentityResolver` invoked by the IO channel
adapters and the facade admission path; revalidation at admission and before
protected effects against `MaximumEvidenceLifetime` and revocation; conformance
suites in `AgentKit.Conformance`; `IConformanceFixture<TContract>` and
`ConformanceCapabilities` base.

**Tests.** Ingress normalization per channel, expired evidence rejected at
admission and at a protected boundary, delegation chain narrowing, suites run
against first-party implementations.

**Docs.** `architecture/identity.md`, `agentkit-identity-and-tenancy` skill,
ledger Identity row.

---

### 17. Observability completion

**Goal.** Content classification and redaction contracts, exporter package,
required-sink settlement integration, and instrumentation in every package.

**Deliverables.** `ObservationContent`, `ObservationContentKind`,
`ContentFingerprint`, `IObservationRedactor`, `ObservationPolicy`,
`RedactionResult`, capture/delivery/bounds policies;
`AgentKit.Observability.OpenTelemetry` with run-event and audit sinks;
instrumentation for artifacts, MCP, all `Tools.*`, Simple, and
`Budgets.Storage.Shared`; reusable signal assertions in `AgentKit.Test.Shared`.

**Tests.** Redaction failure omits content, disabled listeners leave behavior
unchanged, exporter failure does not mutate outcomes, required-sink flush on
shutdown, per-package signal coverage.

**Docs.** `architecture/observability.md`, `agentkit-observability` skill,
ledger Observability row.

---

### 18. Definition and composition validation sweep

**Goal.** `AgentDefinition` matches the documented record and
`AgentCompositionValidator` enforces the full checklist in `AGENTS.md`.

**Deliverables.** `AgentComponentSelection`, `AgentOptionalCapabilitySelection`,
`InstructionSource`, removal of interim properties, validator coverage for all
engine-wide and per-definition requirements, run-profile publication updates,
Simple plan builder over the final shape.

**Tests.** One failing test per validator requirement, DI correspondence,
service-graph acyclicity, keyed multiplicity, optional capability collaborator
validation.

**Docs.** `architecture/composition-and-configuration.md`,
`guides/composition .md`, `agentkit-architecture` skill, ledger Engine row.

---

### 19. Evaluation

**Goal.** Versioned behavioral evaluation as an application leaf over the public
facade.

**Deliverables.** `AgentKit.Evaluation` (`IEvaluationRunner`, `IEvaluator`,
result store, exporter, catalog, `EvaluationRunner`, `SchemaEvaluator`,
`ExactStateEvaluator`, `ToolEffectEvaluator`, `SafetyEvaluator`, optional
`ModelJudgeEvaluator`, options and registrations), `.InMemory`, `.Sqlite`,
`.Json` result stores, result-store conformance suite, one example.

**Tests.** Plan execution with concurrency and repetition, outcome taxonomy,
store conformance, exporter failure isolation.

**Docs.** `architecture/testing-and-evaluation.md`, `agentkit-evaluation` skill,
ledger Evaluation row.

---

### 20. Documentation reconciliation

**Goal.** No source remark, README, guide, or ledger row contradicts the shipped
surface.

**Deliverables.** Remove every remaining "reduced stand-in"/"not yet" remark or
make it accurate; correct stale ledger findings (Sessions SQLite/JSON present,
`CreateOrOverwrite` fix, `LlmRequestContext` remark); update `docs/index.md`,
`getting-started.md#current-status`, use cases, package catalog, provider
reference, and every skill touched; ensure `AGENTS.md` routing map names each
new package.

**Acceptance.** `rg "stand-in|not yet" src --type cs` returns only accurate,
intentional statements; every ledger row cites its verification evidence.

## Cross-cutting decisions

- **Breaking changes.** Workstreams 1, 4, 5, 8, 9, 18 change public contracts
  toward the documented shape. Interim members are marked `[Obsolete]` for one
  release only when a thin adapter is cheap and safe; otherwise they are removed
  with the change documented in the commit and ledger.
- **Store families.** Every new store (approval, decision, journal, goal,
  memory, document, vector, artifact, evaluation result) lands with `.InMemory`,
  `.Sqlite`, and `.Json` adapters and one conformance suite in the same
  workstream.
- **Definition growth.** Each workstream adds its key to `AgentDefinition` and
  its check to the validator; workstream 18 consolidates them into the
  documented `Components` and `OptionalCapabilities` records.
- **Simple sugar.** `AgentKit.Simple` gains one `With*` per new capability so
  every workstream is reachable from a ten-line program.
- **Verification.** Area tests per commit; `make format`, `make lint`,
  `make build`, `make test` before each push.
