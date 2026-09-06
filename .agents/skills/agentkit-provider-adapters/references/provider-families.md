# Provider Families

Use this reference to choose an adapter boundary, not as a frozen copy of a
vendor API. Fetch the current official documentation before implementing
payloads or declaring capabilities.

## AgentKit package map

`AgentKit.Providers` supplies provider-neutral runtime behavior: catalog,
selection, capability validation, and model request execution.
`AgentKit.Providers.OpenAICompatible` supplies reusable protocol-family building
blocks. Neither package is a vendor identity.

Concrete integrations use `AgentKit.Providers.ProviderName` and own their
endpoint, credentials, options, compatibility profile, descriptor discovery, and
service registration. The initial compatible family is:

| Package                         | Shared family                                                                 | Independently registered operations                  |
| ------------------------------- | ----------------------------------------------------------------------------- | ---------------------------------------------------- |
| `AgentKit.Providers.OpenAI`     | OpenAI-compatible Responses, Chat Completions, and embeddings                 | Conversation and embeddings                          |
| `AgentKit.Providers.OpenRouter` | OpenAI-compatible Chat Completions, Responses where supported, and embeddings | Conversation, embeddings, and reranking              |
| `AgentKit.Providers.ZAi`        | OpenAI-compatible Chat Completions subset                                     | Conversation and verified provider-native operations |

Native or cloud-broker packages use their own adapters when compatibility would
discard semantics. Planned names follow the same rule, including
`AgentKit.Providers.Anthropic`, `AgentKit.Providers.GoogleGemini`,
`AgentKit.Providers.AzureOpenAI`, and `AgentKit.Providers.AmazonBedrock`.
Research coverage does not itself commit AgentKit to shipping a package.

## OpenAI and OpenAI-compatible APIs

OpenAI's Responses API models output as typed items and events; function calls
are application-executed tools described by schema. Start with the official
[function calling](https://developers.openai.com/api/docs/guides/function-calling),
[streaming](https://developers.openai.com/api/docs/guides/streaming-responses),
and [embeddings](https://developers.openai.com/api/docs/guides/embeddings)
guides.

Some gateways and local runtimes advertise OpenAI compatibility. Share request,
stream, and error machinery only for the subset proven by tests. Verify, per
provider:

- base URL, authentication, organization/project headers, and model naming;
- Responses versus Chat Completions support;
- content modalities and role mapping;
- tool choice, parallel calls, JSON Schema dialect, and structured outputs;
- streaming framing, event variants, usage placement, and finish reasons; and
- error bodies, rate-limit headers, retries, and cancellation behavior.

Expose compatibility as capabilities. Do not fork AgentKit's provider-neutral
contracts around whichever OpenAI-shaped API was implemented first.

## Native content-block APIs

Anthropic's Messages API uses typed content blocks and distinguishes
application-executed client tools from provider-executed server tools. Consult
the official
[tool-use documentation](https://platform.claude.com/docs/en/agents-and-tools/tool-use/overview)
and current Messages/streaming references. Preserve block identity, tool-use
IDs, stop reasons, thinking/signature data, citations, and server-tool results.

Google Gemini APIs use native content parts/steps and support function calling,
provider tools, streaming, and embeddings through capabilities that evolve
independently. Consult the official
[function-calling](https://ai.google.dev/gemini-api/docs/function-calling),
[streaming](https://ai.google.dev/gemini-api/docs/streaming), and
[embeddings](https://ai.google.dev/gemini-api/docs/embeddings) guides. Do not
flatten provider-executed tools or continuation state into ordinary client tool
calls.

Use a native adapter when translating through an OpenAI-compatible facade would
discard content, lifecycle, safety, or tool semantics.

## Cloud broker APIs

Cloud platforms may expose several model families behind one service API. Amazon
Bedrock Converse, for example, provides a common conversation surface while
retaining model-specific request and response fields. Consult the official
[Converse documentation](https://docs.aws.amazon.com/bedrock/latest/userguide/conversation-inference.html)
and
[tool-use guide](https://docs.aws.amazon.com/bedrock/latest/userguide/tool-use.html).

Treat deployment identity, region, credentials, guardrails, quota, and model
capability as configuration. A broker adapter must not claim the union of every
model's capabilities; describe the selected deployment/model.

Azure-hosted OpenAI and Google Vertex AI often combine a familiar model payload
with platform-specific endpoints, deployment names, identity, policy, and error
behavior. Reuse payload translators where conformance proves it, but keep cloud
transport/authentication packages separate from direct-provider packages.

## Embeddings

Embedding providers are not interchangeable at the vector level. A stored vector
belongs to a provider/model/revision, dimensionality, modality, and
normalization scheme. The embedding contract must make incompatibility
detectable before query time and support explicit re-embedding migrations.

Reranking is a separate semantic operation as well. Do not disguise a reranker
as an embedding model or assume every embedding provider supplies reranking.
