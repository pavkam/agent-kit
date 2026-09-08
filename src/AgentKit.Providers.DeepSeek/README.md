# AgentKit.Providers.DeepSeek

Connect AgentKit conversational models to DeepSeek through its OpenAI-compatible
chat surface.

Use this adapter when your application selects DeepSeek. Configure the endpoint,
credential source, model identity, and capability profile explicitly. The
current registration surface exposes conversational models.

## Use this project

Start with `AddDeepSeek`, `AddDeepSeekLlmModel` in
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

- [AgentKit.Providers](../AgentKit.Providers/README.md) — catalog configured
  models, validate capabilities, and select or resolve model implementations.
- [AgentKit.Abstractions](../AgentKit.Abstractions/README.md) — implement
  AgentKit extensions against provider-neutral contracts and typed domain
  values.
- [AgentKit.Providers.OpenAICompatible](../AgentKit.Providers.OpenAICompatible/README.md)
  — implement shared OpenAI-compatible request translation, transport, and
  streaming mechanics.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Providers.OpenAICompatible](../AgentKit.Providers.OpenAICompatible/README.md).
Other related projects above are composition collaborators, not necessarily
dependencies.

## Tests and reference

- [AgentKit.Providers.DeepSeek.Tests](../../tests/AgentKit.Providers.DeepSeek.Tests/README.md)
  — focused behavior and registration tests.
- [Component specification](../../docs/architecture/model-and-embedding-providers.md)
  — intended ownership and contracts.
- [DeepSeek API reference](../../docs/providers/deepseek.md) — wire behavior and
  capability requirements.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
