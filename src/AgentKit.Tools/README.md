# AgentKit.Tools

Catalog, validate, authorize, and invoke application tools.

Use this runtime with individually selected tool features. Tool descriptions are
discovery data; execution still needs argument validation, security policy, and
the appropriate backing services.

## Use this project

Start with `AddAgentTools`, `AddTool` in
[ServiceExtensions.cs](ServiceExtensions.cs). Read the overloads and XML
documentation for required collaborators, lifetimes, and duplicate-registration
behavior. `AllowListToolAuthorizer` (registered by `AddAgentTools`) fails closed
by default: every call is denied until its `ToolId` is added to
`AgentToolsOptions.AllowedToolIds`, or
`AgentToolsOptions.AllowAllRegisteredTools` is set to grant every registered
tool at once. Build the model-facing `LlmToolDefinition` for a resolved
`ToolDescriptor` (or a whole `IToolCatalog.Descriptors` sequence) with
`ToLlmToolDefinition`/ `ToLlmToolDefinitions` in
[ToolDescriptorExtensions.cs](ToolDescriptorExtensions.cs) and
[ToolDescriptorCollectionExtensions.cs](ToolDescriptorCollectionExtensions.cs).

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## Static application discovery

`AddStaticToolProvider(snapshot, invokers)` registers a
[StaticToolProvider](StaticToolProvider.cs) under the snapshot's exact typed
`ToolSourceId` key. Supply the complete immutable publication and its exact
identity-to-invoker map; source versions and aliases are never invented.
Duplicate source keys reject before mutation. Use `ReplaceStaticToolProvider`
for an explicit change that affects later hosts while keeping old providers and
captures intact. Registration preserves host logging and clocks and activates no
services.

The provider returns a fresh owned source capture for each `DiscoverAsync`
request. Captures share the immutable binding graph and keep separate lease and
closure state. Invokers remain borrowed from their original host owner, which
must keep them alive through every outstanding capture and lease. Use a
different provider for per-request instances, principal-specific data, or
protected remote discovery.

Static discovery publishes configured metadata. The catalog still decides source
selection, collisions, aliases, and model/schema support before exposure.

## Materialized registrations

`AddToolset(publication)` registers a complete immutable `ToolsetPublication`,
including its real version, source membership, execution-policy reference, and
explicit aliases. `AddToolProvider<TProvider>(sourceId)` registers a host-owned
singleton discovery strategy under its exact typed source key. The provider can
receive that key through the standard `[ServiceKey]` constructor parameter. The
instance overload borrows a provider whose original owner retains disposal.
Static providers use the same registration catalog.

`AddToolRegistrationCatalog()` installs the replaceable
`IToolRegistrationCatalog`. When materialized, it captures each explicit
publication and provider once, validates exact key cardinality and source
identity, and rejects missing published sources. It retains no container.
`ResolveSelection(request)` returns a complete `ToolDiscoverySelection`:
toolsets follow authored request order, and shared sources appear once in first
use order. Unknown toolsets or mismatched policy families reject before any
provider discovery. Empty selection exposes no fallback tools or sources.

`ReplaceToolset`, `ReplaceToolProvider`, and
`ReplaceToolRegistrationCatalog<TCatalog>` affect later compositions. Existing
hosts and selections retain their original publications and provider instances.
Additions reject duplicate exact keys before mutation; replacements preserve
unrelated typed, string, keyed, and unkeyed registrations without activation.
Registration order does not select a source or an execution-policy version.

This view supplies selection evidence for catalog discovery. The source
discovery owner described below retains acquisitions through the remaining
schema/capability preflight and legacy `IToolCatalog` migration.

## Discovery ownership

`AddToolRegistrationCatalog()` also registers the internal
`ToolCatalogDiscovery` coordinator, requiring one materialized registration
view. It resolves the full request before contacting providers, discovers each
distinct source once in first-use order, and checks the provider and returned
publication against the exact registered source identity. It takes ownership of
each returned capture before checking cancellation or reading metadata. Null,
reused, or malformed captures reject without exposing a partial graph.

The internal `ToolDiscoveryCapture` retains every exact publication and source
owner through merge and schema/capability preflight. Disposing it releases every
source, including empty or unselected publications. All cleanups start before
any is awaited. A failing discovery retains its original exception followed by
cleanup failures in source-ID order; successful cleanup preserves cancellation
and its token. Provider services and borrowed invokers keep their original
owner.

After merge and preflight, the caller can transfer the graph once into
`ToolCatalogCapture`. Transfer and closure have one synchronized winner. Failed
construction retains ownership with discovery; successful handoff uses the
captured publications without rereading live snapshot getters. Disposing the old
discovery owner cannot close the transferred catalog or its leases. Repeated
disposal shares completion and failure without retrying cleanup.

This completes source acquisition and cleanup ownership. Canonical schema
integration and model-capability preflight and replacement of the legacy
`IToolCatalog` path remain open; discovery alone does not make a catalog ready
for model exposure.

## Canonical schema validation

`services.AddToolSchemaEngine()` registers the replaceable local compiler.
Resolve `IToolSchemaEngine`, compile an owned canonical `JsonSchema` with
explicit `ToolSchemaLimits`, then retain the returned `ICompiledToolSchema`.
Validate parsed instances with fresh limits. Compilation rejects unsupported or
malformed schemas before producing a handle; validation returns valid, invalid,
or resource-limited, while cancellation propagates.

