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

public sealed record OutputConfigurationRejected(
    OutputSchemaConfigurationFailure Failure) : OutputProcessingResult;

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

Deserialization into an application type occurs only after bounds, mode,
structural, and canonical schema validation. The resulting typed value remains
provisional until the selected semantic validators accept it. Conversion failure
is a typed validation failure; successful conversion alone never accepts output.
Partial streamed JSON remains provisional. Synthetic output tools are internal
output channels: they do not enter the application tool catalog or permission
pipeline because they cannot perform an external effect. `Prompted` means schema
and bounded formatting instructions are sent as ordinary model context and the
candidate is parsed locally; it is never reported as provider-enforced native
output.

## Schema engine and preflight evidence

JSON Schema dialect identity is the shared `JsonSchemaDialectId` contract in
AgentKit.Abstractions. This intentionally replaces the former public
`OutputSchemaDialectId` name without an implicit conversion or compatibility
alias: output schemas consume the same provider-neutral dialect identity as tool
and other schema owners. Existing configured dialect text and validation
semantics remain unchanged.

`IOutputSchemaEngine` is a separate replaceable contract in
AgentKit.Abstractions. It owns local schema preflight and evaluation;
`IOutputValidator` owns additional application validation. The first-party
structural engine lives in AgentKit.Output. A host can select a fuller dialect
implementation through the same contract without replacing candidate extraction,
repair policy, or publication. No provider SDK or concrete schema library enters
AgentKit.Abstractions.

Plain `Text` definitions express text constraints through semantic validators. A
JSON schema attached to `Text` is rejected as malformed schema configuration for
that definition, both during registry preflight and direct processor calls. It
never becomes an ignored constraint or a model-repair request.

```csharp
namespace AgentKit;

public interface IOutputSchemaEngine
{
    OutputSchemaEngineProfile Profile { get; }

    OutputSchemaPreflightResult Preflight(
        OutputSchemaPreflightRequest request,
        CancellationToken cancellationToken = default);

    OutputSchemaEvaluationResult Evaluate(
        OutputSchemaEvaluationRequest request,
        CancellationToken cancellationToken = default);
}
```

The immutable `OutputSchemaEngineProfile` captures a typed profile identity,
positive version, default dialect, supported dialects, and disjoint assertion
and annotation keyword sets. Sets compare by ordinal membership independently of
registration order. A schema without `$schema` uses that selected profile's
default dialect, recorded explicitly in its preflight manifest. An application
schema version is independent of dialect and engine profile version.

`OutputSchemaPreflightRequest` carries the owned `JsonSchemaDocument` and
`OutputSchemaProcessingLimits`: maximum UTF-8 bytes, JSON depth, and JSON value
nodes. The accepted `OutputSchemaPreflightManifest` captures profile, dialect,
schema fingerprint, limits, and observed depth and node count. Root depth is
one; each JSON value is one node, including values inside annotations and
property maps. Property names contribute bytes, not separate value nodes. The
fingerprint identifies deterministic UTF-8 serialization of the retained schema
under the selected profile, whose writer settings are fixed by its version. It
does not claim to hash the caller's original whitespace or source bytes.

`OutputSchemaEvaluationRequest` carries the owned schema and candidate,
preflight manifest, schema and candidate limits, and a positive issue bound. The
engine revalidates the schema and compares the evidence before evaluation. A
caller-created manifest, matching hash, or profile identifier cannot bypass
preflight. Compare the complete profile, including identity, version, default
dialect, and both capability sets, together with dialect, fingerprint, limits,
and observed counts. A profile revision fixes fingerprint behavior; changing it
requires a new revision. These values carry evidence; they grant no authority.

Preflight returns accepted evidence or a typed configuration failure. Evaluation
returns passed, candidate-invalid issues, or configuration rejection. Failure
kinds distinguish malformed schemas, unsupported dialects or vocabulary,
unresolved references, resource limits, and mismatched preflight evidence.
`OutputConfigurationRejected` is terminal for that definition and never becomes
model repair or spends validation retry budget. A candidate violation of a
supported, valid schema follows the definition's ordinary retry policy.

Engines are immutable, thread-safe services. Their synchronous operations
perform bounded local work, observe cancellation during traversal, and use no
ambient registry or implicit I/O. Optional external schema acquisition occurs
through a separate explicit resolver before these calls. Captured documents own
their JSON storage; limits still apply to traversal and serialization of already
materialized values. Implementations must not depend on call-stack depth to
enforce a configurable nesting bound. Pre-cancellation and cancellation during
work propagate `OperationCanceledException`; they never return partial accepted
evidence or a candidate validation failure.

### First-party structural profile

The default engine profile is `agentkit-structural`, version `1`, with default
and sole supported dialect `urn:agentkit:json-schema:structural:v1`. It makes no
claim to implement an entire JSON Schema standard dialect.

This profile fingerprints retained schema serialization with SHA-256 and a
`sha256:` prefix followed by lowercase hexadecimal output. Serialization is
compact UTF-8, uses the standard JSON encoder, preserves property and array
order and retained numeric spelling, and enables writer validation. It performs
no property sorting or numerical canonicalization. A replacement engine claiming
the same profile revision must reproduce these semantics and the profile's
conformance fixtures.

