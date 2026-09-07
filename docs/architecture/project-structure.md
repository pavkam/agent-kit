# Project structure

**Role:** Turn the component architecture into concrete projects and dependency
rules.

AgentKit uses one project for each independently selectable implementation or
external dependency boundary. A component may span several projects, and a
domain value does not earn a NuGet package merely because it has a name. The
result is granular where applications need choice and compact where splitting
would only create ceremony.

One built `AgentEngine` is the total process-level composition and may host many
immutable `AgentDefinition` values and concurrent run scopes. Packages register
engine-wide capabilities and catalogs; an agent definition selects from those
catalogs, and each invocation creates isolated mutable run state. No package
registers a singleton "current agent."

## Foundation projects

The
[architecture dependency rules](../concepts/architecture-and-dependency-boundaries.md)
govern every project edge listed below.

| Project               | Responsibility                                                                                                                         | Direct dependencies                                                                                        |
| --------------------- | -------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| AgentKit.Abstractions | [Provider-neutral contracts, typed identities, stable errors, deterministic primitives, and immutable values](foundation-contracts.md) | BCL and Microsoft abstractions required by public contracts                                                |
| AgentKit              | Process-level AgentEngine, builder, agent-definition catalog/validation, standalone and hosted composition, and build validation       | AgentKit.Abstractions, shared AgentKit.Observability infrastructure, and Microsoft.Extensions abstractions |

AgentKit.Abstractions never references AgentKit or a concrete implementation.
AgentKit never references feature packages. Applications choose all concrete
parts explicitly. AgentKit may `TryAdd` only its documented foundation defaults:
`TimeProvider.System`, explicit closed `IIdentifierGenerator<TIdentifier>`
registrations for framework-created identities, a singleton
`IRandomizerFactory`, and a singleton `IContentHasher`. The factory creates an
operation-owned `IRandomizer`; no mutable randomizer is shared through the
container. These defaults are replaceable and do not authorize effects or pull
in a runtime package.

## Runnable spine

| Project                     | Responsibility                                                                                        | Registration              |
| --------------------------- | ----------------------------------------------------------------------------------------------------- | ------------------------- |
| AgentKit.Loop               | Default agent loop, turn coordination, state transitions, and run settlement                          | AddAgentLoop              |
| AgentKit.Budgets            | Hierarchical limits, atomic reservations, accounting, and limit outcomes                              | AddAgentBudgets           |
| AgentKit.Context            | Default context assembler and ordered contributor pipeline                                            | AddAgentContext           |
| AgentKit.Context.Compaction | [Semantic cut selection, summarization, validation, and compaction activation](context-compaction.md) | AddAgentContextCompaction |
| AgentKit.Hooks              | Hook dispatch, ordering, mutation validation, and immutable per-run catalogs                          | AddAgentHooks             |
| AgentKit.IO                 | Input admission, queued promotion, live event fan-out, and final results                              | AddAgentIO                |
| AgentKit.Output             | Output contracts, extraction, validation, repair decisions, and conversion                            | AddAgentOutput            |
| AgentKit.Session            | Session coordination, lane-operation ownership, serialized mutation, branching, and store use         | AddAgentSession           |
| AgentKit.Permissions        | Security authority, policy evaluation, approvals, grants, and human control                           | AddAgentPermissions       |
| AgentKit.Providers          | Model catalog, selection, capability validation, and model request execution                          | AddAgentProviders         |
| AgentKit.Observability      | Shared Microsoft logging, activity, metric, tag, and registration conventions                         | AddAgentKitObservability  |

The engine-wide facade requires one agent-definition catalog/validator, a
`TimeProvider`, and identifier generators for every framework-created typed ID.
Each published agent definition resolves one effective keyed implementation for
every required spine axis and one explicit session-store/model selection; the
catalogs may contain many alternatives. It does not care whether first-party or
application packages supply those keys. AgentKit.Permissions supplies
fail-closed defaults; protected operations deny or defer when no configured
policy or approval path can grant them.

AgentKit.IO owns runtime I/O coordination, not durable conversational truth.
Durable admission and queued-input facts are written through session contracts;
the session coordinator remains authoritative for ordering and recovery.
Reusable channel adapters may use leaf packages such as AgentKit.IO.AspNetCore
or AgentKit.IO.Console when their behavior is substantial enough to justify a
package.

