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

## Shared HTTP helpers

`AgentKit.Providers.Http` holds the provider-neutral HTTP mechanics every
first-party HTTP adapter shares so their failure and authentication semantics
cannot drift:

- `HttpStatusFailureKindMapper` is the one canonical HTTP status →
  `ProviderFailureKind` table (401 authentication, 403 authorization, 429
  throttling, 408/504 timeout, 413 invalid request, 529 unavailable, other 4xx
  invalid request, other 5xx unavailable, 1xx–3xx protocol violation). A
  provider passes an explicit override dictionary only for a documented
  provider-specific status such as Bedrock's 424 or Cohere's 498.
- `RetryAfterResolver` turns a `Retry-After` header in either delta-seconds or
  HTTP-date form into a non-negative delay against the injected `TimeProvider`.
- `ProviderRequestIdReader` reads the first non-blank value of a provider-named
  request-id header into a `ProviderRequestId`.
- `ServerSentEventReader` reads a `text/event-stream` body into dispatched
  `ServerSentEvent` values per the WHATWG processing model: UTF-8 decoding that
  survives arbitrary chunk boundaries, a tolerated BOM, CR/LF/CRLF line endings,
  ignored comment lines, `event:`/`data:`/`id:`/`retry:` field parsing with one
  optional leading space stripped, multi-line `data:` joined by `\n`, dispatch
  on a blank line, and a final unterminated event dispatched at end of stream.
  It never interprets the payload; dialect sentinels such as `[DONE]` remain the
  consuming parser's decision.
- `ProviderAuthorizationHeaderFactory` resolves a `ProviderCredential` into the
  closed `ProviderAuthorizationResult` hierarchy: `ProviderAuthorizationGranted`
  (one header name and value, applied to a request through `Apply`) or
  `ProviderAuthorizationDenied` (a typed `Authentication` failure for an expired
  OAuth token or a credential kind the provider does not accept). An OAuth token
  is always sent as `Authorization: Bearer`; each provider's
  `ProviderAuthorizationScheme` constant on its `*ProviderDefaults` type states
  how it accepts a static API key (`BearerToken`, a dedicated header via
  `ForApiKeyHeader`, or `OAuthTokenOnly`). `ProviderAuthorizationGranted`
  redacts its header value from `ToString()` so a granted result can never print
  a key or token.

- `ProviderErrorMessageEvidence` retains the human-readable message from a
  provider's error body as bounded diagnostic evidence under one stable
  `ProviderFailure.Extensions` key (`agentkit.provider.error_message`). Every
  first-party adapter reports a fixed status template in `SafeMessage`, the
  vendor's machine code in `ProviderCode`, and the vendor's prose only through
  this evidence, because error text can echo credentials, tenants, or injected
  instructions and is never safe for users or models by default.

Body-driven vocabularies (Anthropic `error.type`, Google `error.status`, Bedrock
`x-amzn-errortype`) stay in the owning provider package.

## Shared request preflight

`ModelRequestPreflight` is the provider-neutral check every first-party adapter
runs before credential resolution, translation, or I/O. It rejects a request
whose `Context.Model` is not structurally equal to the adapter's configured
descriptor, a request that exposes tools to a model without `SupportsToolCalls`,
and a request that asserts `ParallelToolCalls` against a model without
`SupportsParallelToolCalls`. Each rejection is an `InvalidRequest` failure with
a fixed safe message and no status, provider code, or retry hint, because the
provider was never contacted.

## Known-model catalog

`KnownModelCatalog.Default` is reference data embedded in this assembly: the
context window, output limit, reasoning and vision support, availability, and
list prices vendors publish for the models the first-party adapters can serve.
It is generated, never hand-edited, from the public
[OpenClaw model catalog](https://github.com/openclaw/catalog) (itself assembled
from models.dev and OpenClaw's plugin manifests), keeps only providers with an
AgentKit adapter under AgentKit's own `ProviderId` values, and records the
upstream revision and timestamps in `KnownModelCatalog.Default.Provenance`.

```csharp
if (KnownModelCatalog.Default.TryFind(AnthropicProviderDefaults.ProviderId, new ModelId("claude-sonnet-4-5"), out var known))
{
    var descriptor = known.ToDescriptor(
        new ModelAlias("assistant"),
        AnthropicProviderDefaults.ApiFamily,
        AnthropicProviderDefaults.DefaultCapabilities);
    services.AddAnthropicLlmModel(descriptor.Alias, descriptor.ModelId, descriptor.Capabilities, descriptor.Limits);
    services.AddModelDescriptors(new ModelDescriptorSourceId("app"), [descriptor]);
}
```

`ToDescriptor` overlays the model's published facts (reasoning, vision, tool
calls, limits, prices) on the provider package's protocol baseline (streaming,
system instructions, parallel tool calls, structured output). Provider packages
may add one-call forms such as `AddOpenAIKnownLlmModel`.

The catalog is a generated vendor-feed snapshot, not runtime discovery: a status
of `Available` records what the feed said at import time, and only the
provider's response proves a request works. Refresh it with
`npm run models:import`, which rewrites
[`Resources/known-models.json`](Resources/known-models.json) from the current
upstream file; `KnownModelCatalogTests` checks the result's integrity.

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
