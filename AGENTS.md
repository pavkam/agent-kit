# AgentKit Agent Instructions

## Mission

AgentKit is a .NET 10 framework for composing agentic applications from
replaceable parts. A built `AgentEngine` is the process-level composition that
hosts and runs multiple independently configured agents. The agent catalog,
loop, model and embedding providers, tools, tool providers, permissions, memory,
storage, queues, goals, and hosting are extension points joined through
dependency injection.

Correct contracts, explicit capabilities, safe tool execution, deterministic
behavior, and provider-neutral composition outrank shortcuts.

## Instruction precedence

The user's instructions take precedence over repository skills. Skills provide
domain guidance; they do not expand the requested scope or authorize external
side effects. When two repository rules conflict, follow the rule closest to the
code being changed and call out any unresolved conflict.

## Repository map

- `src/AgentKit.Abstractions/` is the provider-neutral contract package. It must
  not reference `AgentKit` or any concrete provider SDK.
- `src/AgentKit/` is the dependency-light facade. It contains `AgentEngine`,
  `AgentEngineBuilder`, hosted registration, composition validation, and
  lifecycle ownership. It references `AgentKit.Abstractions` but no concrete
  engine component.
- First-party implementations live in focused projects such as `AgentKit.Loop`,
  `AgentKit.Budgets`, `AgentKit.Context`, `AgentKit.Context.Compaction`,
  `AgentKit.Hooks`, `AgentKit.Identity`, `AgentKit.IO`, `AgentKit.Output`,
  `AgentKit.Session`, `AgentKit.Permissions`, `AgentKit.Providers`, and
  `AgentKit.Tools`.
- Tool features use `AgentKit.Tools.<ToolName>`. Provider integrations use
  `AgentKit.Providers.<ProviderName>`. Session stores use
  `AgentKit.Session.<ProviderName>`.
- Concrete providers, storage, transports, filesystem implementations, and
  hosting integrations are leaves; foundation and runtime packages never
  reference them.
- `AgentKit.Providers.OpenAICompatible` is shared protocol-family machinery.
  Applications normally select concrete packages such as
  `AgentKit.Providers.OpenAI`, `AgentKit.Providers.OpenRouter`, or
  `AgentKit.Providers.ZAI` instead of treating compatibility as provider
  identity.
- Tests mirror source packages under `tests/`. Shared conformance suites may
  live in a dedicated non-packable test project.
- Examples belong under `examples/` and compose packages through their public DI
  surface.
- Repository workflows live under `.agents/skills/`. Add or edit skills only
  there.
- Architecture documents use descriptive filenames without numeric ordering
  prefixes. Their relationships belong in links and indexes, not filenames.

## Architecture authority

`docs/architecture` and its linked normative concepts are the design source of
truth. Implementation, tests, and API snapshots do not override them. Resolve
conflicting owner/concept requirements together and update acceptance scenarios.
The architecture index defines document authority and change rules.

## Architectural invariants

### Composition

- Treat "plugin" as a composition property, not a universal god interface. Each
  extension point has its own narrow contract and lifecycle.
- Every replaceable behavior has an interface in an abstractions package. Offer
  an optional base class when it can safely provide reusable mechanics, but
  never make inheritance the only extension path.
- Keep contracts smaller than implementations. Split discovery, selection,
  execution, persistence, policy, and observation instead of building manager
  interfaces that own all of them.
- `AgentEngine` is a facade over one complete composition of abstractions that
  can host multiple immutable agent definitions and concurrent run scopes. It is
  never synonymous with one agent, a hidden default runtime, or a service
  locator.
- An `Agent` is an immutable engine-bound handle over one validated
  `AgentDefinition`. It owns no mutable session or run state and is safe to use
  concurrently.
- Dependencies point inward: abstractions → nothing concrete; behavioral runtime
  → abstractions; integrations → abstractions and, when needed, runtime.
  First-party packages may reference shared exporter-free
  AgentKit.Observability. Evaluation and goal-worker hosting are application
  leaves allowed to consume the public facade; no facade/runtime depends on
  them. Applications compose.