## Type, package, registration, and lifetime map

This is the compact ownership topology. Component documents define the full
contracts; this table prevents a default or lifetime from quietly migrating to
the facade.

| Boundary                 | Neutral contracts and values                                                                 | First-party owner / registration                                                                                                        | Cardinality and normal lifetime                                                                                  |
| ------------------------ | -------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| Engine and definitions   | `AgentDefinition`, `IAgentDefinitionCatalog`, typed IDs, `IIdentifierGenerator<TIdentifier>` | AgentKit / `AddAgentKit` or `AgentEngine.CreateBuilder`                                                                                 | Engine-wide singular catalog/validator/generators; additive definition sources; one scope per run                |
| Loop and run             | `IAgentLoop`, run context/view, limits and outcomes                                          | AgentKit.Loop / `AddAgentLoop`                                                                                                          | Keyed, singular per definition selection; default loop and mutable run services scoped                           |
| Budgets                  | `IBudgetAuthority`, `IBudgetScope`, reservations, limits, usage                              | AgentKit.Budgets / `AddAgentBudgets`                                                                                                    | Engine/tenant authority with run- and operation-owned child scopes and reservations                              |
| Input/output and context | I/O coordinator/publisher, queues, context assembler/contributors, compaction contracts      | AgentKit.IO / `AddAgentIO`; AgentKit.Context / `AddAgentContext`; AgentKit.Context.Compaction / `AddAgentContextCompaction`             | Keyed, singular assembler and compactor selections; additive ordered contributors/strategies                     |
| Structured output        | output definitions, resolver, processor, validators, repair decisions                        | AgentKit.Output / `AddAgentOutput`                                                                                                      | Keyed run-scoped processor; immutable definitions; additive ordered validators                                   |
| Identity                 | execution identity, issuer mappings, validation and delegation derivation                    | AgentKit.Identity / `AddAgentIdentity`; authentication-specific leaves                                                                  | Scoped ingress resolution; immutable identity values flow downstream without callback                            |
| Hooks                    | Typed dispatch kernel, closed point definitions, dedicated hook interfaces and `EventArgs`   | AgentKit.Hooks / `AddAgentHooks`; point contracts remain in their owning abstraction package                                            | Engine-wide kernel; stage-selected captured profile/catalog; additive points and hooks with declared lifetimes   |
| Session                  | coordinator plus `ISessionStore` and typed entries/cursors                                   | AgentKit.Session / `AddAgentSession`; explicit store packages                                                                           | One effective coordinator/store profile per definition; keyed stores normally singleton/thread-safe              |
| Security                 | authority, policies, approvals, grants, audit contracts                                      | AgentKit.Permissions / `AddAgentPermissions`                                                                                            | One effective authority/policy/approval selection per definition; additive policies/handlers/sinks               |
| Providers                | model catalog/selector/executor plus endpoint, credential, and keyed operation contracts     | AgentKit.Providers / `AddAgentProviders`; concrete provider `Add...` packages                                                           | Engine-wide catalog; independently keyed captured profiles; additive operations; executor scoped                 |
| Tools                    | providers/catalog/resolver/validator/scheduler/invoker, terminal recorder, result projector  | AgentKit.Tools and AgentKit.Tools.ToolName / package `Add...Tool`                                                                       | Keyed runtime/toolset/projection policy; additive sources; invocation state operation-owned                      |
| Memory/goals/durability  | Narrow stores, retrieval, goal/delegation, checkpoint/lease contracts                        | `AgentKit.Memory.*`, AgentKit.Goals; AgentKit.Durability / `AddAgentDurability` plus `AgentKit.Durability.<BackendName>` leaves         | Optional; singular coordinators/catalogs with additive keyed strategies/backends/stores; explicit durable owners |
| Observation              | `IRunEventSink`, `ISecurityAuditSink`, redaction values                                      | AgentKit.Observability / `AddAgentKitObservability`; AgentKit.Observability.OpenTelemetry / `AddOpenTelemetryObservability`             | Shared process-lifetime Microsoft sources; additive keyed sinks; singular replaceable redactor for capture       |
| MCP                      | Protocol-version identities and reflected object-in/object-out tool contracts                | AgentKit.Mcp shared contracts; AgentKit.Mcp.Client / `AddMcpToolClient`; AgentKit.Mcp.Server / `AddAgentKitMcpServer`                   | Immutable contracts/catalog generations; typed clients explicitly owned; server tool classes activated per call  |
| File system              | Narrow bounded-read and explicit atomic-write/directory/metadata/watch contracts             | AgentKit.FileSystem / `AddOperatingSystemFileSystem`; AgentKit.FileSystem.InMemory / `AddInMemoryFileSystem`                            | Singular narrow services per profile key; singleton services, operation-owned handles                            |
| Network                  | `INetworkNameResolver`, `INetworkTransport`, request/response values                         | AgentKit.Network / `AddAgentNetwork`; AgentKit.Network.InMemory / `AddAgentNetworkInMemory`                                             | Singular resolver/transport pair; singleton pools, operation-owned responses                                     |
| Processes                | resolver, executor, sandbox, handle/output values                                            | AgentKit.Processes / `AddAgentProcesses`; AgentKit.Processes.Scripted / `AddScriptedProcesses`                                          | Singular resolver/executor per key, keyed sandboxes; singleton services, caller-owned handles                    |
| Language intelligence    | query identities, diagnostics, hover, locations, symbols, document freshness                 | Host-selected leaf; AgentKit.LanguageServices.Scripted / `AddScriptedLanguageIntelligence`; AgentKit.Tools.Language / `AddLanguageTool` | Singular selected service per profile; immutable operation results; tool invocation state operation-owned        |
| Artifacts                | artifact references, coordinator, store catalog, retention, integrity                        | AgentKit.Artifacts / `AddAgentArtifacts`; explicit backend leaves                                                                       | Keyed coordinators/stores; operation-owned streams; immutable references                                         |
| Evaluation               | `IEvaluationRunner`, keyed evaluators, stores, exporters                                     | AgentKit.Evaluation / `AddAgentEvaluation`                                                                                              | Optional singular runner, additive/keyed collaborators; isolated ordinary agent run scopes                       |

