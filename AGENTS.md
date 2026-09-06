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
  `AgentKit.Providers.ZAi` instead of treating compatibility as provider
  identity.
- Tests mirror source packages under `tests/`. Shared conformance suites may
  live in a dedicated non-packable test project.
- Examples belong under `examples/` and compose packages through their public DI
  surface.
- Repository workflows live under `.agents/skills/`. Add or edit skills only
  there.
- Architecture documents use descriptive filenames without numeric ordering
  prefixes. Their relationships belong in links and indexes, not filenames.

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
- Dependencies point inward: abstractions → nothing concrete; runtime →
  abstractions; integrations → abstractions and, only when necessary, runtime.
  Applications are the composition roots.

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
  factory and validator, one session directory/store selector, one hook
  dispatcher/profile selector, one security authority selector/policy catalog,
  one approval broker, one model catalog, one budget authority, and a
  `TimeProvider`. For every runnable agent definition it must resolve exactly
  one selected loop, continuation policy, input coordinator, output publisher,
  context assembler, session coordinator/run coordinator/profile and store, hook
  profile, security authority/profile, model selector, model request executor,
  and at least one compatible conversational model, output processor, and run
  budget profile. Multiple keyed implementations may coexist; optional
  capabilities validate their own required collaborators when selected.

### Hooks

- Hook interfaces and their dedicated `EventArgs`-derived classes live in
  `AgentKit.Abstractions`; `AgentKit.Hooks` owns dispatch, ordering, validation,
  and per-run catalogs.
- Define one narrow interface and event-argument type for each named lifecycle
  boundary. Do not expose a generic event-name-and-object escape hatch.
- Registrations are additive. Mutating hooks run sequentially in deterministic
  order, and the dispatcher validates allowed mutations after every hook.
- Stable identity, causal data, committed state, and authority-bearing values
  are read-only. Writable properties expose only changes the hook point permits.
- Hooks may short-circuit only through a typed outcome defined by that boundary;
  there is no universal cancel flag.
- Hooks cannot grant, widen, forge, consume, or mint security authority. A hook
  performing a protected operation uses the same security authority as every
  other component.

### Provider neutrality

- Model provider capabilities explicitly. Text, images, audio, structured
  output, tool calls, parallel calls, streaming, usage, reasoning metadata, and
  server-side tools are not universally available.
- Normalize portable semantics; preserve provider-specific information in typed
  extension data or provider options. Never discard data merely because another
  provider lacks it.
- OpenAI-compatible providers may share transports and base adapters, but
  compatibility is a tested capability set, not a brand label.
- `AgentKit.Providers` owns the first-party catalog, selection, capability
  validation, and model request execution. Branded provider packages own
  endpoints, credentials, wire profiles, and concrete model registrations.
- Concrete provider packages register each supported operation independently.
  Conversation, embeddings, reranking, media, and provider-native tools do not
  become one oversized provider interface merely because one vendor offers all
  of them.
- `AgentKit.Providers.OpenAI` registers OpenAI conversational and embedding
  implementations. `AgentKit.Providers.OpenRouter` registers its conversational,
  embedding, and reranking implementations. `AgentKit.Providers.ZAi` registers
  only the operations supported by its verified API profile.
- Keep embedding generation separate from conversational generation. Embedding
  model identity, dimensions, modality, and vector-space compatibility are part
  of the storage contract.
- Translate provider failures into a small stable taxonomy while retaining the
  original status, provider code, request identifier, and exception as
  diagnostic context.

### Messages, loops, and goals

- Messages are immutable, ordered, and content-part based. Preserve role,
  provider identifiers, tool-call correlation, and unknown extension content
  across round trips.
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
- Any component may issue a typed `SecurityRequest`. Allow decisions produce a
  bounded `SecurityGrant` that the effecting component validates immediately
  before acting; mutation after authorization requires reevaluation.
- File-system, network, process, provider-egress, memory, session, MCP, and
  delegation boundaries fail closed when authority, enforcement, or required
  audit is unavailable. Sandboxing limits consequences but never grants access.
- Low-level file-system, network, and process implementations enforce the grant
  again so a higher-level allow cannot authorize a different concrete effect.

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
  configuration, and cross-package designs.
- Use
  [agentkit-identity-and-tenancy](.agents/skills/agentkit-identity-and-tenancy/SKILL.md)
  for trusted-ingress identity normalization, propagation, delegation chains,
  and tenant isolation; authorization remains a permissions concern.
- Use [agentkit-agent-loop](.agents/skills/agentkit-agent-loop/SKILL.md) for
  loop state transitions, continuation, cancellation, and settlement.
- Use
  [agentkit-messages-and-history](.agents/skills/agentkit-messages-and-history/SKILL.md)
  for immutable messages, content parts, correlation, history validation, and
  provider-ready repair views.
- Use [agentkit-input-output](.agents/skills/agentkit-input-output/SKILL.md) for
  input admission, steering and follow-up queues, live event fan-out, final
  publication, and channel adapters.
- Use
  [agentkit-structured-output](.agents/skills/agentkit-structured-output/SKILL.md)
  for output contracts, candidate extraction, validation, repair decisions, and
  typed conversion.
- Use [agentkit-context](.agents/skills/agentkit-context/SKILL.md) for context
  contributors, instruction sources, trust, ordering, and request assembly.
- Use
  [agentkit-context-compaction](.agents/skills/agentkit-context-compaction/SKILL.md)
  for semantic cuts, summaries, compaction validation, activation, and
  concurrency.
- Use [agentkit-sessions](.agents/skills/agentkit-sessions/SKILL.md) for
  canonical session records, stores, optimistic concurrency, snapshots,
  branching, and active-run ownership.
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
  orphan reconciliation.
- Use
  [agentkit-goals-and-delegation](.agents/skills/agentkit-goals-and-delegation/SKILL.md)
  for goal state, attempts, scoped child work, joins, communication, and
  handoffs.
- Use [agentkit-budgets](.agents/skills/agentkit-budgets/SKILL.md) for
  hierarchical limits, atomic reservations, usage accounting, corrections, and
  typed exhaustion.
- Use [agentkit-hooks](.agents/skills/agentkit-hooks/SKILL.md) for typed
  lifecycle hooks, ordering, mutation validation, short-circuiting, reentrancy,
  and isolation.
- Use [agentkit-observability](.agents/skills/agentkit-observability/SKILL.md)
  for neutral events, traces, metrics, logs, audit sinks, correlation,
  redaction, and exporter boundaries.
- Use
  [agentkit-provider-adapters](.agents/skills/agentkit-provider-adapters/SKILL.md)
  for model catalogs, selection, capabilities, conversational operations,
  embeddings, reranking, wire translation, and provider packages.
- Use [agentkit-tools](.agents/skills/agentkit-tools/SKILL.md) for tool
  identity, catalogs, schemas, resolution, invocation, scheduling, retries, and
  results.
- Use
  [agentkit-tools-and-permissions](.agents/skills/agentkit-tools-and-permissions/SKILL.md)
  for system-wide security policy, approvals, bounded grants, enforcement,
  audit, or an end-to-end protected tool-call flow.
- Use [agentkit-mcp](.agents/skills/agentkit-mcp/SKILL.md) for MCP clients,
  servers, transports, capability negotiation, primitives, adapters, and
  protocol lifecycle.
- Use [agentkit-host-access](.agents/skills/agentkit-host-access/SKILL.md) for
  replaceable file-system, network, or process boundaries and their low-level
  grant enforcement; load only the affected boundary guidance.
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