- New admissions revalidate pinned definitions and profiles. Recovery preserves
  an open run's identities; work after a settled run receives a new RunId.
  Caller-wait cancellation is distinct from durable abort. Final results
  separate semantic outcome from settlement/recovery status.
- Grant use is consumed once by the effecting boundary with its own enforcement
  intent. It is not atomic with arbitrary external effects. Security-control
  persistence uses explicit bounded host bootstrap capabilities and cannot
  recursively authorize its own grant/audit writes.
- Started budget reservations with unknown spend survive disposal or process
  loss until reconciliation. Indivisible batches reserve all dimensions
  atomically. Storage fencing alone never proves an external effect stopped.

### Dependency injection

- Use `Microsoft.Extensions.DependencyInjection`, options, logging,
  configuration, and resilience conventions rather than a parallel container.
- `AgentEngine.CreateBuilder()` returns a separate mutable `AgentEngineBuilder`;
  `Build()` returns an immutable `AgentEngine`.
- `AgentEngineBuilder.Services` is the feature composition surface. The same
  service registrations must work with ASP.NET Core, worker hosts, and custom
  service collections.
- Support standalone and host-managed ownership through the same registration
  path. A standalone engine owns the provider it builds; an externally hosted
  engine never disposes the host's provider.
- Registration extensions return `IServiceCollection`, do not build a service
  provider, and document duplicate-registration behavior.
- Register defaults with replaceability in mind. Use additive registrations for
  multi-provider concepts and stable keys or names when selection is required.
- Every behaviorally meaningful mechanism and policy is configurable at its
  proper boundary through DI, typed options, engine configuration, an immutable
  agent definition, or an explicit run override. First-party feature packages
  provide sensible documented defaults through replaceable registrations;
  credentials, endpoints, persistence targets, and authority are external facts
  and are never fabricated as defaults.
- Provider operations bind independently keyed, versioned endpoint/service-
  surface and credential/account profiles to one operation/model registration.
  Capture those bindings before an attempt; never pair providers or accounts
  through unkeyed registration order. Credential-profile references are
  classified execution/audit evidence and do not enter ordinary assistant
  message metadata.
- Validate options at the composition boundary. Do not defer missing endpoints,
  invalid models, or impossible limits until the middle of an agent run.
- Do not use service locators, ambient containers, mutable global registries, or
  static "current agent" state.
- Validate both graphs: project references must form a directed acyclic graph,
  and constructor/factory dependencies must form a directed acyclic service
  graph. Interfaces in AgentKit.Abstractions prevent package cycles; they do not
  excuse runtime constructor cycles.
- Composition validation requires singular engine-wide composition services: one
  agent-definition catalog with at least one runnable definition, one run-scope
  factory and validator, one session directory/store selector, one hook dispatch
  kernel/point-definition catalog/profile selector, one security authority
  selector/policy catalog, one approval broker, one model catalog, one
  provider-profile runtime selector, one budget authority, and a `TimeProvider`.
  For every runnable agent definition it must resolve exactly one selected loop,
  continuation policy, input coordinator, output publisher, context assembler,
  session coordinator/run coordinator/profile and store, hook profile, security
  authority/profile, model selector, model request executor, and at least one
  compatible conversational model, output processor, and run budget profile.
  Multiple keyed implementations may coexist; optional capabilities validate
  their own required collaborators when selected.

### Hooks

- Built-in hook interfaces and their dedicated `EventArgs`-derived classes live
  in `AgentKit.Abstractions`; third-party points live in their owning contract
  assembly. `AgentKit.Hooks` owns the typed dispatch kernel, ordering,
  validation, and captured catalogs without depending on feature packages.
- Define one narrow interface and event-argument type for each named lifecycle
  boundary. Point definitions are typed and additive; do not expose a generic
  event-name-and-object escape hatch.
