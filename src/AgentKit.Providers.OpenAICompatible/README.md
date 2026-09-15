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

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

Capabilities belong to the configured operation and model. A provider name or
compatible wire format does not imply support for every feature.

Tool results use the deterministic `agentkit.tool-result.v1` JSON envelope
inside the wire message's `content` string. It retains exact outcome status,
effect certainty, retry advice, failure reason, requested alias, and ordered
text/JSON parts. Empty failure output remains an explicit failure. This is a
projection for the model, not a reconstructed execution record or the
human-facing tool presentation. The profile's positive
`MaximumToolResultCharacters` bound defaults to 262,144; oversized source or
serialized output fails before I/O instead of silently losing evidence. This
intentionally changes the previous unwrapped tool-content mapping.

Runtime notices map to a labeled JSON envelope under the `user` role. They never
acquire system/developer instruction authority. OpenAI permits applications to
choose a tool-output string format, including JSON and error codes; see the
[official function-calling guide](https://developers.openai.com/api/docs/guides/function-calling#formatting-results).

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
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Providers](../AgentKit.Providers/README.md). Other related projects
above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Providers.OpenAICompatible.Tests](../../tests/AgentKit.Providers.OpenAICompatible.Tests/README.md)
  — focused behavior and registration tests.
- [Component specification](../../docs/architecture/model-and-embedding-providers.md)
  — intended ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
