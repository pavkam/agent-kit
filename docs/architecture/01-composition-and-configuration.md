# Composition and configuration

**Role:** Build a valid AgentEngine from independently selected components.

The AgentKit package is a dependency-light facade. It owns the public engine,
builder, hosted registration, composition validation, and lifecycle boundary. It
does not contain the loop, session coordinator, provider, permission engine,
storage, or tools.

## Public facade

AgentEngine is the immutable application-facing runtime facade. It exposes the
operations required to create or resume sessions, submit input, stream a run,
and await a final result without exposing the dependency container.

AgentEngine.CreateBuilder returns a separate mutable AgentEngineBuilder. The
builder collects configuration and exposes its service collection. Build
validates the complete graph, constructs the provider in standalone mode, and
returns an immutable engine. A built engine cannot be reconfigured.

The same registrations support standard .NET hosting. AddAgentKit registers the
facade and validation into an existing service collection. The host then owns
provider creation, scopes, shutdown, and disposal.

## Package boundary

AgentKit references AgentKit.Abstractions plus the required Microsoft.Extensions
dependency-injection, options, configuration, and logging abstractions. It does
not reference implementation packages. Applications add AgentKit.Loop,
AgentKit.Context, AgentKit.Session, a session store, a permission
implementation, the provider runtime, the I/O coordinator, and one or more
providers explicitly.

Feature packages expose service collection extensions such as AddAgentLoop,
AddAgentIO, AddAgentProviders, AddAgentSession, AddOpenAI, AddReadTool, and
AddWriteTool. They use the same registration path whether the application starts
with AgentEngineBuilder, ASP.NET Core, a worker host, or a custom service
collection.

## Build validation

A valid engine has exactly one effective loop, input coordinator, output
publisher, session coordinator, session store, context assembler, permission
policy, model catalog, model selector, and model request executor, plus at least
one conversational model and a TimeProvider. TimeProvider.System is registered
when the host has not supplied another instance.

Tools, skills, memory, embeddings, reranking, goals, MCP, evaluation, and extra
contributors are optional. Once an optional capability is registered, its
required collaborators must also be present. Build fails with component-specific
diagnostics for missing services, duplicate singular registrations, invalid
scopes, ambiguous keys, impossible limits, unsafe retry combinations, or
incompatible capabilities.

Validation runs before the first request. Hosted applications receive the same
checks during host validation or initial facade resolution; they do not discover
a missing provider halfway through a run.

## Agent definition and run scope

An agent definition is a reusable blueprint. It identifies the loop, models,
context contributors, tool sources, policies, stores, output contract, limits,
and extensions that form an agent. It is immutable after validation and safe to
share between concurrent runs.

A run scope contains mutable execution state: current turn, budget reservations,
run-scoped extension state, request snapshots, and cancellation. Nothing in the
run scope leaks into another run. Session state is loaded through the session
component; it is not kept indefinitely in a singleton agent object.

## Registration model

Registrations declare one of three shapes:

- singular registrations have one effective implementation and an explicit
  replacement path;
- additive registrations preserve deterministic order for context contributors,
  tool providers, middleware, observers, and similar collections; and
- named or keyed registrations support multiple providers, models, stores, or
  other selectable implementations without losing stable identity.

Repeated package registration is idempotent where practical. Collisions are
either rejected or resolved by documented precedence. Registration extensions
return the service collection and never build or resolve a provider.

## Configuration

Configuration is layered immutable input. Library defaults, host settings,
managed policy, workspace settings, agent definition, composed capabilities, run
options, and next-turn overrides have explicit precedence. Each value also
declares how it combines: replacement, append, keyed merge, deep merge, ordered
rules, or explicit reset.

Security constraints are not ordinary overridable values. Untrusted workspace
configuration cannot load executable extensions, inject credentials, or widen
tool, filesystem, network, or model authority. Invalid reloads leave the last
known-good snapshot active. In-flight work continues with its captured snapshot.

Credentials are resolved by leaf integrations when sending a request. They never
enter AgentEngineBuilder, agent definitions, options display, context manifests,
messages, or durable records.

## Ownership and disposal

In standalone mode AgentEngine owns the provider created by its builder and is
asynchronously disposable. In hosted mode the host owns the provider and
AgentEngine never disposes it. Owned services are disposed exactly once.

Immutable definitions, descriptors, and thread-safe catalogs may be singleton.
Run state and mutable capability instances are scoped. Operation adapters may be
transient. Every public service documents threading, ownership, and disposal.

## Related concept specifications

- [Agent definition and run context](../concepts/agent-definition-and-run-context.md)
- [Configuration and overrides](../concepts/configuration-and-overrides.md)
- [Public API and dependency injection](../concepts/public-api-and-dependency-injection.md)