- Hook event arguments expose only identities established at that lifecycle
  stage. Distinguish point, registration, dispatch, and individual invocation
  identities; never fabricate an agent, session, or run identity to satisfy a
  universal base shape.
- Registrations are additive. Mutating hooks run sequentially in deterministic
  order, and the dispatcher validates allowed mutations after every hook. Soft
  before/after constraints ignore absent targets; hard dependencies, singleton
  first/last anchors, contradictions, and cycles validate explicitly.
- Stable identity, causal data, committed state, and authority-bearing values
  are read-only. Writable properties expose only changes the hook point permits.
- Hooks may short-circuit only through a typed outcome defined by that boundary;
  there is no universal cancel flag.
- Hooks cannot grant, widen, forge, consume, or mint security authority. A hook
  performing a protected operation uses the same security authority as every
  other component.
- Failure policy is monotonic: each invocation uses the strictest point, host,
  profile, registration, and narrower-scope requirement. Only read-only or
  observational points may isolate failure, cancellation always propagates, and
  isolation never leaks partial mutation.

### Provider neutrality

- Model provider capabilities explicitly. Text, images, audio, structured
  output, tool calls, parallel calls, streaming, usage, reasoning metadata, and
  server-side tools are not universally available.
- Normalize portable semantics; preserve provider-specific information in typed
  extension data or provider options. Never discard data merely because another
  provider lacks it.
- OpenAI-compatible providers may share transports and base adapters, but
  compatibility is a tested capability set, not a brand label.
- A shared wire-family package may implement secret-safe token/header mechanics,
  but the branded leaf owns supported authentication schemes, audience, scopes,
  account binding, refresh, rotation, and endpoint/service-surface profiles.
- `AgentKit.Providers` owns the first-party catalog, selection, capability
  validation, and model request execution. Branded provider packages own
  endpoints, credentials, wire profiles, and concrete model registrations.
- Concrete provider packages register each supported operation independently.
  Conversation, embeddings, reranking, media, and provider-native tools do not
  become one oversized provider interface merely because one vendor offers all
  of them.
- `AgentKit.Providers.OpenAI` registers OpenAI conversational and embedding
  implementations. `AgentKit.Providers.OpenRouter` registers its conversational,
  embedding, and reranking implementations. `AgentKit.Providers.ZAI` registers
  only the operations supported by its verified API profile.
- Keep embedding generation separate from conversational generation. Embedding
  model identity, dimensions, modality, and vector-space compatibility are part
  of the storage contract.
- Candidate multiplicity and usage-reporting availability are capabilities. A
  single-candidate operation rejects extra choices, and missing usage remains
  unknown rather than becoming reported zero.
- Provider translation may be one-to-many or many-to-one only through a
  loss-aware mapping that preserves order, trust, and tool correlation. It never
  promotes runtime/synthetic content to system/developer authority or
  reconstructs an authoritative tool execution record from its message
  projection.
- Translate provider failures into a small stable taxonomy while retaining the
  original status, provider code, request identifier, and exception as
  diagnostic context.

### Messages, loops, and goals

- Messages are immutable, ordered, and content-part based. Preserve role,
  provider identifiers, tool-call correlation, and unknown extension content
  across round trips.
- `RuntimeMessage` and other synthetic operational evidence never gain
  system/developer instruction precedence during repair or provider mapping.
- `ToolCallResult` is the authoritative terminal execution record;
  `ToolResultPart` is a separately bounded, loss-aware history/model projection
  using the captured policy version. Preserve the requested alias and represent
  unresolved identity explicitly instead of fabricating a canonical tool ID.
- Domain identities are dedicated immutable value types, normally
  `readonly record struct` values such as `AgentId`, `SessionId`, `RunId`,
  `TurnId`, `MessageId`, and `ToolCallId`. Public contracts do not exchange raw
  strings, GUIDs, or integers for domain identity. Deterministic creation uses
  an injected identifier generator.
- Streaming is a typed event sequence, not a stream of strings. Partial text,
  tool arguments, reasoning metadata, usage, completion, and errors have
  distinct states.
