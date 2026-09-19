# WS8: Structured output completion

Goal: the output contract reaches provider translators (`NativeSchema` as
`response_format` / `responseSchema` / forced tool), the processor negotiates
mode against model capability with an explicit downgrade option,
`SyntheticTool`, `Union`, and `Media` modes extract and validate,
`OutputEndStrategy` is honored, repair attempts reserve budget, and the
processor accepts a hook context.

Owning documents: [Structured output](../architecture/structured-output.md),
[Structured output concept](../concepts/structured-output.md).

## Progress

- [ ] WS8-C1 output contract on the request context
- [ ] WS8-C2 OpenAI-family `response_format`
- [ ] WS8-C3a Gemini and Vertex `responseSchema`
- [ ] WS8-C3b Anthropic and Bedrock forced tool
- [ ] WS8-C3c Cohere and Mistral forced tool
- [ ] WS8-C4a negotiation, downgrade, `SyntheticTool`
- [ ] WS8-C4b `Union`
- [ ] WS8-C4c `Media`
- [ ] WS8-C5 `OutputEndStrategy` in the loop
- [ ] WS8-C6 hook context and `OutputValidating` point
- [ ] WS8-C7 repair attempts reserve budget
- [ ] WS8-C8 Simple `WithOutput<T>` mode options

## Verified current state

| Item                                         | State                      | Evidence                                                                                                                                     |
| -------------------------------------------- | -------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| `NativeSchema` sent to a provider            | MISSING                    | no translator references `response_format`/`json_schema`/`responseSchema`; `LlmRequestContext` has no output field                           |
| `SyntheticTool`, `Media`, `Union`            | EXISTS-AS-REDUCED          | rejected as `UnsupportedMode` at `src/AgentKit.Output/DefaultOutputProcessor.cs:157-166`                                                     |
| `OutputEndStrategy`                          | EXISTS-UNWIRED             | remark `Abstractions/Output/OutputEndStrategy.cs:13-17`; loop never reads it                                                                 |
| `AllowProviderModeDowngrade`                 | EXISTS-UNWIRED             | `AgentOutputOptions.cs:54-59`                                                                                                                |
| repair budget                                | attempt counter            | `DefaultAgentLoop.cs:1130`, `RunTracking.NextAttempt` L2865-2867                                                                             |
| `OutputProcessingRequest`                    | EXISTS-AS-REDUCED-STAND-IN | `OutputProcessingRequest.cs:14-17` quotes the missing `AgentRunView` and `BudgetExecutionCapability`; ctor `(definition, response, attempt)` |
| `IOutputProcessor.ProcessAsync` (2-arg)      | EXISTS-AS-REDUCED          | `IOutputProcessor.cs:9-13` names `HookDispatchContext`                                                                                       |
| `ModelRequirements.RequiresStructuredOutput` | never written              | only reader `DefaultModelCapabilityValidator.cs:62`                                                                                          |
| capability descriptors                       | inaccurate                 | 9 leaves claim `supportsStructuredOutput: true` while sending nothing; `GoogleGeminiProviderDefaults.cs:74` says false                       |
| Simple `WithOutput<T>`                       | hard-codes `Prompted`      | `AgentEngineBuilderExtensions.cs:694-697`                                                                                                    |

Test doubles: `IOutputProcessor` 5 (`Loop.Tests/ScriptedOutputProcessor.cs`,
inline in Conversations, Output, AgentKit tests); `IOutputSchemaEngine`
(`StructuralOutputSchemaEngine`, `PatternOutputSchemaEngine`, inline);
`IOutputValidator` 2; 54 `ProcessAsync(` call sites in tests.

## Hidden prerequisites

1. No field carries the output contract to translators; C1 adds an additive
   nullable `LlmRequestContext.Output` (spec places it on the missing
   `ModelRequestContext`, `context.md:271`).
2. `OpenAICompatibilityProfile` has no structured-output flag; add one in C2.
3. New `OutputSchemaConfigurationFailureKind.ProviderCapabilityMismatch`.
4. C6 waits for WS2 `HookDispatchContext`; ship as a default-interface overload.
5. C7 waits for WS11-C3 and a `BudgetDimensions.OutputRepairs` dimension.
6. Do not add `AgentRunView` (NO-SPEC); carry `Model` and identities on the
   request instead.

## Spec coverage

