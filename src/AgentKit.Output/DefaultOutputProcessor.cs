// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output;

using System.Diagnostics;

/// <summary>
/// The default <see cref="IOutputProcessor"/>: extracts a candidate from a
/// terminal model response, validates it, and returns an accept, retry, or
/// reject decision.
/// </summary>
/// <remarks>
/// <para>
/// See <see cref="IOutputProcessor"/> for the reduced-scope rationale
/// shared by every implementation of this contract. This implementation
/// additionally consolidates candidate extraction, structural schema
/// validation, deserialization, and repair-instruction construction into
/// one class rather than the separately injectable
/// extractor/validator-catalog/repair-policy/deserializer collaborators the
/// full architecture describes: none of those sub-steps are part of this
/// reduced package's public, independently replaceable surface (only
/// <see cref="IOutputProcessor"/> and <see cref="IOutputValidator"/> are),
/// so one cohesive, thoroughly tested class is more tractable than an
/// internal multi-interface pipeline with no external replaceability
/// benefit.
/// </para>
/// <para>
/// Validation follows the documented pipeline order: mode support, schema
/// presence, candidate extraction, candidate size, structural schema
/// validation, runtime-type deserialization, then every named validator the
/// definition selects, in order.
/// </para>
/// <para>
/// Runtime-type deserialization uses case-insensitive property matching,
/// since a model-produced JSON candidate's casing convention is not
/// guaranteed to match the declared CLR type's PascalCase property names.
/// </para>
/// </remarks>
internal sealed class DefaultOutputProcessor: IOutputProcessor
{
    private static readonly JsonSerializerOptions _deserializationOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ImmutableDictionary<string, IOutputValidator> _validatorsByName;
    private readonly AgentOutputOptionsSnapshot _options;
    private readonly ILogger<DefaultOutputProcessor> _logger;

    /// <summary>Initializes a new instance of the <see cref="DefaultOutputProcessor"/> class.</summary>
    /// <param name="validators">Every additively registered validator, addressable by its stable name.</param>
    /// <param name="options">The validated processor options.</param>
    /// <param name="logger">The optional structured logger; a null value disables log publication.</param>
    /// <exception cref="ArgumentNullException"><paramref name="validators"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">Two or more validators share the same <see cref="IOutputValidator.Name"/>.</exception>
    public DefaultOutputProcessor(
        IEnumerable<IOutputValidator> validators,
        AgentOutputOptionsSnapshot options,
        ILogger<DefaultOutputProcessor>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(validators);
        ArgumentNullException.ThrowIfNull(options);

        var builder = ImmutableDictionary.CreateBuilder<string, IOutputValidator>();
        foreach (var validator in validators)
        {
            if (!builder.TryAdd(validator.Name, validator))
            {
                throw new ArgumentException(
                    $"More than one validator is registered with the name '{validator.Name}'.", nameof(validators));
            }
        }

        _validatorsByName = builder.ToImmutable();
        _options = options;
        _logger = logger ?? NullLogger<DefaultOutputProcessor>.Instance;
    }