- Agent loops expose explicit stop reasons, limits, cancellation, and progress.
  They do not own provider selection, tool authorization, memory, or queueing
  through hidden dependencies.
- Goals and queued work are domain state, not prompt strings. Persist identity,
  status transitions, attempts, and causality explicitly.
- Define ordering, concurrency, backpressure, retries, and idempotency wherever
  more than one message or tool call can be in flight.

### Input and output

- `AgentKit.IO` owns the first-party admission coordinator, queued-input
  promotion, live event fan-out, and final-result publication. It does not own
  durable session truth or the agent state machine.
- Input queues preserve admission identity, delivery class, capacity,
  backpressure, ordering, and idempotency. Queue state is persisted through
  session contracts when durability is required.
- Channel adapters are leaves. HTTP, console, UI, or messaging integrations
  translate their protocol into AgentKit input and output contracts without
  bypassing admission, authorization, or settlement.
- `AgentKit.Output` owns output-definition resolution, extraction, validation,
  repair decisions, and deserialization. It returns typed decisions to the loop;
  it never invokes providers or publishes results itself.

### Identity, budgets, and artifacts

- Execution identity is authenticated at a trusted ingress and propagated as an
  immutable value. Identity never grants authority, and security never
  authenticates credentials.
- `AgentKit.Budgets` owns hierarchical atomic reservations. Consumers reserve
  through `IBudgetScope`; the budget authority never depends back on loops,
  providers, tools, goals, context, or evaluation.
- `AgentKit.Artifacts` owns durable binary/generated content and references. It
  never appends its own session or tool records; callers coordinate reference
  commitment so artifact and session dependencies remain one-way.

### Observability and diagnostics

- Every externally reachable operation and every materially asynchronous
  internal stage MUST be observable through structured
  `Microsoft.Extensions.Logging` logs and `System.Diagnostics` instrumentation.
  Operations with meaningful duration or causality MUST create an `Activity`;
  bounded aggregate behavior MUST use `Meter` instruments. Point-in-time
  diagnostics that need neither duration nor aggregation remain structured log
  events.
- All first-party packages use the shared `AgentKit` activity source, meter,
  activity names, metric names, and tag names from `AgentKit.Observability`. Do
  not create package-local naming dialects or depend on an exporter SDK.
  Exporters and hosts subscribe through the standard Microsoft diagnostics and
  logging surfaces.
- Use source-generated `LoggerMessage` methods with stable, package-owned event
  IDs and templates. Do not use interpolated log strings. Log categories name
  the emitting type, and event severity reflects the semantic outcome rather
  than whether an exception happened to be thrown.
- Start activities before observable work and set every terminal activity to a
  truthful success or error status. Preserve parentage through the async call
  context, record normalized outcome and error attributes, and attach applicable
  typed domain identities to traces and logs. Never persist `Activity.Current`
  or treat trace/span identity as semantic state.
- Logs and spans MAY carry high-cardinality correlation identities; metrics MUST
  use only bounded dimensions. Prompts, model output, reasoning, tool
  arguments/results, retrieved content, raw paths, credentials, authorization
  headers, and secrets are content and MUST NOT be logged or tagged by default.
  Content capture remains explicit, classified, bounded, policy-controlled, and
  redacted before export; redaction failure omits content.
- Instrumentation is observational only. Disabled listeners MUST leave behavior
  unchanged, logging/exporter failures MUST NOT mutate semantic outcomes, and
  instrumentation MUST NOT become a control-flow, authorization, persistence, or
  synchronization dependency.

### Tools and security

- Separate tool description, discovery, resolution, authorization, execution,
  and result recording.
- A model requests a tool; it never directly executes one. Every call passes
  schema validation and the configured security authority before invocation.
- Unknown tools, invalid arguments, missing policy context, and unsupported
  capabilities fail closed with typed results.
- Tool and MCP metadata are untrusted input. Descriptions and annotations never
  grant permissions.
- Preserve call IDs through provider, loop, permission, execution, and result
  messages. Redact secrets from logs and diagnostics.
