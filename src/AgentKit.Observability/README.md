# AgentKit.Observability

Share AgentKit logging, activity, metric, and tag conventions across components.

Use the common diagnostics surface to subscribe through standard .NET
instrumentation. This package carries no exporter SDK; hosts choose their own
logging and telemetry backends.

## Use this project

Start with `AddAgentKitObservability` in
[ServiceExtensions.cs](ServiceExtensions.cs). Read the overloads and XML
documentation for required collaborators, lifetimes, and duplicate-registration
behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable component
example, follow [Getting started](../../docs/getting-started.md). Complete
engine composition is described in the
[composition guide](../../docs/guides/composition.md).

## Tool-source capture diagnostics

Retained source capture uses `ToolInvokerAcquire`, `ToolInvokerRelease`,
`ToolProviderCaptureClose`, and `ToolProviderCaptureDisposeResources` from
`AgentKitActivityNames`. The shared capture counter and duration histogram use
only bounded operation/outcome dimensions. Source and tool identities and exact
versions belong on traces and structured logs, never metrics; descriptor,
argument, result, and exception-message content is excluded.

## Related projects

- [AgentKit](../AgentKit/README.md) — compose a process-level engine that hosts
  immutable agent definitions and isolated runs.
- [AgentKit.Hooks](../AgentKit.Hooks/README.md) — dispatch typed lifecycle hooks
  with ordering, mutation validation, and failure policy.
- [AgentKit.Permissions](../AgentKit.Permissions/README.md) — evaluate security
  requests and manage bounded grants, profiles, and audit dispatch.

This project has no source-project dependencies.

## Tests and reference

- [AgentKit.Observability.Tests](../../tests/AgentKit.Observability.Tests/README.md)
  — focused behavior and registration tests.
- [Component specification](../../docs/architecture/observability.md) — intended
  ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
