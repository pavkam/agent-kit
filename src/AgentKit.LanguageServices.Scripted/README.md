# AgentKit.LanguageServices.Scripted

Supply deterministic language-intelligence responses for tests and replay.

Use scripted scenarios to exercise language tools without an editor server or
external analysis process. This package is a testable backend, not a live
language-server integration.

## Use this project

Start with `AddScriptedLanguageIntelligence` in
[ServiceExtensions.cs](ServiceExtensions.cs). Read the overloads and XML
documentation for required collaborators, lifetimes, and duplicate-registration
behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable component
example, follow [Getting started](../../docs/getting-started.md). Complete
engine composition is described in the
[composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Tools.Language](../AgentKit.Tools.Language/README.md) — request
  bounded language-intelligence operations through a selected service.
- [AgentKit.Processes.Scripted](../AgentKit.Processes.Scripted/README.md) —
  simulate process resolution and execution with deterministic scenarios.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.LanguageServices.Scripted.Tests](../../tests/AgentKit.LanguageServices.Scripted.Tests/README.md)
  — focused behavior and registration tests.
- [Component specification](../../docs/architecture/tools.md) — intended
  ownership and contracts.
- [Coding-harness tools](../../docs/profiles/coding-harness/coding-harness-built-in-tools.md)
  — the application profile for these features.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