- Every bounded, identified call reaches one terminal record and one correlated
  projection, including pre-invocation rejection. Projection/publication retry
  never repeats the tool effect.
- Any component may issue a typed `SecurityRequest`. Allow decisions produce a
  bounded `SecurityGrant` that the effecting component validates immediately
  before acting; mutation after authorization requires reevaluation.
- File-system, network, process, provider-egress, memory, session, MCP, and
  delegation boundaries fail closed when authority, enforcement, or required
  audit is unavailable. Sandboxing limits consequences but never grants access.
- Low-level file-system, network, and process implementations enforce the grant
  again so a higher-level allow cannot authorize a different concrete effect.
- File writes require an explicit create-only, replace-existing,
  create-or-replace, or append disposition with atomic target-state semantics.
  Parent-directory creation is a separate declared and authorized effect;
  actual-stream bounds and payload/final fingerprints are enforced at the host
  boundary.

### Memory and storage

- Distinguish conversation history, working context, durable memory, document
  storage, vector indexing, and retrieval. Do not hide them behind one
  all-purpose memory interface.
- Storage contracts state ownership, consistency, concurrency, pagination,
  deletion, and failure semantics.
- Retrieval is a policy-controlled context source. Treat retrieved content as
  untrusted data, enforce context budgets, and record provenance.

## .NET and public API rules

- Target .NET 10 and C# 14. New code must use the repository's modern C#
  conventions in
  [`.agents/skills/references/modern-csharp.md`](.agents/skills/references/modern-csharp.md);
  do not imitate legacy syntax merely because nearby code predates .NET 10.
- The base namespace is `AgentKit`; package and child namespaces use
  `AgentKit.*`.
- Use file-scoped namespaces and place `using` directives inside the namespace,
  as enforced by `.editorconfig`.
- Put every named type in its own file with the same name. Model immutable data
  and discriminated outcomes with records. Use `readonly record struct` or
  `readonly struct` for small value-semantic types that benefit from value-type
  representation; use explicit classes for services with lifecycle or identity.
- Use primary constructors and positional records when they keep immutable
  dependency or data shapes clear. Public values that must reject invalid input
  use explicit validating constructors or factories; a positional declaration is
  not an excuse to skip an invariant. This matches the Sharp Vision baseline
  while keeping domain validation explicit.
- Public asynchronous APIs accept `CancellationToken`. Prefer `ValueTask` or
  `ValueTask<T>` when synchronous completion is expected or the API is a hot,
  allocation-sensitive abstraction; keep `Task` for inherently asynchronous work
  or when callers need repeated awaits/combinators. Never block on async work,
  and use `IAsyncEnumerable<T>` only when streaming is real.
- Enable nullable analysis and express flow contracts with
  `System.Diagnostics.CodeAnalysis` attributes. Add `JetBrains.Annotations`
  attributes when they communicate useful Rider/ReSharper semantics that the
  compiler attributes cannot express; do not add decorative or duplicate
  annotations.
- Declare new instance and type extensions with C# 14 `extension` blocks. Use
  legacy `this`-parameter extension methods only for a verified compatibility or
  tooling constraint, and document that constraint.
- Use `TimeProvider` and injectable randomness/identity sources for behavior
  that must be deterministic in tests.
- Do not call ambient clocks or use wall-clock delays in framework behavior.
  Register `TimeProvider.System` only as the replaceable default.
- Every source project contains `GlobalUsings.cs`, `AssemblyInfo.cs`, and a
  package-scoped `ServiceExtensions.cs`. The abstraction package must not add a
  meaningless no-op registration merely to satisfy the file convention.
- `AssemblyInfo.cs` grants `InternalsVisibleTo` only to deliberate test or
  generated-proxy assemblies. Production packages do not use friend access to
  bypass public contracts.
