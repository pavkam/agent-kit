# AgentKit.Providers

Catalog configured models, validate capabilities, and select or resolve model
implementations.

Use this package with one or more concrete provider adapters. Descriptor
aliases, implementations, and endpoint or credential profiles are explicit
registrations; the catalog invents no default model.

## Use this project

Start with `AddAgentProviders`, `AddModelDescriptors`,
`AddModelDescriptorSource` in [ServiceExtensions.cs](ServiceExtensions.cs). Read
the overloads and XML documentation for required collaborators, lifetimes, and
duplicate-registration behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

Capabilities belong to the configured operation and model. A provider name or
compatible wire format does not imply support for every feature.

## Related projects

- [AgentKit.Providers.OpenAI](../AgentKit.Providers.OpenAI/README.md) — connect
  AgentKit conversational models to OpenAI through Chat Completions.
- [AgentKit.Providers.Anthropic](../AgentKit.Providers.Anthropic/README.md) —
  connect AgentKit conversational models to Anthropic Claude through Messages.
- [AgentKit.Loop](../AgentKit.Loop/README.md) — coordinate turns, context
  preparation, model attempts, tool calls, and terminal outcomes.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Providers.Tests](../../tests/AgentKit.Providers.Tests/README.md) —
  focused behavior and registration tests.
- [Component specification](../../docs/architecture/model-and-embedding-providers.md)
  — intended ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
