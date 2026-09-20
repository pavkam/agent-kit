# WS7: Provider runtime and semantic operations

Goal: the loop executes model requests through `IModelRequestExecutor`
(same-model retry, ordered fallback, taxonomy passthrough), providers bind
independently keyed endpoint and credential profiles through a runtime selector
before every attempt, embedding and reranking gain their own selector/executor
pairs, and every provider leaf is instrumented.

Owning documents:
[Model and embedding providers](../architecture/model-and-embedding-providers.md),
[Provider request pipeline](../concepts/provider-request-pipeline.md).

## Progress

- [x] WS7-C1 `IModelRequestExecutor` contract family
- [ ] WS7-C2 `DefaultModelRequestExecutor` and runtime options
- [ ] WS7-C3 loop consumes the executor
- [ ] WS7-C4 profile runtime selector and lease
- [ ] WS7-C5a `ModelDescriptor.Binding`, OpenAI and OpenAICompatible bind
- [ ] WS7-C5b remaining leaves bind
- [ ] WS7-C6 embedding selector and executor
- [ ] WS7-C7 `EmbeddingSpaceIdentity` growth
- [ ] WS7-C8a rerank contracts and defaults
- [ ] WS7-C8b Cohere reranker
- [ ] WS7-C8c OpenRouter reranker
- [ ] WS7-C9a provider observability names and OpenAICompatible base
- [ ] WS7-C9b six native leaves instrumented
- [ ] WS7-C10 validator

## Verified current state

| Item                                                                                                                                                                                                | State                                 | Evidence                                                                                                                                                                                   |
| --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `IModelRequestExecutor`, `ModelExecutionRequest/Result`, `ModelFallbackRequired`, `DefaultModelRequestExecutor`                                                                                     | MISSING                               | only a `<c>` remark at `Abstractions/Loop/AgentRunServices.cs:15`                                                                                                                          |
| `IProviderProfileRuntimeSelector`, `IProviderProfileRuntimeLease`                                                                                                                                   | MISSING                               | –                                                                                                                                                                                          |
| `ProviderOperationBinding`, endpoint/credential profile references, snapshots, keys, versions, `ProviderCredentialSourceKey`, `ProviderServiceSurfaceId`, `ProviderEndpointId`, `ProviderAccountId` | EXISTS-UNWIRED                        | `Abstractions/Providers/`; zero production callers outside Abstractions                                                                                                                    |
| `ProtectedSemanticOperationContext`                                                                                                                                                                 | EXISTS-UNWIRED                        | `Abstractions/Providers/ProtectedSemanticOperationContext.cs:58-78`                                                                                                                        |
| `ModelDescriptor`                                                                                                                                                                                   | EXISTS-AS-REDUCED-STAND-IN            | no `ServiceSurface`, `EndpointId`, `Binding`, `DescriptorRevision`, `Compatibility` (`ModelDescriptor.cs:73-100` vs spec `:301-315`); 17 src / 24 test ctor sites                          |
| `IProviderCredentialSource.GetCredentialAsync(ProviderId)`                                                                                                                                          | EXISTS-AND-USED, reduced              | spec `ResolveAsync(request)` (`:439-446`); changing it breaks 14 leaves and 16 test projects                                                                                               |
| resilience pipeline                                                                                                                                                                                 | MISSING                               | `Microsoft.Extensions.Http.Resilience` is not in `Directory.Packages.props`                                                                                                                |
| same-model retry                                                                                                                                                                                    | MISSING                               | `DefaultAgentLoop.cs:830-837` calls the adapter once                                                                                                                                       |
| `ModelFallbackPolicy.OrderedCandidates`                                                                                                                                                             | selection-time only                   | `DefaultModelSelector.cs:46`                                                                                                                                                               |
| embedding/rerank selectors, executors, `IReranker`, `Rerank*`                                                                                                                                       | MISSING                               | –                                                                                                                                                                                          |
| `IEmbeddingModel`, `IEmbeddingModelResolver`, embedding request/result types                                                                                                                        | EXISTS, no consumer outside Providers | 5 implementers                                                                                                                                                                             |
| `EmbeddingSpaceIdentity`                                                                                                                                                                            | EXISTS-AS-REDUCED (5 fields)          | vs spec `:1010-1024`                                                                                                                                                                       |
| leaf observability                                                                                                                                                                                  | MISSING                               | zero `LoggerMessage`/`StartActivity` in OpenAICompatible, Anthropic, OpenAI, Cohere; `ProviderLog`/`ProviderMetrics` are internal to `AgentKit.Providers` and cover catalog/selection only |

`ILlmModel` implementers (7 classes, 15 leaves): `OpenAICompatibleLlmModelBase`
(base for OpenAI, AzureOpenAI, Groq, Ollama, OpenRouter, DeepSeek, ZAI, XAI,
MoonshotKimi), Anthropic, AwsBedrock, Cohere, GoogleGemini, GoogleVertexAI,
MistralAI. The executor wraps `ILlmModel`, so none change for C1–C3; profile
binding (C5) touches all 14 leaf `ServiceExtensions` and 7 model classes;
reranking adds new classes in Cohere and OpenRouter only.