- Provide substantive XML documentation for every source-authored `public`,
  `internal`, `protected`, `protected internal`, and `private protected` type
  and member. Explain the behavioral contract and, where applicable,
  type-parameter and parameter constraints, return or value semantics, ownership
  and lifetime, mutation, threading, cancellation, and thrown exceptions. A
  summary-only one-liner or text that merely restates the identifier is not
  sufficient. Include `<param>` and `<typeparam>` entries for every parameter,
  plus `<returns>`, `<value>`, `<exception>`, and `<remarks>` wherever their
  semantics apply. Use `<inheritdoc/>` only when the inherited documentation
  fully describes the implemented contract; supplement it whenever behavior or
  exceptions differ.
- Every constructor and method must enforce all documented constraints on its
  caller-supplied arguments before assignment, state mutation, I/O, or any other
  observable effect. At `public`, `internal`, `protected`, `protected internal`,
  and `private protected` boundaries, use the applicable BCL
  `ArgumentException.ThrowIf*`, `ArgumentNullException.ThrowIf*`, or
  `ArgumentOutOfRangeException.ThrowIf*` guard instead of a handwritten
  condition-and-throw block.
- When the BCL has no `ThrowIf*` member for a required constraint, add one
  canonical reusable static extension member with C# 14's unnamed-receiver
  syntax, `extension(TargetExceptionType)`. Put it in a top-level, nongeneric
  `<TargetExceptionType>Extensions` class named for the exception type, such as
  `ArgumentOutOfRangeExceptionExtensions`, and invoke it through the exception
  type, for example `ArgumentOutOfRangeException.ThrowIfFoo(value)`. The guard
  must throw the correct `ArgumentException` subtype, preserve or infer
  `ParamName` with `CallerArgumentExpression` where appropriate, and have
  focused tests. Document the extension container, extension block, guard, and
  exact constraint. Reuse the guard; do not create competing helpers, copy its
  condition across call sites, or use a legacy `this`-parameter extension
  method.
- In private methods, use side-effect-free `Debug.Assert(...)` checks at entry
  for caller-established argument preconditions and at the point where logical
  invariants must hold. Assertions must state the assumption being checked and
  never replace runtime validation at an externally reachable boundary. Code
  must remain safe and correct in Release builds when assertions are omitted.
- Avoid speculative generality. Add an abstraction for a demonstrated extension
  axis and at least two plausible implementations, not merely to wrap a single
  method.
- Public contract changes require compatibility review. Prefer additive
  evolution; document intentional breaking changes.

## Tests

- Use xUnit v3, Shouldly, and Arrange/Act/Assert.
- Mirror each source project with a .NET 10 executable, non-packable test
  project under `tests/`, following the Sharp Vision test-project setup.
- Name tests `MethodName_WhenThis_ThatIsExpected`.
- Write reusable conformance suites for every swappable contract, then run the
  same suite against the default implementation and each adapter.
- Unit tests do not call live model, embedding, MCP, or storage services. Use
  deterministic fakes, loopback HTTP handlers, recorded protocol fixtures, and
  controllable clocks.
- Keep opt-in integration tests separate and skip them clearly when credentials
  or infrastructure are unavailable.
- Test streaming at arbitrary fragmentation boundaries, cancellation at every
  await boundary, provider error mapping, tool-call correlation, DI replacement,
  queue ordering, hook order and mutation validation, grant scope and
  consumption, and denial before protected effects.
- Test each new operation's structured log event IDs and safe fields, activity
  name, parentage, correlation tags, terminal status, and bounded metrics.
  Verify cancellation and failures, verify that disabled listeners do not alter
  behavior, and assert that protected content is absent from every signal.
- Add focused tests for every new or changed argument constraint and every
  documented `<exception>` condition. Assert the exact exception type and
  `ParamName`, exercise boundary values, and prove that validation occurs before
  observable effects. Exercise protected members through a minimal derived test
  type. For every custom `ThrowIf*` extension, test the intended type-qualified
  invocation, valid and boundary no-throw cases, every invalid condition,
  inferred `ParamName`, and any stable exception properties promised by its
  contract; also cover at least one production call site.
