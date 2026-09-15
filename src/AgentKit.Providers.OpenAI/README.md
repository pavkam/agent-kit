# AgentKit.Providers.OpenAI

Connect AgentKit conversational models to OpenAI through Chat Completions.

Use this adapter when your application selects OpenAI. Configure the endpoint,
credential source, model identity, and capability profile explicitly. Embedding
models have a separate registration.

## Use this project

Start with `AddOpenAI`, `AddOpenAIApiKeyCredential`, and
`AddOpenAIKnownLlmModel` in [ServiceExtensions.cs](ServiceExtensions.cs):

```csharp
services.AddAgentProviders();
services.AddOpenAI();
services.AddOpenAIApiKeyCredential(apiKey);
services.AddOpenAIKnownLlmModel(new ModelAlias("assistant"), new ModelId("gpt-4o-mini"));
```

`AddOpenAIKnownLlmModel` registers the adapter and publishes an identical
catalog descriptor whose limits, capabilities, and list prices come from the
[known-model catalog](../AgentKit.Providers/README.md#known-model-catalog). For
a model the catalog does not know, or to override its facts, use
`AddOpenAILlmModel(alias, modelId, capabilities, limits)` and publish a matching
descriptor with `AddModelDescriptors`; the adapter rejects a request whose
selected descriptor differs from its own. `AddOpenAIEmbeddingModel` registers
embedding models independently. Read the overloads and XML documentation for
required collaborators, lifetimes, and duplicate-registration behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

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