Singular defaults use `TryAdd` and have an explicit ordinary-DI replacement
path. Additive services preserve deterministic registration order. Keyed
services reject conflicting keys and are selected by an injected catalog or
selector, never by passing `IServiceProvider` into runtime code. Repeated
identical package calls are idempotent; conflicting configuration fails
validation unless the registration explicitly defines a merge.

Every behaviorally meaningful choice lives in one of four places: DI chooses
implementations and topology, package options choose validated process defaults,
an immutable agent definition selects reusable agent behavior, and run options
provide bounded per-run overrides. A package owns and documents its safe
defaults. The AgentKit facade cannot instantiate a hidden loop, store, provider,
security authority, tool, exporter, protocol adapter, or host-access backend.

## Acyclic dependency graphs

AgentKit validates two different graphs. Passing the project-reference graph is
necessary, but it does not prove the DI graph can be constructed.

The project graph has fixed ranks:

```text
applications, hosts, and tests
    -> vendor, transport, persistence, hosting, and backend leaves
        -> focused first-party runtime packages
            -> AgentKit.Abstractions
                -> BCL and Microsoft.Extensions abstractions

applications and hosts -> AgentKit facade -> AgentKit.Abstractions
```

Behavioral runtime packages exchange contracts and values from
AgentKit.Abstractions rather than referencing sibling implementations.
`AgentKit.Observability` is the explicit shared infrastructure exception:
first-party facade, runtime, and leaf packages may reference its exporter-free
diagnostic names and emission helpers. It depends only on Abstractions, BCL, and
Microsoft abstractions and never references a consumer or exporter.
`AddAgentKitObservability` configures that surface; emitting standard
diagnostics does not require opting into an exporter.

`AgentKit.Evaluation` is an application-facing leaf. It may reference the public
AgentKit facade because it drives ordinary engine operations; the facade and
behavioral runtime packages never reference Evaluation. Its optional evaluator
extension contracts may live in that leaf because core execution does not use
them. This is not an exception allowing the loop to depend on the facade.

A leaf may reference the one or more runtime extension surfaces it adapts, but
an edge never points back from a runtime package to that leaf. AgentKit remains
a sibling facade, not a parent assembly referenced by runtime packages.

