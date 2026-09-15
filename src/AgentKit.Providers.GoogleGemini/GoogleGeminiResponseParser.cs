// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini;

using AgentKit.Providers.GoogleGemini.Wire;
using AgentKit.Providers.Http;

/// <summary>
/// The default <see cref="IGoogleGeminiResponseParser"/>, parsing both
/// buffered and server-sent-events Gemini GenerateContent responses as a
/// typed state machine.
/// </summary>
/// <remarks>
/// Unlike OpenAI-compatible and Anthropic streaming, Gemini's
/// <c>:streamGenerateContent</c> SSE frames each repeat a growing partial
/// <c>GenerateContentResponse</c> rather than emitting small,
/// explicitly-indexed deltas; there is no terminal sentinel line, and the
/// stream is authoritative-complete only once a chunk carries a non-null
/// <c>finishReason</c>. This parser correlates a streamed part with its
/// earlier <see cref="ModelPartStarted"/> event by its ordinal position
/// within <c>candidates[0].content.parts</c>, and treats each chunk's text
/// for an already-open text or thought part as an incremental fragment to
/// append. A <c>functionCall</c> part is always delivered complete in a
/// single chunk, so it is opened, filled, and closed in one step.
/// </remarks>
public sealed class GoogleGeminiResponseParser: IGoogleGeminiResponseParser
{
    private const string _unknownPartTypeName = "unknownPart";

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IIdentifierGenerator<ToolCallId> _toolCallIdGenerator;

    /// <summary>Initializes a new instance of the <see cref="GoogleGeminiResponseParser"/> class.</summary>
    /// <param name="toolCallIdGenerator">
    /// Mints the internal <see cref="ToolCallId"/> assigned to each
    /// <c>functionCall</c> part the provider returns.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="toolCallIdGenerator"/> is null.</exception>
    public GoogleGeminiResponseParser(IIdentifierGenerator<ToolCallId> toolCallIdGenerator)
    {
        ArgumentNullException.ThrowIfNull(toolCallIdGenerator);
        _toolCallIdGenerator = toolCallIdGenerator;
    }