| Construct              | Supported behavior                                                                                                                              |
| ---------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| Boolean schema         | `true` accepts and `false` rejects within all processing bounds.                                                                                |
| `type`                 | A JSON primitive type name or a nonempty unique array of those names; `integer` uses exact mathematical value, without floating-point rounding. |
| `required`             | An array of unique property names.                                                                                                              |
| `properties`           | An object mapping unique names to supported child schemas.                                                                                      |
| `items`                | One supported child schema applied to each array item.                                                                                          |
| `additionalProperties` | A boolean; `false` rejects object members absent from `properties`.                                                                             |
| Annotations            | `$comment`, `title`, `description`, `default`, `examples`, `deprecated`, `readOnly`, and `writeOnly`, with validated keyword shapes.            |
| `$schema`              | An optional root declaration matching this dialect. Nested declarations are rejected.                                                           |

Every other keyword is rejected, including references, combinators, formats,
patterns, and unsupported numeric or collection assertions. Duplicate schema
keywords and duplicate names in a `properties` map are malformed configuration.
Candidate object members must also have unique names, so different JSON
consumers cannot silently validate and deserialize different values. Annotation
values remain data and are included in processing bounds; they are never
evaluated as child schemas.

Default schema bounds are 262,144 UTF-8 bytes, depth 64, and 4,096 value nodes.
Default candidate bounds are 1,048,576 UTF-8 bytes, depth 64, and 65,536 value
nodes. All are explicit per-processor options. The first-party processor
supports configured depth limits from 1 through 128 and rejects larger settings
before registration. The structural engine rejects requested depth bounds above
128 with a typed resource-limit failure before traversal; neutral contracts
allow other implementations to support different bounds. The processor passes
its exact captured candidate byte limit to extraction, parsing, schema
evaluation, and deserialization; no later stage receives a weaker limit.
Diagnostics obey the smaller of the definition and processor issue limits, use
bounded safe locations, and do not echo raw member names or values into default
repair text or telemetry.

## First-party implementation and DI

```csharp
namespace AgentKit.Output;

internal sealed class OutputProcessor(
    ComponentKey<IOutputProcessor> key,
    IOutputSchemaEngine schemaEngine,
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
    int MaximumSchemaBytes,
    int MaximumSchemaDepth,
    int MaximumSchemaNodes,
    int MaximumCandidateDepth,
    int MaximumCandidateNodes,
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

    public int MaximumSchemaBytes { get; set; } = 262_144;
    public int MaximumSchemaDepth { get; set; } = 64;
    public int MaximumSchemaNodes { get; set; } = 4_096;
    public int MaximumCandidateDepth { get; set; } = 64;
    public int MaximumCandidateNodes { get; set; } = 65_536;
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

        public IServiceCollection ReplaceOutputSchemaEngine<TEngine>(
            ComponentKey<IOutputProcessor> key)
            where TEngine : class, IOutputSchemaEngine =>
            OutputRegistration.ReplaceSchemaEngine<TEngine>(services, key);

        public IServiceCollection AddOutputDefinition(
            ComponentKey<IOutputProcessor> processorKey,
            OutputDefinition definition) =>
            OutputRegistration.AddDefinition(services, processorKey, definition);

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

The processor key owns its definition registry, schema engine, immutable
options, and validator registrations. The registry preflights its primary
schemas and every union alternative through that key's engine before
publication. A configuration failure rejects that profile; a schema supported by
another profile cannot make it valid. Two profiles may register the same
definition identity and version independently. Registering duplicate definitions
within one profile is a conflict, including identical-content duplicates.
Additive registration retains entries; the registry rejects duplicates before
publishing the profile. The first-party registry reports invalid schemas with
`OutputDefinitionConfigurationException`, carrying the neutral typed failure.

`ReplaceOutputSchemaEngine<TEngine>(key)` replaces only that key's singleton
engine; it does not replace the processor or another profile's engine.
`ReplaceOutputProcessor<TProcessor>(key)` replaces only that key's scoped
processor. Repeated `AddAgentOutput(key, configure)` preserves the first
configuration and does not invoke later configuration callbacks. Validate all
supplied keys and option bounds before mutating registrations. Registration
never builds a service provider or performs schema acquisition.

No-key convenience overloads forward to the explicit
`AgentOutputDefaults.ProcessorKey`, whose value is `agentkit-default-output`.
Unkeyed compatibility resolutions alias only that key. Runtime agent selection
must still name a processor key; it never picks the last registration or falls
back to this convenience profile when a selected key is missing. A registry or
processor factory resolves only collaborators registered under its fixed key. It
must not instantiate another profile's validators or retain the mutable service
collection to perform later selection.

The facade's composition validation requires exactly one selected processor,
schema engine, and definition resolver for each runnable selection, and
validates their declared service dependencies without constructor cycles.
Resolving a feature package's registry alone does not prove that facade
validation, request capture, provider mode negotiation, output retry accounting,
or final publication is wired. Conformance tests cover the component boundary;
composed run tests must prove invalid definitions fail before provider I/O and
rejected candidates never become successful run results.

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