The runtime service graph is also directed:

```text
AgentEngine runtime -> run-scope factory -> compiled run plan -> agent loop
agent loop -> session/input/context/provider/tools/output/budget collaborators
context -> history/retrieval/compactor/output-definition collaborators
protected component -> security authority -> policy/grant/approval stores
consumer -> artifact coordinator -> artifact backend
all components -> hook dispatcher and immutable event sinks
```

The lower node never constructor-depends on the caller above it. In particular:

- session, approval, grant, budget, artifact, and durability stores use their
  own narrow persistence contracts; they do not implement durability by calling
  a higher coordinator that already depends on them;
- output processing returns retry or repair decisions to the loop and never
  calls the provider executor;
- model-backed compaction uses a dedicated summary-generation operation and
  never calls the context assembler;
- identity resolution finishes before admission or run activation; downstream
  components consume the immutable identity and never call the resolver;
- event sinks and hooks do not call the dispatcher or effecting component during
  the same dispatch; explicit nested work is a new bounded operation; and
- artifact finalization never appends its own session/tool record. The caller
  owns cross-store prepare/finalize or outbox coordination.

Composition validation builds descriptors for every selected closed constructor
and declared factory graph, rejects strongly connected components, checks keyed
selections, and reports the full dependency path. It validates declared
dependencies; it cannot prove arbitrary executable factory or callback behavior.
Conformance tests and review enforce that declarations are complete and that
runtime callbacks respect operation boundaries. Deferred factories, `Lazy<T>`,
`Func<T>`, scopes, or service-provider lookups are not accepted as ways to hide
a cycle. A factory is valid only when it is an explicit operation boundary whose
created graph is independently acyclic and whose lifetime is owned.

## Session storage

AgentKit.Session does not choose a storage medium. Storage implementations are
leaf packages:

- AgentKit.Session.InMemory supplies deterministic ephemeral storage for tests,
  examples, and short-lived applications.
- AgentKit.Session.Sqlite supplies the first durable local store.
- Future database or distributed stores follow AgentKit.Session.ProviderName.

The session coordinator and store are registered separately. There is no hidden
production store. Composition fails when a session coordinator has no store.

## Provider runtime and integrations

This split implements the
[provider identity and capability model](../concepts/model-providers-and-capabilities.md):
neutral selection stays in the runtime package, while wire behavior stays in
leaf integrations.

AgentKit.Providers contains no vendor protocol. It combines configured model
descriptors, selects a model, validates required capabilities, and executes
provider attempts under the loop's budgets and fallback policy.

Provider integrations follow AgentKit.Providers.ProviderName:

| Project                             | Responsibility                                                                                                                 | Registration                |
| ----------------------------------- | ------------------------------------------------------------------------------------------------------------------------------ | --------------------------- |
| AgentKit.Providers.OpenAICompatible | Reusable Responses, Chat Completions, embeddings, transport, parsing, profiles, and secret-safe credential transport mechanics | AddOpenAICompatibleProvider |
| AgentKit.Providers.OpenAI           | OpenAI endpoints, credentials, capabilities, conversational models, and embeddings                                             | AddOpenAI                   |
| AgentKit.Providers.OpenRouter       | OpenRouter routing, metadata, conversational models, embeddings, and reranking                                                 | AddOpenRouter               |
| AgentKit.Providers.ZAi              | Z.ai endpoints, credentials, Chat Completions profile, and native operations                                                   | AddZAi                      |

AgentKit.Providers.OpenAICompatible is a protocol-family toolkit for concrete
providers and custom compatible endpoints. It is not a brand identity or a claim
that every OpenAI-shaped endpoint supports the same behavior. OpenAI,
OpenRouter, and Z.ai each have their own package, options, compatibility
profile, descriptors, and registration.

Authentication support and provider composition remain concrete-leaf policy. A
wire-family helper may obtain opaque credential material or construct a
profile-approved header, but each branded package owns endpoint/service-surface
profiles, credential/account profiles, scheme, audience, scopes, refresh,
rotation, and the binding of both profiles to an operation adapter. No provider
runtime consumes one unkeyed global credential source.

