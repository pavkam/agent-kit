# Project structure

**Role:** Turn the component architecture into concrete projects and dependency
rules.

AgentKit uses one project for each independently selectable implementation or
external dependency boundary. A component may span several projects, and a
domain value does not earn a NuGet package merely because it has a name. The
result is granular where applications need choice and compact where splitting
would only create ceremony.

## Foundation projects

| Project               | Responsibility                                                                           | Direct dependencies                                         |
| --------------------- | ---------------------------------------------------------------------------------------- | ----------------------------------------------------------- |
| AgentKit.Abstractions | Provider-neutral contracts and immutable domain values used by every package             | BCL and Microsoft abstractions required by public contracts |
| AgentKit              | AgentEngine, AgentEngineBuilder, standalone and hosted composition, and build validation | AgentKit.Abstractions and Microsoft.Extensions abstractions |

AgentKit.Abstractions never references AgentKit or a concrete implementation.
AgentKit never references feature packages. Applications choose all concrete
parts explicitly.

## Runnable spine

| Project              | Responsibility                                                               | Registration        |
| -------------------- | ---------------------------------------------------------------------------- | ------------------- |
| AgentKit.Loop        | Default agent loop, turn coordination, budgets, and run settlement           | AddAgentLoop        |
| AgentKit.Context     | Default context assembler and ordered contributor pipeline                   | AddAgentContext     |
| AgentKit.IO          | Input admission, queued promotion, live event fan-out, and final results     | AddAgentIO          |
| AgentKit.Session     | Session coordination, active-run ownership, branching, and store use         | AddAgentSession     |
| AgentKit.Permissions | Permission policy evaluation, approval coordination, and human control       | AddAgentPermissions |
| AgentKit.Providers   | Model catalog, selection, capability validation, and model request execution | AddAgentProviders   |

The facade requires one implementation of each spine contract but does not care
whether these first-party projects or application implementations supply it. It
also requires an explicitly selected session store, at least one conversational
model, and a TimeProvider.

AgentKit.IO owns runtime I/O coordination, not durable conversational truth.
Durable admission and queued-input facts are written through session contracts;
the session coordinator remains authoritative for ordering and recovery.
Reusable channel adapters may use leaf packages such as AgentKit.IO.AspNetCore
or AgentKit.IO.Console when their behavior is substantial enough to justify a
package.

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

AgentKit.Providers contains no vendor protocol. It combines configured model
descriptors, selects a model, validates required capabilities, and executes
provider attempts under the loop's budgets and fallback policy.

Provider integrations follow AgentKit.Providers.ProviderName:

| Project                             | Responsibility                                                                     | Registration                |
| ----------------------------------- | ---------------------------------------------------------------------------------- | --------------------------- |
| AgentKit.Providers.OpenAICompatible | Reusable Responses, Chat Completions, embeddings, transport, parsing, and profiles | AddOpenAICompatibleProvider |
| AgentKit.Providers.OpenAI           | OpenAI endpoints, credentials, capabilities, conversational models, and embeddings | AddOpenAI                   |
| AgentKit.Providers.OpenRouter       | OpenRouter routing, metadata, conversational models, embeddings, and reranking     | AddOpenRouter               |
| AgentKit.Providers.ZAi              | Z.ai endpoints, credentials, Chat Completions profile, and native operations       | AddZAi                      |

AgentKit.Providers.OpenAICompatible is a protocol-family toolkit for concrete
providers and custom compatible endpoints. It is not a brand identity or a claim
that every OpenAI-shaped endpoint supports the same behavior. OpenAI,
OpenRouter, and Z.ai each have their own package, options, compatibility
profile, descriptors, and registration.

Each vendor package exposes one ASP.NET-style entry point and registers its
supported operations independently. AddOpenAI can configure named conversational
and embedding models. AddOpenRouter can configure named conversational,
embedding, and reranking models. AddZAi exposes only operations supported by its
verified profile; it does not manufacture an embedding provider from a chat
endpoint.

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
- AgentKit.Tools.Skill; and
- future packages following AgentKit.Tools.ToolName.

AgentKit.Tools.Skill registers both the skill tool and its context contributor.
Read and write tools depend on file-system contracts, not the operating-system
implementation. Installing a tool package does not grant permission to invoke
it.

## File system

AgentKit.FileSystem supplies the real operating-system implementation of the
file-system contracts. AgentKit.FileSystem.InMemory supplies a deterministic
implementation for applications and tests. Framework and tool packages depend on
the abstractions only.

## Optional components

| Project family                                  | Responsibility                                                                                          |
| ----------------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| AgentKit.Memory and AgentKit.Memory.BackendName | Memory policy, retrieval, documents, and storage or vector integrations                                 |
| AgentKit.Goals                                  | Goal lifecycle, attempts, delegation, communication, and joins                                          |
| AgentKit.Durability.BackendName                 | Checkpoints, leases, fencing, and workflow-engine adaptation                                            |
| AgentKit.Mcp.Client and AgentKit.Mcp.Server     | MCP client and server lifecycle, transports, and primitive adapters                                     |
| AgentKit.Observability.OpenTelemetry            | Activity, metric, log, and event export without runtime control                                         |
| AgentKit.Evaluation                             | Dataset execution, evaluators, result comparison, and reproducible reports through public AgentKit APIs |

Optional components do not become hidden facade dependencies. Their service
registrations validate required collaborators only when the component is added.

## Deliberate non-projects

Some boundaries stay explicit without receiving a catch-all package:

- messages, content, events, identities, and result values live in
  AgentKit.Abstractions;
- canonical history and queued-input truth live behind AgentKit.Session;
- extensions live with the component they extend rather than in a universal
  AgentKit.Extensions package;
- provider retries, tool retries, and run deadlines remain with their owning
  components rather than a generic AgentKit.Resilience layer that can repeat
  unsafe work;
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

The tests directory mirrors source projects one for one:

| Source project                      | Test project                              |
| ----------------------------------- | ----------------------------------------- |
| AgentKit                            | AgentKit.Tests                            |
| AgentKit.Abstractions               | AgentKit.Abstractions.Tests               |
| AgentKit.Loop                       | AgentKit.Loop.Tests                       |
| AgentKit.Context                    | AgentKit.Context.Tests                    |
| AgentKit.IO                         | AgentKit.IO.Tests                         |
| AgentKit.Session                    | AgentKit.Session.Tests                    |
| AgentKit.Session.InMemory           | AgentKit.Session.InMemory.Tests           |
| AgentKit.Session.Sqlite             | AgentKit.Session.Sqlite.Tests             |
| AgentKit.Permissions                | AgentKit.Permissions.Tests                |
| AgentKit.Providers                  | AgentKit.Providers.Tests                  |
| AgentKit.Providers.OpenAICompatible | AgentKit.Providers.OpenAICompatible.Tests |
| AgentKit.Providers.OpenAI           | AgentKit.Providers.OpenAI.Tests           |
| AgentKit.Providers.OpenRouter       | AgentKit.Providers.OpenRouter.Tests       |
| AgentKit.Providers.ZAi              | AgentKit.Providers.ZAi.Tests              |
| AgentKit.FileSystem                 | AgentKit.FileSystem.Tests                 |
| Each remaining source package       | A matching PackageName.Tests project      |

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