- Maintain repository static-analysis or architecture tests that require XML
  documentation on `public`, `internal`, and every protected accessibility. Do
  not ban primary constructors or positional records wholesale; require an
  explicit validating constructor or factory only when the type has caller-input
  invariants. These checks enforce coverage; review still enforces that the
  documentation is substantive and accurate.
- Test private-method preconditions and invariants through observable behavior.
  Do not invoke or reflect into private members merely to trigger
  `Debug.Assert`, and do not make correctness depend on Debug-only assertion
  behavior.
- Test observable runtime contracts rather than private calls or incidental
  implementation shape. Policy tests may inspect syntax and documentation when
  the rule itself is explicitly structural.
- Keep common fakes and fixtures in `AgentKit.Test.Shared`, reusable behavioral
  suites in `AgentKit.Conformance`, and public API snapshots in
  `AgentKit.Compatibility.Tests`.

## Skill routing

- Use [agentkit-architecture](.agents/skills/agentkit-architecture/SKILL.md) for
  package boundaries, `AgentEngine` composition, public contracts, DI,
  configuration, cross-package designs, and coding-harness profile composition.
- Use
  [agentkit-identity-and-tenancy](.agents/skills/agentkit-identity-and-tenancy/SKILL.md)
  for trusted-ingress identity normalization, propagation, delegation chains,
  and tenant isolation; authorization remains a permissions concern.
- Use [agentkit-agent-loop](.agents/skills/agentkit-agent-loop/SKILL.md) for
  loop state transitions, continuation, cancellation, and settlement.
- Use
  [agentkit-messages-and-history](.agents/skills/agentkit-messages-and-history/SKILL.md)
  for immutable messages, content parts, correlation, history validation, and
  provider-ready repair views, including runtime-role non-elevation and
  authoritative-result projection.
- Use [agentkit-input-output](.agents/skills/agentkit-input-output/SKILL.md) for
  input admission, steering and follow-up queues, live event fan-out, final
  publication, channel adapters, PTY fan-out, control-plane reconnect, and TUI,
  IDE, batch, RPC, or ACP-style frontend projections with server-realm and
  incarnation fencing.
- Use
  [agentkit-structured-output](.agents/skills/agentkit-structured-output/SKILL.md)
  for output contracts, candidate extraction, validation, repair decisions, and
  typed conversion.
- Use [agentkit-context](.agents/skills/agentkit-context/SKILL.md) for context
  contributors, instruction sources, coding-project resource discovery, trust,
  ordering, and request assembly.
- Use
  [agentkit-context-compaction](.agents/skills/agentkit-context-compaction/SKILL.md)
  for semantic cuts, summaries, compaction validation, activation, and
  concurrency.
- Use [agentkit-sessions](.agents/skills/agentkit-sessions/SKILL.md) for
  canonical session records, stores, optimistic concurrency, snapshots,
  branching, per-lane operation ownership, serialized cross-lane mutation,
  export/import, and the session side of workspace reversion.
- Use
  [agentkit-durable-execution](.agents/skills/agentkit-durable-execution/SKILL.md)
  for checkpoints, recovery, replay, leases, fencing, reconciliation, and
  durable backend adapters.
- Use
  [agentkit-memory-and-storage](.agents/skills/agentkit-memory-and-storage/SKILL.md)
  for durable memory, source documents, vectors, retrieval, reranking, privacy,
  and deletion—not sessions or working context.
- Use [agentkit-artifacts](.agents/skills/agentkit-artifacts/SKILL.md) for
  durable content references, integrity, backend selection, retention, and
  orphan reconciliation, including tool-output spill, workspace snapshots, and
  session export/share payloads.
- Use
  [agentkit-goals-and-delegation](.agents/skills/agentkit-goals-and-delegation/SKILL.md)
  for goal state, attempts, scoped child work, joins, communication, and
  handoffs, including coding-harness task/subagent tools.
- Use [agentkit-budgets](.agents/skills/agentkit-budgets/SKILL.md) for
  hierarchical limits, atomic reservations, usage accounting, corrections, and
  typed exhaustion.