Each vendor package exposes one ASP.NET-style entry point and registers its
supported operations independently. AddOpenAI can configure named conversational
and embedding models. AddOpenRouter can configure named conversational,
embedding, and reranking models. AddZAi exposes only operations supported by its
verified profile; it does not manufacture an embedding provider from a chat
endpoint.

Each configured operation captures a versioned endpoint profile and a versioned
credential profile independently from its model alias. A package may expose an
opt-in named profile for a well-known public endpoint; it never chooses that
origin merely because endpoint configuration is absent. This keeps multiple
providers, service surfaces, deployments, and accounts composable inside one
engine without registration-order coupling.

Further provider packages fall into three families:

- compatible integrations such as DeepSeek, Groq, Moonshot Kimi, xAI, and
  selected Ollama APIs may reuse OpenAICompatible where conformance proves the
  shared wire behavior;
- native integrations such as Anthropic, Google Gemini, Mistral AI, and Cohere
  preserve their own content and lifecycle semantics; and
- cloud brokers such as Azure OpenAI, Google Vertex AI, and Amazon Bedrock own
  deployment, region, identity, and platform-specific behavior even when they
  reuse a payload translator.

Provider research coverage is not an automatic promise to ship every package. A
package is added when AgentKit can describe its supported operations and run the
relevant shared conformance suites.

Conversation, embeddings, reranking, media generation, token counting, and
provider-native tools are separate contracts. A vendor package may implement
several of them, but an application can select and replace each operation
without replacing the others.

## Tools

AgentKit.Tools contains the optional default catalog, resolver, validation,
scheduler, invocation, and result pipeline. Individual tool features remain
small packages:

- AgentKit.Tools.Read;
- AgentKit.Tools.Write;
- AgentKit.Tools.List, AgentKit.Tools.Glob, and AgentKit.Tools.Search;
- AgentKit.Tools.Edit and AgentKit.Tools.Patch;
- AgentKit.Tools.Command and AgentKit.Tools.Language;
- AgentKit.Tools.Web, AgentKit.Tools.WebSearch, AgentKit.Tools.Resource,
  AgentKit.Tools.Question, AgentKit.Tools.Plan, AgentKit.Tools.Task, and
  AgentKit.Tools.Skill; and
- future packages following AgentKit.Tools.ToolName.

AgentKit.Tools.Skill registers both the skill tool and its context contributor.
Read and write tools depend on file-system contracts, not the operating-system
implementation. Installing a tool package does not grant permission to invoke
it.

## Protected host access

AgentKit.FileSystem supplies the real operating-system implementation of the
file-system contracts. AgentKit.FileSystem.InMemory supplies a deterministic
implementation for applications and tests. Framework and tool packages depend on
the abstractions only. The registration entry points are
`AddOperatingSystemFileSystem` and `AddInMemoryFileSystem`.

AgentKit.Network supplies security-aware DNS resolution, connection, redirect,
and data-egress behavior. AgentKit.Network.InMemory supplies deterministic
responses and network traces for tests. AgentKit.Tools.Web depends on these
abstractions rather than a concrete HTTP stack. `AddAgentNetwork` and
`AddAgentNetworkInMemory` select the real or deterministic implementation
explicitly.

AgentKit.Processes supplies security-aware process creation, arguments,
environment, working-directory, sandbox, cancellation, and output behavior.
AgentKit.Processes.Scripted supplies deterministic executions for tests. Shell
and code-execution tools depend on these abstractions rather than calling
operating-system process APIs directly. `AddAgentProcesses` and
`AddScriptedProcesses` register these alternatives.

Language-intelligence contracts remain in AgentKit.Abstractions. A selected host
leaf owns protocol lifecycle and workspace synchronization, while
AgentKit.Tools.Language owns only model-facing validation, authorization, and
bounded projection. AgentKit.LanguageServices.Scripted supplies deterministic
identified snapshots for tests and replay without starting a language server.

Each real implementation re-canonicalizes its operation, validates and consumes
the bounded security grant immediately before the effect, and fails closed when
enforcement or required audit is unavailable. A tool-level decision is not
permission to change the path, destination, executable, argument, content, or
principal at the lower boundary. Keyed host profiles do not weaken this rule.

## Optional components