The default `agentkit-bounded-tool-schema` revision 1 supports the structural,
exact numeric, length/count, enum/const, and uniqueness subset documented in the
[tool architecture](../../docs/architecture/tools.md). It preserves canonical
schemas, treats format as annotation, and rejects references, regex assertions,
unions, nested dialect changes, and unknown keywords. It performs no I/O or
provider downgrade. Bounds include raw UTF-8 bytes, depth, nodes, and total
comparison work; each concurrent validation gets a separate budget.

`ReplaceToolSchemaEngine<TEngine>()` explicitly replaces unkeyed registrations
while preserving keyed engines, old hosts, and compiled handles. Catalog
integration, provider/model translation preflight, and bounded argument parsing
remain separate pending stages.

## Catalog collision policy

`AddToolCatalogMerging()` registers `RejectingToolCatalogMergePolicy` and the
internal immutable merge coordinator. The default accepts unambiguous graphs and
rejects identity, alias, and missing-target collisions. Hosts can use
`ReplaceToolCatalogMergePolicy<TPolicy>()` to select explicitly configured
captured contributions. Multiple unkeyed policies reject composition.

The coordinator validates the full source graph before policy, preserves
authored descriptor order and empty source versions, and revalidates every
selected source, descriptor, policy, and alias. An alias must agree with its
selected binding. Policy cannot fabricate metadata, drop a missing target, or
publish a partial graph. Cancellation after policy completion prevents snapshot
transfer. This component borrows metadata; discovery, capture cleanup, and
schema/capability preflight still belong to catalog capture.

## Retained catalog captures

[ToolCatalogCapture](ToolCatalogCapture.cs) takes an already merged
`ToolCatalogSnapshot` and its exact map of owned source captures. Construction
checks every selected descriptor and source version before taking ownership;
unselected source tools cannot be acquired through the catalog. Source snapshots
are read once, and later metadata changes never redirect an acquisition.

The catalog validates each returned source lease in full and wraps its
ownership. Closing waits for pending acquisitions and leases. A late lease after
closure or cancellation is released before returning. Source cleanup starts for
every owner, including empty sources; failures remain observable without
suppressing another cleanup or retrying a previous one. Callers release their
leases before awaiting catalog closure. Borrowed invokers keep their original
disposal owner.

This object supplies retained catalog lifetime. Schema/capability preflight and
replacement of the legacy `IToolCatalog` coordinator remain separate work;
`AddAgentTools` still selects the legacy runtime.

## Retained source captures

Providers create a [ToolProviderCapture](ToolProviderCapture.cs) from one
`ToolProviderSnapshot`, its complete exact identity-to-invoker map, and an
optional owned source lifetime such as an `AsyncServiceScope`. Construction
rejects missing, extra, null, default, or duplicate normalized bindings before
taking ownership. Captures belong to source acquisitions and are not singleton
DI registrations.

`AcquireInvokerAsync` returns an independent lease to the retained instance or
`ToolInvokerUnavailable` for that exact identity. It performs no lookup against
current registrations and never invokes the tool. Closing blocks new
acquisitions and waits for existing leases before owned cleanup. Release your
leases before awaiting closure on the same control path. Repeated disposal
shares completion and failure; it never retries cleanup. A released lease keeps
its immutable metadata readable but rejects further invoker access.

The capture never disposes individual invokers. A supplied lifetime owns their
source resources; a null lifetime leaves them under external host ownership.
Acquisition, release, closure, and cleanup emit isolated structured logs,
activities, and bounded operation/outcome metrics without descriptor content.

The existing `AddAgentTools` path still uses the legacy catalog and combined
invoker. Catalog coordination, canonical resolution/invocation, and loop
integration remain under construction; source and catalog captures supply their
retained binding and lifetime boundaries.

## Retained projection policies

Register immutable policy snapshots with
`AddToolResultProjectionPolicy(snapshot)`. `AddAgentTools` installs the
replaceable `IToolResultProjectionPolicyCatalog`; it can also be registered
independently with `AddToolResultProjectionPolicyCatalog()`.

The catalog captures configured revisions once. Equivalent duplicates are
idempotent; conflicting content under one reference rejects composition.
`ResolveAsync` returns the exact snapshot or
`ToolResultProjectionPolicyUnavailable` for that reference. It never selects the
newest revision or creates a default. Keep revisions required by recorded
results, including across host restarts, or use
`ReplaceToolResultProjectionPolicyCatalog<TCatalog>()` to select another
retention implementation. The built-in catalog is immutable configuration, not a
persistent policy store.

`ReplaceToolResultProjectionPolicy(snapshot)` is a composition-time replacement
for one exact reference. It preserves other revisions and existing catalog
instances, and rejects opaque snapshot registrations before changing services.
It must not rewrite policy content required by retained results.

Lookup emits content-free reference metadata, isolated activities/logs, and
bounded outcome/count/duration metrics. The complete tool-result projector and
executor integration remain under construction.

## Related projects

- [AgentKit.Tools.Read](../AgentKit.Tools.Read/README.md) — read bounded file
  content through the filesystem abstraction.
- [AgentKit.Tools.Command](../AgentKit.Tools.Command/README.md) — offer an
  explicit shell-command tool over the process boundary.
- [AgentKit.Permissions](../AgentKit.Permissions/README.md) — evaluate security
  requests and manage bounded grants, profiles, and audit dispatch.
- [AgentKit.Loop](../AgentKit.Loop/README.md) — coordinate turns, context
  preparation, model attempts, tool calls, and terminal outcomes.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Tools.Tests](../../tests/AgentKit.Tools.Tests/README.md) — focused
  behavior and registration tests.
- [Component specification](../../docs/architecture/tools.md) — intended
  ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