- Use [agentkit-hooks](.agents/skills/agentkit-hooks/SKILL.md) for typed
  lifecycle hooks, ordering, mutation validation, short-circuiting, reentrancy,
  stage identity, failure-policy precedence, and rollback-safe isolation.
- Use [agentkit-observability](.agents/skills/agentkit-observability/SKILL.md)
  for neutral events, traces, metrics, logs, audit sinks, correlation,
  redaction, and exporter boundaries.
- Use
  [agentkit-provider-adapters](.agents/skills/agentkit-provider-adapters/SKILL.md)
  for model catalogs, selection, capabilities, conversational operations,
  embeddings, reranking, endpoint/account binding, response multiplicity, usage
  evidence, wire translation, provider packages, and coding-harness
  interoperability profiles.
- Use [agentkit-tools](.agents/skills/agentkit-tools/SKILL.md) for tool
  identity, catalogs, schemas, resolution, invocation, scheduling, retries, and
  authoritative results, bounded message projections, and coding-host
  read/search/edit/patch/LSP built-ins.
- Use
  [agentkit-tools-and-permissions](.agents/skills/agentkit-tools-and-permissions/SKILL.md)
  for system-wide security policy, approvals, bounded grants, enforcement,
  audit, secondary effects such as parent creation, or an end-to-end protected
  tool-call flow.
- Use [agentkit-mcp](.agents/skills/agentkit-mcp/SKILL.md) for MCP clients,
  servers, shared reflection contracts, object-in/object-out tool classes,
  protocol-era/version distinctions, transports, capability negotiation,
  primitives, adapters, and protocol lifecycle, including coding-host namespace,
  instruction, root, catalog-generation, and endpoint-bound OAuth/callback
  rules.
- Use [agentkit-host-access](.agents/skills/agentkit-host-access/SKILL.md) for
  replaceable file-system, network, or process boundaries and their low-level
  grant enforcement, including explicit write disposition, bounded read/text
  semantics, workspaces, mutations, terminals, language services, and filesystem
  snapshots; load only affected boundary guidance. Each host surface has its own
  normative specification in `docs/concepts/`: file-system access and bounds,
  network access and egress, and process execution and sandboxing.
- Use [agentkit-diagnostics](.agents/skills/agentkit-diagnostics/SKILL.md) to
  isolate runtime, composition, protocol, persistence, security, cancellation,
  or concurrency failures before changing behavior.
- Use
  [agentkit-conformance-testing](.agents/skills/agentkit-conformance-testing/SKILL.md)
  for reusable contract suites and adapter verification.
- Use [agentkit-evaluation](.agents/skills/agentkit-evaluation/SKILL.md) for
  versioned agent-behavior datasets, evaluators, comparisons, and reports; use
  conformance testing for component contract correctness.

Skills are compact task routers, not a second architecture specification. Each
skill links its owning `docs/architecture/` page and the normative
`docs/concepts/` specifications needed for that work, loading conditional
references only when the task requires them. Adding or materially changing an
architecture or concept document requires updating the owning skill and this
routing map in the same change.

Read only the skills relevant to the task. For volatile provider or protocol
behavior, verify current primary documentation before designing or changing the
adapter.

## Workflow and verification

1. Read the relevant contract, skill, implementation, and nearest tests.
2. State the extension point and dependency direction affected.
3. Add or update a focused failing test for behavior changes.
4. Implement the smallest complete change through public abstractions.
5. Update XML documentation, examples, and repository guidance that changed.
6. Run focused checks, then the repository gates warranted by the change.

Architecture changes are incomplete until `docs/architecture/`, the normative
concept specifications, this file, and every affected repository skill agree on
package ownership and dependency direction.

Before declaring repository-wide work complete, run:

```bash
make format
make lint
make build
make test
```

Preserve unrelated user work. Do not commit, push, publish packages, rotate
credentials, or mutate external services unless the task explicitly requests it.