| Project family                                  | Responsibility                                                                                          |
| ----------------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| AgentKit.Memory and AgentKit.Memory.BackendName | Memory policy, retrieval, documents, and storage or vector integrations                                 |
| AgentKit.Goals                                  | Goal lifecycle, attempts, delegation, communication, and joins                                          |
| AgentKit.Goals.Hosting                          | Host-owned worker draining durable local delegation intents through the public engine                   |
| AgentKit.Durability                             | Provider-neutral durability coordinator, recovery, catalog, selector, codecs, profiles, and fencing     |
| AgentKit.Durability.BackendName                 | Concrete journal, lease, checkpoint, or workflow-engine backend adaptation                              |
| AgentKit.Mcp                                    | Shared MCP protocol-version identities and reflected tool contracts                                     |
| AgentKit.Mcp.Client and AgentKit.Mcp.Server     | MCP client and server lifecycle, transports, and primitive adapters                                     |
| AgentKit.Observability.OpenTelemetry            | Activity, metric, log, and event export without runtime control                                         |
| AgentKit.Evaluation                             | Dataset execution, evaluators, result comparison, and reproducible reports through public AgentKit APIs |
| AgentKit.Identity                               | Trusted-ingress identity normalization, issuer mapping, validation, and delegation derivation           |
| AgentKit.Artifacts and backend leaves           | Durable content references, integrity, retention, reconciliation, and explicit storage backends         |

Optional components do not become hidden facade dependencies. Their service
registrations validate required collaborators only when the component is added.

## Deliberate non-projects

Some boundaries stay explicit without receiving a catch-all package:

- messages, content, events, identities, and result values live in
  AgentKit.Abstractions;
- canonical history and queued-input truth live behind AgentKit.Session;
- hook implementations live with the feature they extend, while the shared
  dispatcher and ordering machinery live in AgentKit.Hooks; there is no
  universal AgentKit.Extensions package;
- provider retries, tool retries, and run deadlines remain with their owning
  components rather than a generic AgentKit.Resilience layer that can repeat
  unsafe work;
- stable error values live in AgentKit.Abstractions, while each effect boundary
  owns its mapper; there is no central error manager that depends on every
  subsystem;
- credentials remain inside concrete integration boundaries and never enter a
  generic secret bag; and
- TimeProvider is the clock abstraction. AgentKit does not wrap it for sport.

## Project anatomy

Every source project follows the same shape:

- the project file declares package metadata and only its direct dependencies;
- GlobalUsings.cs contains project-wide namespaces and aliases;
- AssemblyInfo.cs contains InternalsVisibleTo for the matching test assembly and
  any other deliberate assembly-wide attributes;
- ServiceExtensions.cs exposes the package's ASP.NET-style service collection
  registrations; and
- named types each live in a matching file under a responsibility-based folder.

ServiceExtensions lives in the package's namespace, so AgentKit.Tools.Read and
AgentKit.Tools.Write can each expose a ServiceExtensions type without colliding.
Registration methods return the service collection, do not build or resolve a
provider, are idempotent where practical, and document singular, additive, and
replacement behavior.

AgentKit.Abstractions does not provide a meaningless no-op registration. Its
ServiceExtensions contains only generic helpers for registering user-supplied
implementations against neutral contracts; concrete defaults remain outside the
package.

## Test projects

The
[testing and evaluation specification](../concepts/testing-and-evaluation.md)
defines which behaviors belong in package tests, shared conformance suites, API
compatibility snapshots, and composed-agent evaluations.

The tests directory mirrors source projects one for one:

