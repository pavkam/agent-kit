# Structured output

**Role:** Resolve an output contract, validate the terminal model candidate, and
return a typed accept, repair, retry, or rejection decision.

AgentKit.Output owns the first-party output-processing runtime described by the
[structured-output specification](../concepts/structured-output.md). Contracts
live in AgentKit.Abstractions. AgentKit.IO publishes provisional events and the
final result; it does not decide whether model output is valid.

## Boundary and flow

The context assembler includes the selected immutable output definition in the
provider-ready request. The provider adapter translates supported native schema
or synthetic-tool modes but does not claim that the returned value is valid.
After a terminal provider response, the loop asks the selected output processor
to extract and validate the candidate.

The processor returns a decision. It never calls the provider, loop, tool
executor, or output publisher itself. A retry decision contains bounded repair
instructions and budget cost; the loop decides whether another model request is
allowed. That cut prevents output → provider → context → output cycles.

## Normative minimal contract shape

```csharp
namespace AgentKit;

public readonly record struct OutputDefinitionId(string Value);
public readonly record struct OutputDefinitionVersion(string Value);

public enum OutputMode
{
    Text,
    SyntheticTool,
    NativeSchema,
    Prompted,
    Media,
    Union
}

public enum OutputEndStrategy
{
    Graceful,
    Early,
    Exhaustive
}

public sealed record OutputDefinition(
    OutputDefinitionId Id,
    OutputDefinitionVersion Version,
    string Name,
    OutputMode Mode,
    JsonSchemaDocument? Schema,
    Type? RuntimeType,
    ImmutableArray<OutputAlternative> Alternatives,
    ImmutableArray<OutputValidatorReference> Validators,
    OutputValidationPolicy ValidationPolicy,
    OutputRetryPolicy RetryPolicy,
    OutputEndStrategy EndStrategy);

public sealed record OutputProcessingRequest(
    AgentRunView Run,
    OutputDefinition Definition,
    ModelResponse Response,
    int ValidationAttempt,
    BudgetExecutionCapability Budget);

public abstract record OutputProcessingResult;

public sealed record OutputAccepted(
    ValidatedOutput Output,
    OutputValidationManifest Manifest) : OutputProcessingResult;

public sealed record OutputRetryRequired(
    OutputRepairInstruction Repair,
    OutputValidationFailure Failure) : OutputProcessingResult;

public sealed record OutputRejected(OutputValidationFailure Failure)
    : OutputProcessingResult;

public interface IOutputDefinitionResolver
{
    ValueTask<OutputDefinitionResult> ResolveAsync(
        OutputDefinitionRequest request,
        CancellationToken cancellationToken = default);
}

public interface IOutputProcessor
{
    ValueTask<OutputProcessingResult> ProcessAsync(
        OutputProcessingRequest request,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken = default);
}

public interface IOutputValidator
{
    ValueTask<OutputValidationResult> ValidateAsync(
        OutputValidationRequest request,
        CancellationToken cancellationToken = default);
}
```

Deserialization into an application type occurs only after structural, schema,
semantic, size, and mode validation. Partial streamed JSON remains provisional.
Synthetic output tools are internal output channels: they do not enter the
application tool catalog or permission pipeline because they cannot perform an
external effect. `Prompted` means schema and bounded formatting instructions are
sent as ordinary model context and the candidate is parsed locally; it is never
reported as provider-enforced native output.

## First-party implementation and DI

```csharp
namespace AgentKit.Output;

internal sealed class OutputProcessor(
    ComponentKey<IOutputProcessor> key,
    IOutputCandidateExtractor extractor,
    IOutputValidatorCatalog validators,
    IOutputRepairPolicy repairPolicy,
    IOutputDeserializer deserializer,
    IHookDispatcher hooks,
    TimeProvider timeProvider,
    AgentOutputOptionsSnapshot options) : IOutputProcessor
{
}

internal sealed record AgentOutputOptionsSnapshot(
    int MaximumCandidateBytes,
    int MaximumValidationIssues,
    int MaximumRepairAttempts,
    bool RequireSchemaForStructuredModes,
    bool AllowProviderModeDowngrade);

public sealed class AgentOutputOptions
{
    public int MaximumCandidateBytes { get; set; } = 1_048_576;
    public int MaximumValidationIssues { get; set; } = 64;
    public int MaximumRepairAttempts { get; set; } = 2;
    public bool RequireSchemaForStructuredModes { get; set; } = true;
    public bool AllowProviderModeDowngrade { get; set; }
}

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAgentOutput(
            ComponentKey<IOutputProcessor> key,
            Action<AgentOutputOptions>? configure = null) =>
            OutputRegistration.AddDefault(services, key, configure);

        public IServiceCollection ReplaceOutputProcessor<TProcessor>(
            ComponentKey<IOutputProcessor> key)
            where TProcessor : class, IOutputProcessor =>
            OutputRegistration.ReplaceProcessor<TProcessor>(services, key);

        public IServiceCollection AddOutputValidator<TValidator>(
            ComponentKey<IOutputProcessor> processorKey,
            OutputValidatorRegistration registration)
            where TValidator : class, IOutputValidator =>
            OutputRegistration.AddValidator<TValidator>(
                services,
                processorKey,
                registration);

        public IServiceCollection ReplaceOutputValidator<TValidator>(
            ComponentKey<IOutputProcessor> processorKey,
            OutputValidatorRegistration registration)
            where TValidator : class, IOutputValidator =>
            OutputRegistration.ReplaceValidator<TValidator>(
                services,
                processorKey,
                registration);
    }
}
```

Processors are keyed and run-scoped. Definitions and stateless validators may be
singleton. Validators are additive and deterministically ordered. A model or
provider capability profile selects a mode before send; a provider downgrade is
explicit and revalidated against the output definition.

`AddAgentOutput` is idempotent for one processor key and captures validated
mutable binding options into one immutable `AgentOutputOptionsSnapshot` for the
run-scoped processor. It `TryAdd`s the candidate extractor, validator catalog,
repair policy, deserializer, and safe structural/schema validators for that key.
Validators are additive only within that key; duplicate identities or ordering
cycles fail build unless `ReplaceOutputValidator` names that same identity. The
options snapshot contains only bounded processing mechanics; it never captures a
provider credential, mutable schema, runtime `Type`, run state, or hook lease.
The defaults bound candidate size, validation diagnostics, and repair attempts,
require a schema for structured modes, and reject provider mode downgrade unless
explicitly enabled. No provider, model, schema, output type, or repair authority
is invented.

## Failure, budgets, and hooks

Validation retries reserve from a dedicated child budget and never consume the
tool retry budget. Malformed schema, ambiguous union, unsupported dialect,
oversized candidate, extraction conflict, deserialization failure, exhausted
retry budget, and provider capability mismatch are typed results.

Hooks may tighten validation, redact safe diagnostics, or replace bounded repair
text through dedicated event arguments. They cannot accept an invalid candidate,
change the declared application type, invoke tools, or turn exhausted limits
into success.

`HookDispatchContext` is supplied separately by the live run and is never
persisted in `OutputProcessingRequest`, manifests, retry instructions, or final
results. Processing without a live hook lease passes `null`; the processor does
not resolve or fabricate one.

## Related architecture

- [Context](context.md)
- [Agent runtime](agent-runtime.md)
- [Input and output](input-and-output.md)
- [Model and embedding providers](model-and-embedding-providers.md)
- [Budgets and limits](budgets.md)
