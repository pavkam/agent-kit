# AgentKit Agent Instructions

## Mission

AgentKit is a .NET 10 framework for composing agentic applications from
replaceable parts. The agent loop, model and embedding providers, tools, tool
providers, permissions, memory, storage, queues, goals, and hosting are
extension points joined through dependency injection.

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
  `AgentKit.Context`, `AgentKit.IO`, `AgentKit.Session`, `AgentKit.Permissions`,
  `AgentKit.Providers`, and `AgentKit.Tools`.
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
- `AgentEngine` is a facade over one composition of abstractions, never a hidden
  default runtime or service locator.
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
- Validate options at the composition boundary. Do not defer missing endpoints,
  invalid models, or impossible limits until the middle of an agent run.
- Do not use service locators, ambient containers, mutable global registries, or
  static "current agent" state.
- Composition validation requires one loop, one input coordinator, one output
  publisher, one session coordinator and store, one context assembler, one
  permission policy, one model catalog, one model selector, one model request
  executor, at least one conversational model, and a `TimeProvider`. Optional
  capabilities must validate their own required collaborators when registered.

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

### Tools and permissions

- Separate tool description, discovery, resolution, authorization, execution,
  and result recording.
- A model requests a tool; it never directly executes one. Every call passes
  schema validation and the configured permission policy before invocation.
- Unknown tools, invalid arguments, missing policy context, and unsupported
  capabilities fail closed with typed results.
- Tool and MCP metadata are untrusted input. Descriptions and annotations never
  grant permissions.
- Preserve call IDs through provider, loop, permission, execution, and result
  messages. Redact secrets from logs and diagnostics.

### Memory and storage

- Distinguish conversation history, working context, durable memory, document
  storage, vector indexing, and retrieval. Do not hide them behind one
  all-purpose memory interface.
- Storage contracts state ownership, consistency, concurrency, pagination,
  deletion, and failure semantics.
- Retrieval is a policy-controlled context source. Treat retrieved content as
  untrusted data, enforce context budgets, and record provenance.

## .NET and public API rules

- Target .NET 10 and C# 14. Use current platform features when they simplify the
  contract or implementation without weakening portability.
- The base namespace is `AgentKit`; package and child namespaces use
  `AgentKit.*`.
- Use file-scoped namespaces and place `using` directives inside the namespace,
  as enforced by `.editorconfig`.
- Put every named type in its own file with the same name. Use immutable records
  or readonly structs for values and explicit classes for services with
  lifecycle or identity.
- Public asynchronous APIs accept `CancellationToken`, use `IAsyncEnumerable<T>`
  only when streaming is real, and never block on async work.
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
- Validate public arguments before observable state changes. Document every
  public and internal type/member with useful XML documentation, including
  ownership, threading, cancellation, and exceptions.
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
  queue ordering, and permission denial.
- Test observable contracts rather than private calls or implementation shape.
- Keep common fakes and fixtures in `AgentKit.Test.Shared`, reusable behavioral
  suites in `AgentKit.Conformance`, and public API snapshots in
  `AgentKit.Compatibility.Tests`.

## Skill routing

- Use [agentkit-architecture](.agents/skills/agentkit-architecture/SKILL.md) for
  package boundaries, interfaces, base classes, and DI composition.
- Use
  [agentkit-provider-adapters](.agents/skills/agentkit-provider-adapters/SKILL.md)
  for conversational, embedding, reranking, and other provider operations.
- Use [agentkit-input-output](.agents/skills/agentkit-input-output/SKILL.md) for
  input admission, work queues, live streams, structured output, and channel
  adapters.
- Use [agentkit-mcp](.agents/skills/agentkit-mcp/SKILL.md) for MCP clients,
  transports, primitives, and protocol lifecycle.
- Use
  [agentkit-tools-and-permissions](.agents/skills/agentkit-tools-and-permissions/SKILL.md)
  for tool contracts, execution, authorization, and audit.
- Use [agentkit-agent-loop](.agents/skills/agentkit-agent-loop/SKILL.md) for
  messages, streaming, queues, goals, and orchestration loops.
- Use
  [agentkit-memory-and-storage](.agents/skills/agentkit-memory-and-storage/SKILL.md)
  for memory, embeddings, retrieval, and persistence.
- Use [agentkit-diagnostics](.agents/skills/agentkit-diagnostics/SKILL.md) to
  investigate runtime and integration failures.
- Use
  [agentkit-conformance-testing](.agents/skills/agentkit-conformance-testing/SKILL.md)
  for reusable contract suites and adapter verification.
- Use [agentkit-evaluation](.agents/skills/agentkit-evaluation/SKILL.md) for
  versioned agent-behavior datasets, evaluators, comparisons, and reports; use
  conformance testing for component contract correctness.

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
