# AgentKit.Providers.OpenAI

Connect AgentKit conversational models to OpenAI through Chat Completions.

Use this adapter when your application selects OpenAI. Configure the endpoint,
credential source, model identity, and capability profile explicitly. Embedding
models have a separate registration.

## Use this project

Start with `AddOpenAI`, `AddOpenAILlmModel`, `AddOpenAIEmbeddingModel` in
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

- [AgentKit.Providers.OpenAI.Tests](../../tests/AgentKit.Providers.OpenAI.Tests/README.md)
  — focused behavior and registration tests.
- [Component specification](../../docs/architecture/model-and-embedding-providers.md)
  — intended ownership and contracts.
- [OpenAI API reference](../../docs/providers/openai.md) — wire behavior and
  capability requirements.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