    /// <inheritdoc/>
    public async Task<ModelAttemptResult> ParseBufferedAsync(
        Stream responseBody,
        ProviderResponseParseContext context,
        IModelResponseObserver observer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(responseBody);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(observer);

        var requestId = context.ModelRequestId;
        long sequence = 0;

        await observer.OnEventAsync(new ModelResponseStarted(requestId, sequence++), cancellationToken)
            .ConfigureAwait(false);

        GoogleGeminiGenerateContentResponseDto dto;
        try
        {
            dto = await JsonSerializer
                .DeserializeAsync<GoogleGeminiGenerateContentResponseDto>(responseBody, _serializerOptions, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new JsonException("The response body deserialized to a null value.");
        }
        catch (JsonException exception)
        {
            return await FailAsync(
                observer,
                context,
                sequence,
                ProviderFailureKind.ProtocolViolation,
                "The provider returned a response body that could not be parsed.",
                exception,
                cancellationToken).ConfigureAwait(false);
        }

        var candidate = dto.Candidates?.Count > 0 ? dto.Candidates[0] : null;
        if (candidate is null)
        {
            return await FailAsync(
                observer,
                context,
                sequence,
                dto.PromptFeedback?.BlockReason is { Length: > 0 } blockReason
                    ? ProviderFailureKind.InvalidRequest
                    : ProviderFailureKind.ProtocolViolation,
                dto.PromptFeedback?.BlockReason is { Length: > 0 } reason
                    ? $"The provider blocked the prompt: {reason}."
                    : "The provider returned a response with no candidates.",
                diagnosticCause: null,
                cancellationToken).ConfigureAwait(false);
        }

        var partsDto = candidate.Content?.Parts ?? [];
        var parts = ImmutableArray.CreateBuilder<ContentPart>();

        for (var index = 0; index < partsDto.Count; index++)
        {
            await observer.OnEventAsync(new ModelPartStarted(requestId, sequence++, index), cancellationToken)
                .ConfigureAwait(false);

            var (delta, part) = BuildPart(partsDto[index], _toolCallIdGenerator);
            if (delta is not null)
            {
                await observer.OnEventAsync(new ModelPartDelta(requestId, sequence++, index, delta), cancellationToken)
                    .ConfigureAwait(false);
            }

            await observer.OnEventAsync(new ModelPartCompleted(requestId, sequence++, index, part), cancellationToken)
                .ConfigureAwait(false);
            parts.Add(part);
        }

        ModelUsage usage;
        try
        {
            usage = BuildUsage(dto.UsageMetadata);
        }
        catch (ArgumentException exception)
        {
            return await FailAsync(
                observer,
                context,
                sequence,
                ProviderFailureKind.ProtocolViolation,
                "The provider returned invalid usage evidence.",
                exception,
                cancellationToken,
                parts.ToImmutable()).ConfigureAwait(false);
        }
        if (dto.UsageMetadata is not null)
        {
            await observer.OnEventAsync(new ModelUsageUpdated(requestId, sequence++, usage), cancellationToken)
                .ConfigureAwait(false);
        }

        var response = new ModelResponse(
            requestId,
            context.CreateResponseIdentity(dto.ModelVersion, dto.ResponseId),
            parts.ToImmutable(),
            MapStopReason(candidate.FinishReason, parts.Any(p => p is ToolCallPart)),
            usage,
            ExtensionData.Empty);

        await observer.OnEventAsync(new ModelResponseCompleted(requestId, sequence++, response), cancellationToken)
            .ConfigureAwait(false);

        return new ModelAttemptCompleted(response);
    }

    /// <inheritdoc/>
    public async Task<ModelAttemptResult> ParseStreamingAsync(
        Stream responseBody,
        ProviderResponseParseContext context,
        IModelResponseObserver observer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(responseBody);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(observer);

        var requestId = context.ModelRequestId;
        long sequence = 0;

        await observer.OnEventAsync(new ModelResponseStarted(requestId, sequence++), cancellationToken)
            .ConfigureAwait(false);

        var parts = new List<PartAccumulator>();
        string? resolvedModel = null;
        string? responseId = null;
        GoogleGeminiUsageMetadataDto? usage = null;
        var usageIsFinal = false;
        string? finishReason = null;
        string? blockReason = null;
        var sawAnyCandidate = false;

        await foreach (var streamEvent in ServerSentEventReader.ReadAsync(responseBody, cancellationToken).ConfigureAwait(false))
        {
            var payload = streamEvent.Data;
            if (payload.Length == 0)
            {
                continue;
            }

            GoogleGeminiGenerateContentResponseDto chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<GoogleGeminiGenerateContentResponseDto>(payload, _serializerOptions)
                    ?? throw new JsonException("The chunk payload deserialized to a null value.");
            }
            catch (JsonException exception)
            {
                return await FailAsync(
                    observer,
                    context,
                    sequence,
                    ProviderFailureKind.ProtocolViolation,
                    "The provider returned a malformed streaming chunk.",
                    exception,
                    cancellationToken,
                    BuildPartialParts(parts),
                    TryBuildRetainedUsage(usage, usageIsFinal)).ConfigureAwait(false);
            }

            resolvedModel ??= chunk.ModelVersion;
            responseId ??= chunk.ResponseId;
            blockReason ??= chunk.PromptFeedback?.BlockReason;

            var candidate = chunk.Candidates?.Count > 0 ? chunk.Candidates[0] : null;
            if (chunk.UsageMetadata is not null)
            {
                usage = chunk.UsageMetadata;
                usageIsFinal = candidate?.FinishReason is not null;
            }
            if (candidate is null)
            {
                continue;
            }

            sawAnyCandidate = true;
            finishReason = candidate.FinishReason ?? finishReason;

            // Each streamed chunk carries new part fragments, not cumulative slots: a functionCall arriving at
            // parts[0] after text at parts[0] is a new part, and answer text after thought text is a new part.
            // Fragments are therefore routed by kind against the most recently opened accumulator.
            foreach (var partDto in candidate.Content?.Parts ?? [])
            {
                sequence = await ProcessStreamedPartAsync(observer, requestId, sequence, partDto, parts, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        ModelUsage usageResult;
        try
        {
            usageResult = BuildUsage(
                usage,
                usageIsFinal ? ModelUsageReportState.Final : ModelUsageReportState.Interim);
        }
        catch (ArgumentException exception)
        {
            return await FailAsync(
                observer,
                context,
                sequence,
                ProviderFailureKind.ProtocolViolation,
                "The provider returned invalid usage evidence.",
                exception,
                cancellationToken,
                BuildPartialParts(parts)).ConfigureAwait(false);
        }

        if (!sawAnyCandidate || finishReason is null)
        {
            return await FailAsync(
                observer,
                context,
                sequence,
                blockReason is { Length: > 0 }
                    ? ProviderFailureKind.InvalidRequest
                    : ProviderFailureKind.ProtocolViolation,
                blockReason is { Length: > 0 }
                    ? $"The provider blocked the prompt: {blockReason}."
                    : "The provider's streaming response ended before a finish reason was received.",
                diagnosticCause: null,
                cancellationToken,
                BuildPartialParts(parts),
                usage is null ? null : usageResult).ConfigureAwait(false);
        }

        var finalParts = ImmutableArray.CreateBuilder<ContentPart>();
        for (var index = 0; index < parts.Count; index++)
        {
            var accumulator = parts[index];
            sequence = await CloseAccumulatorAsync(observer, requestId, sequence, index, accumulator, cancellationToken)
                .ConfigureAwait(false);
            finalParts.Add(accumulator.Part!);
        }

        if (usage is not null)
        {
            await observer.OnEventAsync(new ModelUsageUpdated(requestId, sequence++, usageResult), cancellationToken)
                .ConfigureAwait(false);
        }

        var response = new ModelResponse(
            requestId,
            context.CreateResponseIdentity(resolvedModel, responseId),
            finalParts.ToImmutable(),
            MapStopReason(finishReason, finalParts.Any(p => p is ToolCallPart)),
            usageResult,
            ExtensionData.Empty);

        await observer.OnEventAsync(new ModelResponseCompleted(requestId, sequence++, response), cancellationToken)
            .ConfigureAwait(false);

        return new ModelAttemptCompleted(response);
    }

    /// <summary>
    /// Routes one streamed part fragment to the open accumulator it continues, or opens a new one. Function
    /// calls always form their own closed part; thought text continues an open reasoning part and plain text
    /// continues an open text part; any kind change closes the previous accumulator first.
    /// </summary>
    private async Task<long> ProcessStreamedPartAsync(
        IModelResponseObserver observer,
        ModelRequestId requestId,
        long sequence,
        GoogleGeminiPartDto partDto,
        List<PartAccumulator> parts,
        CancellationToken cancellationToken)
    {
        var current = parts.Count == 0 ? null : parts[^1];
        var currentIndex = parts.Count - 1;

        if (partDto.FunctionCall is { } functionCall)
        {
            if (current is not null)
            {
                sequence = await CloseAccumulatorAsync(observer, requestId, sequence, currentIndex, current, cancellationToken)
                    .ConfigureAwait(false);
            }

            var index = parts.Count;
            var accumulator = new PartAccumulator();
            parts.Add(accumulator);
            await observer.OnEventAsync(new ModelPartStarted(requestId, sequence++, index), cancellationToken)
                .ConfigureAwait(false);

            var callId = _toolCallIdGenerator.Create();
            var arguments = functionCall.Args ?? ProviderJson.ParseEmptyObject();
            var toolCallPart = CreateToolCallPart(callId, functionCall, arguments, partDto.ThoughtSignature);

            await observer.OnEventAsync(
                    new ModelPartDelta(requestId, sequence++, index, new ToolArgumentsContentDelta(callId, arguments.GetRawText())),
                    cancellationToken)
                .ConfigureAwait(false);

            accumulator.Part = toolCallPart;
            accumulator.Closed = true;
            await observer.OnEventAsync(new ModelPartCompleted(requestId, sequence++, index, toolCallPart), cancellationToken)
                .ConfigureAwait(false);
            return sequence;
        }

        var kind = partDto.Thought == true
            ? PartKind.Reasoning
            : partDto.Text is not null ? PartKind.Text : PartKind.Unknown;

        // A signature belongs to exactly one wire part and two signed parts must never be merged, so a fragment
        // carrying a different signature than the open accumulator starts a new part of the same kind.
        var carriesDistinctSignature = partDto.ThoughtSignature is { Length: > 0 } signature
            && current is { Closed: false, Signature: { } existing }
            && !string.Equals(existing, signature, StringComparison.Ordinal);

        if (current is null || current.Closed || current.Kind != kind || kind == PartKind.Unknown || carriesDistinctSignature)
        {
            if (current is not null)
            {
                sequence = await CloseAccumulatorAsync(observer, requestId, sequence, currentIndex, current, cancellationToken)
                    .ConfigureAwait(false);
            }

            current = new PartAccumulator { Kind = kind };
            currentIndex = parts.Count;
            parts.Add(current);
            await observer.OnEventAsync(new ModelPartStarted(requestId, sequence++, currentIndex), cancellationToken)
                .ConfigureAwait(false);
            if (kind == PartKind.Unknown)
            {
                current.UnknownDto = partDto;
                return sequence;
            }
        }

        // The signature may arrive on any fragment of the part, including a trailing fragment with empty text;
        // the first one observed is retained for the whole part.
        current.Signature ??= partDto.ThoughtSignature;

        if (!string.IsNullOrEmpty(partDto.Text))
        {
            _ = current.Text.Append(partDto.Text);
            ContentDelta delta = kind == PartKind.Reasoning
                ? new ReasoningContentDelta(partDto.Text, ExtensionData.Empty)
                : new TextContentDelta(partDto.Text);
            await observer.OnEventAsync(new ModelPartDelta(requestId, sequence++, currentIndex, delta), cancellationToken)
                .ConfigureAwait(false);
        }

        return sequence;
    }

    /// <summary>Materializes an open accumulator into its final part and emits its completion exactly once.</summary>
    private static async Task<long> CloseAccumulatorAsync(
        IModelResponseObserver observer,
        ModelRequestId requestId,
        long sequence,
        int index,
        PartAccumulator accumulator,
        CancellationToken cancellationToken)
    {
        if (accumulator.Closed)
        {
            return sequence;
        }

        accumulator.Part = MaterializePart(accumulator);
        accumulator.Closed = true;

        await observer.OnEventAsync(new ModelPartCompleted(requestId, sequence++, index, accumulator.Part), cancellationToken)
            .ConfigureAwait(false);
        return sequence;
    }

    /// <summary>Builds the content part an open text, reasoning, or unknown accumulator currently represents.</summary>
    /// <param name="accumulator">An accumulator that has not been closed; function-call accumulators close at creation and never reach this method open.</param>
    /// <returns>The part built from the accumulated state.</returns>
    private static ContentPart MaterializePart(PartAccumulator accumulator)
    {
        Debug.Assert(accumulator is not null, "Callers materialize an existing accumulator.");
        Debug.Assert(!accumulator.Closed, "Closed accumulators already carry their final part.");

        return accumulator.Kind switch
        {
            PartKind.Text => new TextPart(
                accumulator.Text.ToString(),
                TextSemantics.Plain,
                GoogleGeminiThoughtSignature.Create(accumulator.Signature)),
            PartKind.Reasoning => new ReasoningPart(
                new ReasoningContent(accumulator.Text.ToString(), ReasoningVisibility.Visible, accumulator.Signature, ExtensionData.Empty),
                ExtensionData.Empty),
            PartKind.Unknown => new UnknownContentPart(
                _unknownPartTypeName,
                JsonSerializer.SerializeToElement(accumulator.UnknownDto, _serializerOptions),
                ExtensionData.Empty),
            _ => throw new UnreachableException($"Unrecognized {nameof(PartKind)} value '{accumulator.Kind}'."),
        };
    }

    /// <summary>
    /// Collects the truthful partial output of an interrupted stream: every accumulator in ordinal order, using
    /// the closed accumulator's final part when it has one and otherwise materializing the text received so far.
    /// </summary>
    /// <param name="parts">The streaming accumulators in the order they were opened.</param>
    /// <returns>The parts in ordinal order.</returns>
    private static ImmutableArray<ContentPart> BuildPartialParts(List<PartAccumulator> parts)
    {
        Debug.Assert(parts is not null, "The streaming state machine always owns the accumulator list.");

        var partial = ImmutableArray.CreateBuilder<ContentPart>(parts.Count);
        foreach (var accumulator in parts)
        {
            partial.Add(accumulator.Closed ? accumulator.Part! : MaterializePart(accumulator));
        }

        return partial.ToImmutable();
    }

    /// <summary>
    /// Builds the usage retained so far for an interrupted stream, or null when the provider has not reported any
    /// usage or the reported values are not valid evidence; a failure exit never masks its own cause with a
    /// secondary usage-validation failure.
    /// </summary>
    /// <param name="usage">The latest <c>usageMetadata</c> received, when any.</param>
    /// <param name="usageIsFinal">Whether that usage accompanied a finish reason.</param>
    /// <returns>The retained usage evidence, or null when none can be truthfully reported.</returns>
    private static ModelUsage? TryBuildRetainedUsage(GoogleGeminiUsageMetadataDto? usage, bool usageIsFinal)
    {
        if (usage is null)
        {
            return null;
        }

        try
        {
            return BuildUsage(usage, usageIsFinal ? ModelUsageReportState.Final : ModelUsageReportState.Interim);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static async Task<ModelAttemptResult> FailAsync(
        IModelResponseObserver observer,
        ProviderResponseParseContext context,
        long sequence,
        ProviderFailureKind kind,
        string safeMessage,
        Exception? diagnosticCause,
        CancellationToken cancellationToken,
        ImmutableArray<ContentPart> partialParts = default,
        ModelUsage? usage = null)
    {
        var normalizedPartialParts = partialParts.IsDefault ? [] : partialParts;
        var failure = new ProviderFailure(
            kind,
            context.ProviderId,
            context.ProviderRequestId,
            statusCode: null,
            providerCode: null,
            retryAfter: null,
            safeMessage,
            diagnosticCause,
            ExtensionData.Empty);

        await observer.OnEventAsync(
                new ModelResponseFailed(context.ModelRequestId, sequence, failure, normalizedPartialParts, usage),
                cancellationToken)
            .ConfigureAwait(false);

        return new ModelAttemptFailed(failure, normalizedPartialParts, usage);
    }

    private static (ContentDelta? Delta, ContentPart Part) BuildPart(
        GoogleGeminiPartDto part,
        IIdentifierGenerator<ToolCallId> toolCallIdGenerator)
    {
        if (part.FunctionCall is { } functionCall)
        {
            var callId = toolCallIdGenerator.Create();
            var arguments = functionCall.Args ?? ProviderJson.ParseEmptyObject();
            var delta = new ToolArgumentsContentDelta(callId, arguments.GetRawText());
            var toolCallPart = CreateToolCallPart(callId, functionCall, arguments, part.ThoughtSignature);

            return (delta, toolCallPart);
        }

        if (part.Thought == true)
        {
            return (
                new ReasoningContentDelta(part.Text ?? string.Empty, ExtensionData.Empty),
                new ReasoningPart(
                    new ReasoningContent(part.Text, ReasoningVisibility.Visible, part.ThoughtSignature, ExtensionData.Empty),
                    ExtensionData.Empty));
        }

        if (part.Text is not null)
        {
            // A non-function-call answer may carry the turn's thought signature on its final text part; it is kept
            // on that exact part so the translator can echo it back in place.
            return (
                new TextContentDelta(part.Text),
                new TextPart(part.Text, TextSemantics.Plain, GoogleGeminiThoughtSignature.Create(part.ThoughtSignature)));
        }

        return (null, new UnknownContentPart(_unknownPartTypeName, JsonSerializer.SerializeToElement(part, _serializerOptions), ExtensionData.Empty));
    }

    /// <summary>
    /// Builds the tool-call part for one <c>functionCall</c> wire part, retaining the part's
    /// <c>thoughtSignature</c> as typed extension data so it can be echoed back inside the same part.
    /// </summary>
    /// <param name="callId">The internal call identity minted for this request.</param>
    /// <param name="functionCall">The parsed <c>functionCall</c> object.</param>
    /// <param name="arguments">The call arguments, already defaulted to an empty object when absent.</param>
    /// <param name="thoughtSignature">The sibling <c>thoughtSignature</c> on the enclosing part, when any.</param>
    /// <returns>The immutable tool-call part.</returns>
    private static ToolCallPart CreateToolCallPart(
        ToolCallId callId,
        GoogleGeminiFunctionCallDto functionCall,
        JsonElement arguments,
        string? thoughtSignature)
    {
        Debug.Assert(functionCall is not null, "Callers only build a tool-call part for a functionCall wire part.");

        return new ToolCallPart(
            callId,
            new ToolReference(new ToolId(functionCall.Name), null, functionCall.Name),
            arguments,
            functionCall.Id is { Length: > 0 } id ? new ProviderToolCallId(id) : null,
            GoogleGeminiThoughtSignature.Create(thoughtSignature));
    }

    private static ModelUsage BuildUsage(
        GoogleGeminiUsageMetadataDto? usage,
        ModelUsageReportState reportState = ModelUsageReportState.Final)
    {
        Debug.Assert(Enum.IsDefined(reportState), "Callers supply a defined usage report state.");
        if (usage is null)
        {
            return ModelUsage.NotReported;
        }

        var extensions = usage.ToolUsePromptTokenCount is { } toolUseTokens
            ? new ExtensionData(
                ImmutableDictionary<string, ExtensionValue>.Empty.Add(
                    GoogleGeminiExtensionKeys.ToolUsePromptTokenCount,
                    new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(toolUseTokens)])))
            : ExtensionData.Empty;

        return new ModelUsage(reportState,
            usage.PromptTokenCount,
            usage.CandidatesTokenCount,
            usage.CachedContentTokenCount,
            usage.ThoughtsTokenCount,
            estimatedCost: null,
            costCurrency: null,
            extensions);
    }

    private static NormalizedStopReason MapStopReason(string? finishReason, bool hasToolCall)
    {
        var baseReason = finishReason switch
        {
            "STOP" => NormalizedStopReason.Completed,
            "MAX_TOKENS" => NormalizedStopReason.Length,
            null => NormalizedStopReason.Pending,
            _ => NormalizedStopReason.Error,
        };

        return baseReason == NormalizedStopReason.Completed && hasToolCall ? NormalizedStopReason.ToolUse : baseReason;
    }

    private enum PartKind
    {
        Text,
        Reasoning,
        Unknown,
    }

    /// <summary>
    /// Mutable, private streaming-accumulation state for one in-progress
    /// content part, keyed by its ordinal position within
    /// <c>candidates[0].content.parts</c>. This is implementation-internal
    /// parsing state, never exposed outside this class.
    /// </summary>
    private sealed class PartAccumulator
    {
        public PartKind Kind { get; set; }

        public StringBuilder Text { get; } = new();

        /// <summary>
        /// The first <c>thoughtSignature</c> observed on any fragment of this part. A reasoning part surfaces it
        /// as <see cref="ReasoningContent.SignatureToken"/>; a text part retains it as typed extension data.
        /// </summary>
        public string? Signature { get; set; }

        public GoogleGeminiPartDto? UnknownDto { get; set; }

        public bool Closed { get; set; }

        public ContentPart? Part { get; set; }
    }
}