Test doubles: `ILlmModel` (`Loop.Tests/FakeLlmModel.cs`,
`Test.Shared/ScriptedLlmModel.cs`, inline ×5); `ILlmModelResolver`
(`FakeModelRuntime.cs:67`, `AliasLlmModelResolver.cs`, ×3); `IModelSelector`
(×4); `IModelCatalog` (×3); `IEmbeddingModel` (×2); `IModelResponseObserver` 30
files, untouched.

## Hidden prerequisites

1. Decide resilience: add a central package for
   `Microsoft.Extensions.Http.Resilience` (pulls Polly into a behavioral
   runtime) or hand-roll a bounded retry loop with typed options. Recommended:
   hand-roll first.
2. `ModelSelectionRequest` has no exclusion field for fallback reselect; add
   `ExcludedCandidates` (NO-SPEC) or narrow `Policy.Candidates`.
3. `AgentRunServices` has 15 construction sites; append an optional parameter.
4. `AgentDefinition.Components.ModelExecutor` (WS18); key by `LoopKey` interim.
5. `HookDispatchContext` (WS2) and `BudgetExecutionCapability` producer (WS11)
   are nullable slots in C1.
6. `ProviderFailureKind` has no context-overflow kind (WS10-C1).
7. Leaves are separate assemblies; a public observability helper in
   `AgentKit.Providers` or per-leaf duplication (C9a decision).
8. Verify `DocumentId` and `EmbeddingInputId` exist before C8a.

## Spec coverage

| Contract                                                                      | Spec                                                                               |
| ----------------------------------------------------------------------------- | ---------------------------------------------------------------------------------- |
| profile runtime lease and selector, results                                   | `model-and-embedding-providers.md:415-472`                                         |
| `LlmModelRequest(Operation, ModelRequestContext, …)`                          | `:674-679` (shipped uses `LlmRequestContext`; keep it)                             |
| `ModelExecutionRequest/Result` family, `IModelRequestExecutor`                | `:789-825`                                                                         |
| `ProviderRetryPolicy`, `IProviderRetryPolicy`, `SemanticOperationRetryPolicy` | NO-SPEC                                                                            |
| embedding selector family                                                     | `:906-939`; `EmbeddingRequirements` NO-SPEC                                        |
| reranker family, `IReranker`, `Rerank*`, `RerankerDescriptor`                 | `:942-968,1100-1178,331`; `RerankerRequirements`, `SemanticOperationUsage` NO-SPEC |
| `EmbeddingSpaceIdentity` full shape                                           | `:1010-1024`; `EmbeddingNormalization` NO-SPEC                                     |
| default executor ctor shapes; options; profile options; DI                    | `:1218-1258,1375-1583`                                                             |
| `ModelDescriptor` with `Binding`                                              | `:301-315`                                                                         |
| `provider.*` activity and metric names                                        | NO-SPEC                                                                            |

## Chunks

### WS7-C1: `IModelRequestExecutor` contract family

- Depends on: –. Risk: ADDITIVE. Size: M.
- Deliverables: `Abstractions/Providers/IModelRequestExecutor.cs`,
  `ModelExecutionRequest.cs` (uses existing `LlmRequestContext`; nullable
  `BudgetExecutionCapability?` and hook slot), `ModelExecutionResult.cs`,
  `ModelExecutionCompleted.cs`, `ModelFallbackRequired.cs`,
  `ModelExecutionFailed.cs`, `ModelExecutionCancelled.cs`,
  `ProviderRetryPolicy.cs`; guard tests. Snapshot: Abstractions. Record the
  `LlmRequestContext` deviation in the architecture document.
- Landed: `ModelExecutionRequest` uses `LlmRequestContext`. `Budget` and
  `Hooks` are nullable. `ProviderRetryPolicy` stores attempt and delay bounds
  only. No executor implementation yet. The compatibility snapshot is still
  outstanding until the shared solution build is green.

### WS7-C2: `DefaultModelRequestExecutor` and runtime options

- Depends on: C1. Risk: ADDITIVE. Size: M.
- Deliverables: internal `src/AgentKit.Providers/DefaultModelRequestExecutor.cs`
  (over `ILlmModelResolver`, retry only when no observer event was emitted,
  `ModelFallbackRequired` for throttling/unavailable/timeout under
  `OrderedCandidates`), `AgentProviderRuntimeOptions.cs`, snapshot,
  `AddAgentProviders(Action<AgentProviderRuntimeOptions>?)`,
  `ReplaceModelRequestExecutor<T>()`, new log/metric events, activity
  `model.execute`; tests with `ScriptedLlmModel`. Snapshot: Providers.

