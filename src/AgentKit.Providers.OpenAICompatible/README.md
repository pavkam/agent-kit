# AgentKit.Providers.OpenAICompatible

Implement shared OpenAI-compatible request translation, transport, and streaming
mechanics.

Use this package when building an adapter for that wire family. Applications
normally select a branded provider package so endpoints, credentials, and
supported capabilities have an explicit owner.

## Use this project

Start with `AddOpenAICompatibleProvider` in
[ServiceExtensions.cs](ServiceExtensions.cs). Read the overloads and XML
documentation for required collaborators, lifetimes, and duplicate-registration
behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable component
example, follow [Getting started](../../docs/getting-started.md). Complete
engine composition is described in the
[composition guide](../../docs/guides/composition.md).

Capabilities belong to the configured operation and model. A provider name or
compatible wire format does not imply support for every feature.

## Related projects

- [AgentKit.Providers.OpenAI](../AgentKit.Providers.OpenAI/README.md) — connect
  AgentKit conversational models to OpenAI through Chat Completions.
- [AgentKit.Providers.OpenRouter](../AgentKit.Providers.OpenRouter/README.md) —
  connect AgentKit conversational models to OpenRouter through its
  OpenAI-compatible chat surface.
- [AgentKit.Providers.ZAI](../AgentKit.Providers.ZAI/README.md) — connect
  AgentKit conversational models to Z.ai through its OpenAI-compatible chat
  surface.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Providers.OpenAICompatible.Tests](../../tests/AgentKit.Providers.OpenAICompatible.Tests/README.md)
  — focused behavior and registration tests.
- [Component specification](../../docs/architecture/model-and-embedding-providers.md)
  — intended ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