    /// <inheritdoc/>
    public async ValueTask<OutputProcessingResult> ProcessAsync(
        OutputProcessingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var definition = request.Definition;
        using var activity = AgentKitDiagnostics.Activities.StartActivity(AgentKitActivityNames.OutputValidate);
        _ = activity?.SetTag(AgentKitTagNames.OutputDefinitionId, definition.Id.ToString());
        _ = activity?.SetTag(AgentKitTagNames.OutputMode, definition.Mode.ToString());
        _ = activity?.SetTag(AgentKitTagNames.OutputValidationAttempt, request.ValidationAttempt);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await ProcessCoreAsync(request, cancellationToken).ConfigureAwait(false);
            var outcome = result switch
            {
                OutputAccepted => "accepted",
                OutputRetryRequired => "retry_required",
                _ => "rejected",
            };

            if (result is OutputRejected)
            {
                activity.SetFailed(outcome, result.GetType().Name);
            }
            else
            {
                activity.SetSuccessful(outcome);
            }

            OutputLog.Completed(_logger, definition.Id, definition.Mode, request.ValidationAttempt, outcome);
            OutputMetrics.Record(outcome, definition.Mode);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", nameof(OperationCanceledException));
            OutputLog.Cancelled(_logger, definition.Id, definition.Mode, request.ValidationAttempt);
            OutputMetrics.Record("cancelled", definition.Mode);
            throw;
        }
        catch (Exception exception)
        {
            activity.SetFailed("failed", exception.GetType().FullName ?? exception.GetType().Name);
            OutputLog.Failed(
                _logger,
                definition.Id,
                definition.Mode,
                request.ValidationAttempt,
                exception.GetType().FullName ?? exception.GetType().Name);
            OutputMetrics.Record("failed", definition.Mode);
            throw;
        }
    }

    private async ValueTask<OutputProcessingResult> ProcessCoreAsync(
        OutputProcessingRequest request, CancellationToken cancellationToken)
    {

        var definition = request.Definition;
        var attempt = request.ValidationAttempt;

        if (definition.Mode is OutputMode.SyntheticTool or OutputMode.Media or OutputMode.Union)
        {
            return Decide(
                definition,
                attempt,
                new OutputValidationFailure(
                    OutputValidationFailureKind.UnsupportedMode,
                    $"Output mode '{definition.Mode}' is not yet supported by this processor.",
                    []));
        }

        var isStructuredMode = definition.Mode is OutputMode.NativeSchema or OutputMode.Prompted;

        if (isStructuredMode && definition.Schema is null && _options.RequireSchemaForStructuredModes)
        {
            return Decide(
                definition,
                attempt,
                new OutputValidationFailure(
                    OutputValidationFailureKind.MissingSchema,
                    $"Output definition '{definition.Id}' requires a schema for mode '{definition.Mode}'.",
                    []));
        }

        var extraction = ExtractCandidate(
            definition.Mode,
            request.Response,
            _options.MaximumCandidateBytes,
            cancellationToken);
        if (extraction.Failure is not null)
        {
            return Decide(definition, attempt, extraction.Failure);
        }

        var candidateBytes = extraction.Utf8ByteCount
            ?? Encoding.UTF8.GetByteCount(extraction.Json!.Value.GetRawText());

        if (candidateBytes > _options.MaximumCandidateBytes)
        {
            return Decide(
                definition,
                attempt,
                new OutputValidationFailure(
                    OutputValidationFailureKind.OversizedCandidate,
                    $"The candidate is {candidateBytes} bytes, exceeding the maximum of " +
                        $"{_options.MaximumCandidateBytes}.",
                    []));
        }

        if (isStructuredMode && definition.Schema is not null)
        {
            var schemaIssues = StructuralJsonSchemaValidator.Validate(extraction.Json!.Value, definition.Schema.Schema);
            if (!schemaIssues.IsEmpty)
            {
                return Decide(
                    definition,
                    attempt,
                    new OutputValidationFailure(
                        OutputValidationFailureKind.SchemaValidationFailed,
                        "The candidate did not validate against the declared schema.",
                        Bound(schemaIssues, _options.MaximumValidationIssues)));
            }
        }

        object? deserialized = null;
        if (definition.RuntimeType is not null && extraction.Json is not null)
        {
            try
            {
                deserialized = JsonSerializer.Deserialize(
                    extraction.Json.Value.GetRawText(), definition.RuntimeType, _deserializationOptions);
            }
            catch (JsonException exception)
            {
                return Decide(
                    definition,
                    attempt,
                    new OutputValidationFailure(
                        OutputValidationFailureKind.DeserializationFailed,
                        "The candidate could not be deserialized into the declared runtime type.",
                        [new OutputValidationIssue("deserialization-error", exception.Message, null)]));
            }
        }

        var candidate = new ValidatedOutput(definition.Mode, extraction.Text, extraction.Json, deserialized);

        var validatorFailure = await RunValidatorsAsync(definition, candidate, attempt, cancellationToken).ConfigureAwait(false);
        if (validatorFailure is not null)
        {
            return Decide(definition, attempt, validatorFailure);
        }

        var manifest = new OutputValidationManifest(definition.Id, definition.Mode, attempt, []);
        return new OutputAccepted(candidate, manifest);
    }

    private async Task<OutputValidationFailure?> RunValidatorsAsync(
        OutputDefinition definition, ValidatedOutput candidate, int attempt, CancellationToken cancellationToken)
    {
        var issues = ImmutableArray.CreateBuilder<OutputValidationIssue>();
        var maximumIssues = Math.Min(definition.ValidationPolicy.MaximumIssues, _options.MaximumValidationIssues);

        foreach (var reference in definition.Validators)
        {
            if (!_validatorsByName.TryGetValue(reference.Name, out var validator))
            {
                return new OutputValidationFailure(
                    OutputValidationFailureKind.ValidatorNotFound,
                    $"No validator named '{reference.Name}' is registered.",
                    []);
            }

            var result = await validator.ValidateAsync(
                new OutputValidationRequest(definition, candidate, attempt), cancellationToken).ConfigureAwait(false);

            if (result is OutputValidationIssuesFound issuesFound)
            {
                var remaining = maximumIssues - issues.Count;
                issues.AddRange(issuesFound.Issues.Take(remaining));

                if (definition.ValidationPolicy.FailureMode == OutputValidationFailureMode.RejectOnFirstFailure
                    || issues.Count == maximumIssues)
                {
                    break;
                }
            }
        }

        return issues.Count == 0
            ? null
            : new OutputValidationFailure(
                OutputValidationFailureKind.ValidatorFailed,
                "One or more output validators rejected the candidate.",
                issues.ToImmutable());
    }

    private OutputProcessingResult Decide(OutputDefinition definition, int attempt, OutputValidationFailure failure)
    {
        var maximumIssues = Math.Min(definition.ValidationPolicy.MaximumIssues, _options.MaximumValidationIssues);
        var boundedFailure = failure.Issues.Length > maximumIssues
            ? new OutputValidationFailure(
                failure.Kind,
                failure.SafeMessage,
                Bound(failure.Issues, maximumIssues))
            : failure;
        var allowedAttempts = Math.Min(definition.RetryPolicy.MaximumAttempts, _options.MaximumRepairAttempts);

        if (attempt > allowedAttempts)
        {
            return new OutputRejected(boundedFailure);
        }

        var detail = boundedFailure.Issues.IsEmpty
            ? boundedFailure.SafeMessage
            : string.Join(' ', boundedFailure.Issues.Select(static issue => issue.SafeMessage));

        var repair = new OutputRepairInstruction(
            $"The previous output was rejected: {detail} Correct the output and respond again.");

        return new OutputRetryRequired(repair, boundedFailure);
    }

    private static ExtractionOutcome ExtractCandidate(
        OutputMode mode,
        ModelResponse response,
        int maximumCandidateBytes,
        CancellationToken cancellationToken)
    {
        if (mode == OutputMode.Text)
        {
            return ExtractTextWithinLimit(response, maximumCandidateBytes, cancellationToken);
        }

        var structured = response.Parts.OfType<StructuredDataPart>().FirstOrDefault();
        if (structured is not null)
        {
            return new ExtractionOutcome(null, structured.Value, null, null);
        }

        var textExtraction = ExtractTextWithinLimit(response, maximumCandidateBytes, cancellationToken);
        if (textExtraction.Failure is not null)
        {
            return textExtraction;
        }

        var rawText = textExtraction.Text;
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return new ExtractionOutcome(
                null,
                null,
                new OutputValidationFailure(
                    OutputValidationFailureKind.MalformedJson,
                    "No structured data or text content was found to parse as JSON.",
                    []),
                textExtraction.Utf8ByteCount);
        }

        try
        {
            using var document = JsonDocument.Parse(rawText);
            return new ExtractionOutcome(null, document.RootElement.Clone(), null, textExtraction.Utf8ByteCount);
        }
        catch (JsonException exception)
        {
            return new ExtractionOutcome(
                null,
                null,
                new OutputValidationFailure(
                    OutputValidationFailureKind.MalformedJson,
                    "The candidate text could not be parsed as JSON.",
                    [new OutputValidationIssue("json-parse-error", exception.Message, null)]),
                textExtraction.Utf8ByteCount);
        }
    }

    private static ExtractionOutcome ExtractTextWithinLimit(
        ModelResponse response,
        int maximumCandidateBytes,
        CancellationToken cancellationToken)
    {
        Debug.Assert(response is not null, "Candidate extraction requires a terminal model response.");
        Debug.Assert(maximumCandidateBytes > 0, "The captured candidate byte limit must be positive.");

        var encoder = Encoding.UTF8.GetEncoder();
        var builder = new StringBuilder();
        var byteCount = 0;
        Span<byte> byteBuffer = stackalloc byte[256];

        foreach (var part in response.Parts.OfType<TextPart>())
        {
            var remaining = part.Text.AsSpan();
            while (!remaining.IsEmpty)
            {
                cancellationToken.ThrowIfCancellationRequested();
                encoder.Convert(
                    remaining,
                    byteBuffer,
                    flush: false,
                    out var charsUsed,
                    out var bytesUsed,
                    out _);

                if (bytesUsed > maximumCandidateBytes - byteCount)
                {
                    return OversizedText(maximumCandidateBytes);
                }

                byteCount += bytesUsed;
                remaining = remaining[charsUsed..];
            }

            _ = builder.Append(part.Text);
        }

        cancellationToken.ThrowIfCancellationRequested();
        encoder.Convert(
            [],
            byteBuffer,
            flush: true,
            out _,
            out var finalBytes,
            out _);

        if (finalBytes > maximumCandidateBytes - byteCount)
        {
            return OversizedText(maximumCandidateBytes);
        }

        byteCount += finalBytes;
        return new ExtractionOutcome(builder.ToString(), null, null, byteCount);
    }

    private static ExtractionOutcome OversizedText(int maximumCandidateBytes) =>
        new(
            null,
            null,
            new OutputValidationFailure(
                OutputValidationFailureKind.OversizedCandidate,
                $"The text candidate exceeds the maximum of {maximumCandidateBytes} UTF-8 bytes.",
                []),
            null);

    private static ImmutableArray<OutputValidationIssue> Bound(ImmutableArray<OutputValidationIssue> issues, int maximum) =>
        issues.Length > maximum ? issues[..maximum] : issues;

    private readonly record struct ExtractionOutcome(
        string? Text,
        JsonElement? Json,
        OutputValidationFailure? Failure,
        int? Utf8ByteCount);
}