### WS7-C3: Loop consumes the executor

- Depends on: C2. Risk: DENSE-MODIFY `DefaultAgentLoop.RunTurnAsync` (~L811-857)
  and `ResolveModelAsync` (L570-635); `AgentRunServices` gains optional
  `IModelRequestExecutor? ModelExecutor`. Size: M.
- Deliverables: when present, replaces `llmModel.ExecuteAsync`; on fallback,
  reselect excluding the failed alias, rebuild context, re-issue once per turn;
  `AgentRunServicesFactory` resolves it; legacy path preserved when null.
  Snapshots: Abstractions, Loop.

### WS7-C4: Profile runtime selector and lease

- Depends on: –. Risk: ADDITIVE. Size: M.
- Deliverables: Abstractions `IProviderProfileRuntimeSelector.cs`,
  `IProviderProfileRuntimeLease.cs`, selection results; Providers
  `ProviderEndpointProfileOptions.cs`, `ProviderCredentialProfileOptions.cs`,
  `DefaultProviderProfileRuntimeSelector.cs`, `AddProviderEndpointProfile`,
  `AddProviderCredentialProfile`, `Replace*`; selector resolves the existing
  keyed `IProviderCredentialSource` by `ProviderCredentialSourceKey` without
  changing that interface. Snapshots: Abstractions, Providers.

### WS7-C5a/C5b: `ModelDescriptor.Binding` and leaf binding

- Depends on: C4. Risk: ADDITIVE if
  `ProviderOperationBinding? Binding { get; init; }` defaults to null. Size: S
  (C5a: descriptor, OpenAI, OpenAICompatible base selects a lease before
  credential resolution at `ExecuteAsync` L162) and M (C5b: remaining 12
  leaves). Snapshots: Abstractions plus each leaf.
- Done when: binding is captured before each attempt and never appears in
  `ProviderResponseIdentity`.

### WS7-C6: Embedding selector and executor

- Depends on: C1 pattern. Risk: ADDITIVE. Size: M.
- Deliverables: Abstractions `IEmbeddingModelSelector`,
  `EmbeddingSelectionPolicy`, request/decision/result, `EmbeddingRequirements`,
  `IEmbeddingRequestExecutor`, execution request/result,
  `SemanticFallbackPolicy`, `SemanticOperationRetryPolicy`; Providers
  `DefaultEmbeddingModelSelector`, `DefaultEmbeddingRequestExecutor`, DI; tests
  with a fake `IEmbeddingModel`. Existing embedding request/result shapes are
  kept.

### WS7-C7: `EmbeddingSpaceIdentity` growth

- Depends on: –. Risk: ADDITIVE (init-only defaulted). Size: S.
- Deliverables: `Normalization`, `Truncation`, `EndpointId`, `ServiceSurface`,
  `ModelRevision` properties; `EmbeddingNormalization` enum; 5 leaf populate
  sites; equality tests. Snapshots: Abstractions plus 5 leaves.

### WS7-C8a/b/c: Reranking

- Depends on: C6. Risk: ADDITIVE. Size: M, S, S.
- C8a Abstractions `IReranker`, `RerankerAlias`, `RerankerDescriptor`,
  `RerankRequest`, `RerankDocument`, `RerankResult`, `RerankResponse`,
  `RerankModelRequest/Result`, `IRerankerSelector` and policy/request/decision/
  result, `RerankerRequirements`, `IRerankRequestExecutor` and request/result,
  `IRerankerResolver`; Providers defaults and DI. C8b `CohereReranker` with
  translator and parser and fixtures. C8c `OpenRouterReranker`. Snapshots:
  Abstractions, Providers, Cohere, OpenRouter.

### WS7-C9a/C9b: Leaf instrumentation

- Depends on: –. Risk: ADDITIVE. Size: M each.
- C9a: `AgentKitActivityNames.ProviderSend` and embedding/rerank names,
  `agentkit.provider.request.count/duration` with bounded tags, a public
  `ProviderRequestObservability` helper in `AgentKit.Providers`, wired into
  `OpenAICompatibleLlmModelBase` (covers 9 leaves). C9b: Anthropic, Cohere,
  Gemini, VertexAI, Bedrock, Mistral. Tests assert name, status, tags, and no
  prompt or secret content; disabled listener leaves behavior unchanged.

### WS7-C10: Validator

- Depends on: C2, C4. Risk: DENSE-MODIFY `AgentCompositionValidator.cs:225-300`.
  Size: S.
- Deliverables: singular `IProviderProfileRuntimeSelector`, keyed-or-un-keyed
  `IModelRequestExecutor`, per-definition compatibility pass producing a typed
  diagnostic when no candidate model satisfies `ModelRequirements`.

## Totals

S 3, M 10, L 0 (after splits). Confidence medium: retry-policy shapes and the
fallback exclusion mechanism are NO-SPEC; leaf instrumentation is broad but
mechanical.
