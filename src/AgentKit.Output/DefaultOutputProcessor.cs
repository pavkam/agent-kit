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
    private readonly ImmutableDictionary<string, IOutputValidator> _validatorsByName;
    private readonly JsonSerializerOptions _deserializationOptions;
    private readonly IOutputSchemaEngine _schemaEngine;
    private readonly AgentOutputOptionsSnapshot _options;
    private readonly ILogger<DefaultOutputProcessor> _logger;

    /// <summary>Initializes a new instance of the <see cref="DefaultOutputProcessor"/> class.</summary>
    /// <param name="validators">Every additively registered validator, addressable by its stable name.</param>
    /// <param name="schemaEngine">The selected local schema profile used for preflight and evaluation.</param>
    /// <param name="options">The validated processor options.</param>
    /// <param name="logger">The optional structured logger; a null value disables log publication.</param>
    /// <exception cref="ArgumentNullException"><paramref name="validators"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">Two or more validators share the same <see cref="IOutputValidator.Name"/>.</exception>
    public DefaultOutputProcessor(
        IEnumerable<IOutputValidator> validators,
        IOutputSchemaEngine schemaEngine,
        AgentOutputOptionsSnapshot options,
        ILogger<DefaultOutputProcessor>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(validators);
        ArgumentNullException.ThrowIfNull(schemaEngine);
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
        _schemaEngine = schemaEngine;
        _options = options;
        _deserializationOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            MaxDepth = options.MaximumCandidateDepth,
        };
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
                OutputConfigurationRejected => "configuration_rejected",
                _ => "rejected",
            };

            if (result is OutputRejected or OutputConfigurationRejected)
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

        var schemaPreflight = PreflightDefinition(definition, cancellationToken);
        if (schemaPreflight.Failure is not null)
        {
            return new OutputConfigurationRejected(schemaPreflight.Failure);
        }

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
            return new OutputConfigurationRejected(
                new OutputSchemaConfigurationFailure(
                    OutputSchemaConfigurationFailureKind.MalformedSchema,
                    "The selected structured output mode requires a schema.",
                    []));
        }

        var schemaManifest = schemaPreflight.Manifest;

        var extraction = ExtractCandidate(
            definition.Mode,
            request.Response,
            _options.MaximumCandidateBytes,
            _options.MaximumCandidateDepth,
            cancellationToken);
        if (extraction.Failure is not null)
        {
            return Decide(definition, attempt, extraction.Failure);
        }

        if (extraction.Json is { } candidateJson
            && TryFindCandidateStructuralFailure(candidateJson, cancellationToken) is { } structuralFailure)
        {
            return Decide(definition, attempt, structuralFailure);
        }

        if (extraction.Utf8ByteCount is null
            && !FitsJsonUtf8Limit(extraction.Json!.Value, _options.MaximumCandidateBytes, cancellationToken))
        {
            return Decide(
                definition,
                attempt,
                new OutputValidationFailure(
                    OutputValidationFailureKind.OversizedCandidate,
                    $"The candidate exceeds the maximum of {_options.MaximumCandidateBytes} UTF-8 bytes.",
                    []));
        }

        if (extraction.Json is { } boundedCandidateJson
            && TryFindDuplicateCandidateMember(boundedCandidateJson, cancellationToken) is { } duplicateMemberFailure)
        {
            return Decide(definition, attempt, duplicateMemberFailure);
        }

        if (isStructuredMode && definition.Schema is not null)
        {
            var evaluation = _schemaEngine.Evaluate(
                new OutputSchemaEvaluationRequest(
                    definition.Schema,
                    extraction.Json!.Value,
                    schemaManifest!,
                    CreateSchemaLimits(),
                    CreateCandidateLimits(),
                    Math.Min(definition.ValidationPolicy.MaximumIssues, _options.MaximumValidationIssues)),
                cancellationToken);
            if (evaluation is OutputSchemaEvaluationConfigurationRejected configurationRejected)
            {
                return new OutputConfigurationRejected(configurationRejected.Failure);
            }

            if (evaluation is OutputSchemaCandidateInvalid candidateInvalid)
            {
                return Decide(
                    definition,
                    attempt,
                    new OutputValidationFailure(
                        OutputValidationFailureKind.SchemaValidationFailed,
                        "The candidate did not validate against the declared schema.",
                        candidateInvalid.Issues));
            }
        }

        object? deserialized = null;
        if (definition.RuntimeType is not null && extraction.Json is not null)
        {
            try
            {
                deserialized = JsonSerializer.Deserialize(
                    JsonSerializer.SerializeToUtf8Bytes(extraction.Json.Value, _deserializationOptions),
                    definition.RuntimeType,
                    _deserializationOptions);
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
        int maximumCandidateDepth,
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
            using var document = JsonDocument.Parse(
                rawText,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = maximumCandidateDepth,
                });
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

    private OutputSchemaProcessingLimits CreateSchemaLimits() =>
        new(_options.MaximumSchemaBytes, _options.MaximumSchemaDepth, _options.MaximumSchemaNodes);

    private OutputSchemaProcessingLimits CreateCandidateLimits() =>
        new(_options.MaximumCandidateBytes, _options.MaximumCandidateDepth, _options.MaximumCandidateNodes);

    private OutputValidationFailure? TryFindCandidateStructuralFailure(
        JsonElement root,
        CancellationToken cancellationToken)
    {
        var nodes = 0;
        var pending = new Stack<(JsonElement Value, int Depth)>();
        pending.Push((root, 1));
        while (pending.TryPop(out var entry))
        {
            cancellationToken.ThrowIfCancellationRequested();
            nodes++;
            if (entry.Depth > _options.MaximumCandidateDepth || nodes > _options.MaximumCandidateNodes)
            {
                return new OutputValidationFailure(
                    OutputValidationFailureKind.OversizedCandidate,
                    "The candidate exceeds the configured structural processing limits.",
                    []);
            }

            if (entry.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in entry.Value.EnumerateObject())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (CannotQueueCandidateNode(entry.Depth + 1, nodes, pending.Count))
                    {
                        return CandidateStructuralLimitFailure();
                    }

                    pending.Push((property.Value, entry.Depth + 1));
                }
            }
            else if (entry.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in entry.Value.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (CannotQueueCandidateNode(entry.Depth + 1, nodes, pending.Count))
                    {
                        return CandidateStructuralLimitFailure();
                    }

                    pending.Push((item, entry.Depth + 1));
                }
            }
        }

        return null;
    }

    /// <summary>Finds duplicate object members after the candidate's aggregate byte limit has been enforced.</summary>
    /// <param name="root">The structurally bounded JSON candidate to inspect.</param>
    /// <param name="cancellationToken">The token observed before each value and object member.</param>
    /// <returns>A malformed-JSON failure for the first duplicate member; otherwise, <see langword="null"/>.</returns>
    /// <remarks>
    /// The candidate size check runs first so the per-object name sets remain
    /// bounded by accepted candidate content rather than attacker-controlled
    /// raw input width.
    /// </remarks>
    private static OutputValidationFailure? TryFindDuplicateCandidateMember(
        JsonElement root,
        CancellationToken cancellationToken)
    {
        var pending = new Stack<JsonElement>();
        pending.Push(root);
        while (pending.TryPop(out var value))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in value.EnumerateObject())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!names.Add(property.Name))
                    {
                        return new OutputValidationFailure(
                            OutputValidationFailureKind.MalformedJson,
                            "The candidate contains a duplicate object member.",
                            []);
                    }

                    pending.Push(property.Value);
                }
            }
            else if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in value.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    pending.Push(item);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Determines whether adding one discovered child would exceed a captured
    /// structural limit before retaining that child in the pending traversal.
    /// </summary>
    /// <param name="depth">The one-based depth of the discovered child.</param>
    /// <param name="visitedNodes">The number of nodes already removed from the pending traversal.</param>
    /// <param name="pendingNodes">The number of discovered nodes currently awaiting traversal.</param>
    /// <returns>
    /// <see langword="true"/> when the child must be rejected without queueing;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Every queued value will become one visited node. Checking the aggregate
    /// before the push keeps a wide object or array from allocating a pending
    /// traversal proportional to input width after the node limit is known to
    /// be exceeded.
    /// </remarks>
    private bool CannotQueueCandidateNode(int depth, int visitedNodes, int pendingNodes)
    {
        Debug.Assert(visitedNodes <= _options.MaximumCandidateNodes, "Visited candidate nodes have already passed the configured limit.");
        Debug.Assert(pendingNodes >= 0, "The traversal pending-node count cannot be negative.");

        return depth > _options.MaximumCandidateDepth
            || pendingNodes >= _options.MaximumCandidateNodes - visitedNodes;
    }

    /// <summary>Creates the stable failure returned when candidate structural processing reaches a configured limit.</summary>
    /// <returns>A bounded candidate-size failure that contains no candidate content.</returns>
    private static OutputValidationFailure CandidateStructuralLimitFailure() =>
        new(
            OutputValidationFailureKind.OversizedCandidate,
            "The candidate exceeds the configured structural processing limits.",
            []);

    private static bool FitsJsonUtf8Limit(
        JsonElement value,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        Debug.Assert(maximumBytes > 0, "Captured candidate limits are positive.");
        return BoundedJsonSerializer.TryComputeHash(value, maximumBytes, cancellationToken, out _);
    }

    private SchemaPreflightOutcome PreflightDefinition(OutputDefinition definition, CancellationToken cancellationToken)
    {
        Debug.Assert(definition is not null, "Processing requests contain a validated definition.");
        if (definition.Mode == OutputMode.Text && definition.Schema is not null)
        {
            return new SchemaPreflightOutcome(
                null,
                new OutputSchemaConfigurationFailure(
                    OutputSchemaConfigurationFailureKind.MalformedSchema,
                    "A JSON schema cannot be applied to plain-text output.",
                    []));
        }

        OutputSchemaPreflightManifest? primaryManifest = null;
        if (definition.Schema is not null)
        {
            var result = _schemaEngine.Preflight(
                new OutputSchemaPreflightRequest(definition.Schema, CreateSchemaLimits()),
                cancellationToken);
            if (result is OutputSchemaPreflightRejected rejected)
            {
                return new SchemaPreflightOutcome(null, rejected.Failure);
            }

            primaryManifest = ((OutputSchemaPreflightAccepted) result).Manifest;
        }

        foreach (var alternative in definition.Alternatives)
        {
            var result = _schemaEngine.Preflight(
                new OutputSchemaPreflightRequest(alternative.Schema, CreateSchemaLimits()),
                cancellationToken);
            if (result is OutputSchemaPreflightRejected rejected)
            {
                return new SchemaPreflightOutcome(null, rejected.Failure);
            }
        }

        return new SchemaPreflightOutcome(primaryManifest, null);
    }

    private readonly record struct ExtractionOutcome(
        string? Text,
        JsonElement? Json,
        OutputValidationFailure? Failure,
        int? Utf8ByteCount);

    private readonly record struct SchemaPreflightOutcome(
        OutputSchemaPreflightManifest? Manifest,
        OutputSchemaConfigurationFailure? Failure);
}