| Source project                       | Test project                               |
| ------------------------------------ | ------------------------------------------ |
| AgentKit                             | AgentKit.Tests                             |
| AgentKit.Abstractions                | AgentKit.Abstractions.Tests                |
| AgentKit.Loop                        | AgentKit.Loop.Tests                        |
| AgentKit.Budgets                     | AgentKit.Budgets.Tests                     |
| AgentKit.Context                     | AgentKit.Context.Tests                     |
| AgentKit.Context.Compaction          | AgentKit.Context.Compaction.Tests          |
| AgentKit.Hooks                       | AgentKit.Hooks.Tests                       |
| AgentKit.IO                          | AgentKit.IO.Tests                          |
| AgentKit.Output                      | AgentKit.Output.Tests                      |
| AgentKit.Identity                    | AgentKit.Identity.Tests                    |
| AgentKit.Session                     | AgentKit.Session.Tests                     |
| AgentKit.Session.InMemory            | AgentKit.Session.InMemory.Tests            |
| AgentKit.Session.Sqlite              | AgentKit.Session.Sqlite.Tests              |
| AgentKit.Permissions                 | AgentKit.Permissions.Tests                 |
| AgentKit.Providers                   | AgentKit.Providers.Tests                   |
| AgentKit.Providers.OpenAICompatible  | AgentKit.Providers.OpenAICompatible.Tests  |
| AgentKit.Providers.OpenAI            | AgentKit.Providers.OpenAI.Tests            |
| AgentKit.Providers.OpenRouter        | AgentKit.Providers.OpenRouter.Tests        |
| AgentKit.Providers.ZAi               | AgentKit.Providers.ZAi.Tests               |
| AgentKit.FileSystem                  | AgentKit.FileSystem.Tests                  |
| AgentKit.FileSystem.InMemory         | AgentKit.FileSystem.InMemory.Tests         |
| AgentKit.Network                     | AgentKit.Network.Tests                     |
| AgentKit.Network.InMemory            | AgentKit.Network.InMemory.Tests            |
| AgentKit.Processes                   | AgentKit.Processes.Tests                   |
| AgentKit.Processes.Scripted          | AgentKit.Processes.Scripted.Tests          |
| AgentKit.LanguageServices.Scripted   | AgentKit.LanguageServices.Scripted.Tests   |
| AgentKit.Tools.Language              | AgentKit.Tools.Language.Tests              |
| AgentKit.Tools.Web                   | AgentKit.Tools.Web.Tests                   |
| AgentKit.Tools.WebSearch             | AgentKit.Tools.WebSearch.Tests             |
| AgentKit.Tools.Resource              | AgentKit.Tools.Resource.Tests              |
| AgentKit.Tools.Question              | AgentKit.Tools.Question.Tests              |
| AgentKit.Tools.Plan                  | AgentKit.Tools.Plan.Tests                  |
| AgentKit.Tools.Task                  | AgentKit.Tools.Task.Tests                  |
| AgentKit.Tools.Skill                 | AgentKit.Tools.Skill.Tests                 |
| AgentKit.Goals                       | AgentKit.Goals.Tests                       |
| AgentKit.Artifacts                   | AgentKit.Artifacts.Tests                   |
| AgentKit.Artifacts.InMemory          | AgentKit.Artifacts.InMemory.Tests          |
| AgentKit.Mcp                         | AgentKit.Mcp.Tests                         |
| AgentKit.Mcp.Client                  | AgentKit.Mcp.Client.Tests                  |
| AgentKit.Mcp.Server                  | AgentKit.Mcp.Server.Tests                  |
| AgentKit.Observability               | AgentKit.Observability.Tests               |
| AgentKit.Observability.OpenTelemetry | AgentKit.Observability.OpenTelemetry.Tests |
| AgentKit.Evaluation                  | AgentKit.Evaluation.Tests                  |
| Each remaining source package        | A matching PackageName.Tests project       |

Test projects follow the Sharp Vision setup: .NET 10 executable test projects,
xUnit v3, Shouldly, Microsoft.NET.Test.Sdk, and Microsoft Testing Platform code
coverage. Moq is available where interaction testing is useful, not required by
habit.

AgentKit.Test.Shared is a non-packable support library for deterministic fakes,
fixtures, builders, and assertion helpers. AgentKit.Conformance is a
non-packable support library containing reusable behavioral suites. Each
implementation test project instantiates the relevant suites through its public
registration surface. AgentKit.Compatibility.Tests snapshots the public API of
every packable assembly with PublicApiGenerator and Verify.XunitV3.

Compatible provider tests run two layers: shared protocol-family conformance and
the concrete package's capability, options, credentials, errors, and DI
registration tests. All time-dependent tests replace TimeProvider and use
controllable tasks and barriers instead of wall-clock sleeps.

## Solution organization

The solution groups projects under source, tests, and examples. Test project
names mirror package names exactly. Examples reference public packages and use
the same builder and service registrations available to applications; they do
not receive privileged internal access.
