# AgentKit.Observability

Share AgentKit logging, activity, metric, and tag conventions across components.

Use the common diagnostics surface to subscribe through standard .NET
instrumentation. This package carries no exporter SDK; hosts choose their own
logging and telemetry backends.

Retained catalog captures use `tool.catalog.invoker.acquire`,
`tool.catalog.invoker.release`, `tool.catalog.capture.close`, and
`tool.catalog.capture.dispose_sources`. Their operation count/duration
instruments carry only bounded operation/outcome dimensions; traces and logs
retain captured tenant, principal, agent, session, run, and catalog-version
correlation.

Tool-provider discovery uses `tool.provider.discover`, with source and run
correlation on traces/logs. The discovery count and duration instruments carry
only bounded outcome tags; descriptors, schemas, and request content are
omitted.

Catalog merging and collision policy use `tool.catalog.merge` and
`tool.catalog.merge.policy`. Their shared count/duration instruments use bounded
operation and outcome tags. Outcomes are selected, rejected, cancelled, and
failed; logs and traces retain run identity without aliases, descriptors,
schemas, or exception-message content. Missing or reversed clock measurements
omit duration, and observer failures cannot change the merge result.

Registration selection uses `tool.registration.select`, with safe run identity
on logs and traces. Events 4070/4071 report start and completion; count/duration
metrics carry only the bounded outcome. Selected, unavailable, and cancelled
results retain truthful terminal status. Publication, alias, policy, and
exception content is omitted. Observer and clock failures do not change the
selection; unavailable duration is omitted.

Catalog discovery and preflight ownership use `tool.catalog.discover`,
`tool.catalog.discover.source`, and `tool.catalog.discovery.transfer`, `.close`,
`.dispose_sources`, and `.dispose_source`. Events 4080/4081 and spans retain
safe run and applicable source identity. Each asynchronous source
discovery/cleanup has its own activity under the coordinating stage.
Count/duration metrics use only closed operation/outcome dimensions; aliases,
publications, and exception messages never enter diagnostics. Cleanup failure
remains failure even when the original discovery was cancelled.

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
