# AgentKit.Tools

Catalog, validate, authorize, and invoke application tools.

Use this runtime with individually selected tool features. Tool descriptions are
discovery data; execution still needs argument validation, security policy, and
the appropriate backing services.

## Use this project

Start with `AddAgentTools`, `AddTool` in
[ServiceExtensions.cs](ServiceExtensions.cs). Read the overloads and XML
documentation for required collaborators, lifetimes, and duplicate-registration
behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable component
example, follow [Getting started](../../docs/getting-started.md). Complete
engine composition is described in the
[composition guide](../../docs/guides/composition.md).

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

This object supplies retained catalog lifetime. Catalog merge decisions, dynamic
provider registration, and replacement of the legacy `IToolCatalog` coordinator
remain separate work; `AddAgentTools` still selects the legacy runtime.

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
