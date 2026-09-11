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
invoker. Aggregate catalog capture, canonical resolution/invocation, and loop
integration remain under construction; this source-capture component supplies
their retained binding and lifetime boundary.

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