| Contract                                                                        | Spec                                                        |
| ------------------------------------------------------------------------------- | ----------------------------------------------------------- |
| `OutputProcessingRequest(Run, Definition, Response, ValidationAttempt, Budget)` | `structured-output.md:89-94`; `AgentRunView` NO-SPEC        |
| `IOutputProcessor.ProcessAsync(request, HookDispatchContext?, ct)`              | `:119-125`                                                  |
| processor ctor, options snapshot, options, DI                                   | `:277-362`                                                  |
| provider wire mapping                                                           | NO-SPEC (prose `concepts/structured-output.md:30-33,87-92`) |
| mode negotiation and downgrade                                                  | prose `:365-368`                                            |
| `LlmRequestContext.Output`                                                      | NO-SPEC                                                     |
| repair child budget                                                             | prose `:420-425`                                            |

## Chunks

### WS8-C1: Output contract on the request context

- Depends on: –. Risk: ADDITIVE (init, default null; 29 ctor sites unaffected).
  Size: S.
- Deliverables: `LlmRequestContext.Output : OutputDefinition?`; optional
  `ContextAssemblyRequest.Output`; `DefaultContextAssembler` copies it; loop
  passes `request.Output` (`DefaultAgentLoop.cs:730-758`);
  `RequiresStructuredOutput = definition.Output?.Mode is NativeSchema` in model
  selection. Snapshot: Abstractions.
- Done when: a `NativeSchema` definition with an unsupporting model yields
  `NoCompatibleModel`.

### WS8-C2: OpenAI-family `response_format`

- Depends on: C1. Risk: ADDITIVE (fixtures unchanged when `Output` is null).
  Size: M.
- Deliverables: `OpenAIRequestTranslator` emits
  `response_format: {type: json_schema, json_schema: {name, schema, strict}}`;
  `OpenAICompatibilityProfile.SupportsJsonSchemaResponseFormat`; correct the
  nine leaves' descriptors after verifying primary documentation; fixtures.
  Snapshots: OpenAICompatible plus changed leaves.

### WS8-C3a/b/c: Other providers

- Depends on: C1. Risk: ADDITIVE. Size: S, M, S.
- Gemini/Vertex `responseSchema`/`responseMimeType`; Anthropic/Bedrock and
  Cohere/Mistral forced-tool `SyntheticTool` with parser mapping back to a
  candidate; truthful capability flags; fixtures. A reserved synthetic tool name
  convention is NO-SPEC and must be documented.

### WS8-C4a/b/c: Processor negotiation, `SyntheticTool`, `Union`, `Media`

- Depends on: C1. Risk: DENSE-MODIFY `DefaultOutputProcessor.cs:144-210`,
  `ExtractCandidate` ~L368, `AgentOutputOptions.cs:54-59`. Size: M, M, S.
- Deliverables: `OutputProcessingRequest.Model : ModelDescriptor?` (additive);
  preflight compares mode vs capability, downgrades to `Prompted` when allowed
  else `OutputConfigurationRejected(ProviderCapabilityMismatch)`; `Union`
  iterates `definition.Alternatives` requiring exactly one pass; `Media`
  validates content type; matrix tests. Snapshots: Abstractions, Output.

### WS8-C5: `OutputEndStrategy` in the loop

- Depends on: C4. Risk: DENSE-MODIFY `SettleCompletedAsync` L1040-1062. Size: S.
- Deliverables: `Graceful`, `Early`, `Exhaustive` semantics; remove the remark;
  loop tests with `ScriptedOutputProcessor`.

### WS8-C6: Hook context and `OutputValidating` point

- Depends on: WS2-C2. Risk: ADDITIVE via default-interface overload. Size: M.
- Deliverables: 3-arg `ProcessAsync` overload;
  `AgentHookPoints.OutputValidating` and `OutputValidatingEventArgs`; test
  proves a hook cannot accept an invalid candidate. Snapshot: Abstractions,
  Output.

### WS8-C7: Repair attempts reserve budget

- Depends on: WS11-C3. Risk: DENSE-MODIFY `DefaultAgentLoop.cs:1130`,
  `RunTracking`. Size: S.
- Deliverables: `BudgetDimensions.OutputRepairs`; counter fallback when
  unbudgeted; `OutputProcessingRequest.Budget : BudgetExecutionCapability?`;
  exhaustion settles as a budget limit.

### WS8-C8: Simple `WithOutput<T>` mode options

- Depends on: C4. Risk: ADDITIVE. Size: S.
- Deliverables: `OutputMode` parameter or `SimpleOutputOptions`; `NativeSchema`
  round-trip test. Snapshot: Simple.

## Totals

S 6, M 5. Confidence medium: provider wire mappings are prose-only and each
leaf's true structured-output support must be verified against primary
documentation.
