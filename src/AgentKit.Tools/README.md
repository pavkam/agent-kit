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
