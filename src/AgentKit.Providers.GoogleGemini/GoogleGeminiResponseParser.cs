// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini;

using AgentKit.Providers.GoogleGemini.Wire;

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
        GoogleGeminiResponseParseContext context,
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
                cancellationToken).ConfigureAwait(false);
        }
        if (dto.UsageMetadata is not null)
        {
            await observer.OnEventAsync(new ModelUsageUpdated(requestId, sequence++, usage), cancellationToken)
                .ConfigureAwait(false);
        }

        var response = new ModelResponse(
            requestId,
            BuildIdentity(context, dto.ModelVersion, dto.ResponseId),
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
        GoogleGeminiResponseParseContext context,
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

        using var reader = new StreamReader(responseBody, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);

        var parts = new List<PartAccumulator>();
        string? resolvedModel = null;
        string? responseId = null;
        GoogleGeminiUsageMetadataDto? usage = null;
        var usageIsFinal = false;
        string? finishReason = null;
        string? blockReason = null;
        var sawAnyCandidate = false;

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (line.Length == 0 || !line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var payload = line["data:".Length..].TrimStart();
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
                    cancellationToken).ConfigureAwait(false);
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

            var partsDto = candidate.Content?.Parts ?? [];
            for (var index = 0; index < partsDto.Count; index++)
            {
                sequence = await ProcessStreamedPartAsync(observer, requestId, sequence, index, partsDto[index], parts, cancellationToken)
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
                cancellationToken).ConfigureAwait(false);
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
                usage is null ? null : usageResult).ConfigureAwait(false);
        }

        var finalParts = ImmutableArray.CreateBuilder<ContentPart>();
        for (var index = 0; index < parts.Count; index++)
        {
            var accumulator = parts[index];
            if (!accumulator.Closed)
            {
                accumulator.Part = accumulator.Kind switch
                {
                    PartKind.Text => new TextPart(accumulator.Text.ToString(), TextSemantics.Plain, ExtensionData.Empty),
                    PartKind.Reasoning => new ReasoningPart(
                        new ReasoningContent(accumulator.Text.ToString(), ReasoningVisibility.Visible, accumulator.Signature, ExtensionData.Empty),
                        ExtensionData.Empty),
                    PartKind.Unknown => new UnknownContentPart(
                        _unknownPartTypeName,
                        JsonSerializer.SerializeToElement(accumulator.UnknownDto, _serializerOptions),
                        ExtensionData.Empty),
                    _ => throw new UnreachableException($"Unrecognized {nameof(PartKind)} value '{accumulator.Kind}'."),
                };
                accumulator.Closed = true;

                await observer.OnEventAsync(
                        new ModelPartCompleted(requestId, sequence++, index, accumulator.Part),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            finalParts.Add(accumulator.Part!);
        }

        if (usage is not null)
        {
            await observer.OnEventAsync(new ModelUsageUpdated(requestId, sequence++, usageResult), cancellationToken)
                .ConfigureAwait(false);
        }

        var response = new ModelResponse(
            requestId,
            BuildIdentity(context, resolvedModel, responseId),
            finalParts.ToImmutable(),
            MapStopReason(finishReason, finalParts.Any(p => p is ToolCallPart)),
            usageResult,
            ExtensionData.Empty);

        await observer.OnEventAsync(new ModelResponseCompleted(requestId, sequence++, response), cancellationToken)
            .ConfigureAwait(false);

        return new ModelAttemptCompleted(response);
    }

    private async Task<long> ProcessStreamedPartAsync(
        IModelResponseObserver observer,
        ModelRequestId requestId,
        long sequence,
        int index,
        GoogleGeminiPartDto partDto,
        List<PartAccumulator> parts,
        CancellationToken cancellationToken)
    {
        if (index >= parts.Count)
        {
            var accumulator = new PartAccumulator();
            parts.Add(accumulator);
            await observer.OnEventAsync(new ModelPartStarted(requestId, sequence++, index), cancellationToken)
                .ConfigureAwait(false);

            if (partDto.FunctionCall is { } functionCall)
            {
                var callId = _toolCallIdGenerator.Create();
                var arguments = functionCall.Args ?? ParseEmptyObject();
                var toolCallPart = new ToolCallPart(
                    callId,
                    new ToolReference(new ToolId(functionCall.Name), null, functionCall.Name),
                    arguments,
                    functionCall.Id is { Length: > 0 } id ? new ProviderToolCallId(id) : null,
                    ExtensionData.Empty);

                await observer.OnEventAsync(
                        new ModelPartDelta(requestId, sequence++, index, new ToolArgumentsContentDelta(callId, arguments.GetRawText())),
                        cancellationToken)
                    .ConfigureAwait(false);

                accumulator.Part = toolCallPart;
                accumulator.Closed = true;
                await observer.OnEventAsync(new ModelPartCompleted(requestId, sequence++, index, toolCallPart), cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (partDto.Thought == true)
            {
                accumulator.Kind = PartKind.Reasoning;
                accumulator.Signature = partDto.ThoughtSignature;
                if (!string.IsNullOrEmpty(partDto.Text))
                {
                    _ = accumulator.Text.Append(partDto.Text);
                    await observer.OnEventAsync(
                            new ModelPartDelta(requestId, sequence++, index, new ReasoningContentDelta(partDto.Text, ExtensionData.Empty)),
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            else if (partDto.Text is not null)
            {
                accumulator.Kind = PartKind.Text;
                if (partDto.Text.Length > 0)
                {
                    _ = accumulator.Text.Append(partDto.Text);
                    await observer.OnEventAsync(
                            new ModelPartDelta(requestId, sequence++, index, new TextContentDelta(partDto.Text)),
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            else
            {
                accumulator.Kind = PartKind.Unknown;
                accumulator.UnknownDto = partDto;
            }

            return sequence;
        }

        var existing = parts[index];
        if (existing.Closed)
        {
            return sequence;
        }

        if (existing.Kind == PartKind.Text && !string.IsNullOrEmpty(partDto.Text))
        {
            _ = existing.Text.Append(partDto.Text);
            await observer.OnEventAsync(
                    new ModelPartDelta(requestId, sequence++, index, new TextContentDelta(partDto.Text)),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (existing.Kind == PartKind.Reasoning)
        {
            existing.Signature ??= partDto.ThoughtSignature;
            if (!string.IsNullOrEmpty(partDto.Text))
            {
                _ = existing.Text.Append(partDto.Text);
                await observer.OnEventAsync(
                        new ModelPartDelta(requestId, sequence++, index, new ReasoningContentDelta(partDto.Text, ExtensionData.Empty)),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return sequence;
    }

    private static async Task<ModelAttemptResult> FailAsync(
        IModelResponseObserver observer,
        GoogleGeminiResponseParseContext context,
        long sequence,
        ProviderFailureKind kind,
        string safeMessage,
        Exception? diagnosticCause,
        CancellationToken cancellationToken,
        ModelUsage? usage = null)
    {
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
                new ModelResponseFailed(context.ModelRequestId, sequence, failure, [], usage),
                cancellationToken)
            .ConfigureAwait(false);

        return new ModelAttemptFailed(failure, [], usage);
    }

    private static (ContentDelta? Delta, ContentPart Part) BuildPart(
        GoogleGeminiPartDto part,
        IIdentifierGenerator<ToolCallId> toolCallIdGenerator)
    {
        if (part.FunctionCall is { } functionCall)
        {
            var callId = toolCallIdGenerator.Create();
            var arguments = functionCall.Args ?? ParseEmptyObject();
            var delta = new ToolArgumentsContentDelta(callId, arguments.GetRawText());
            var toolCallPart = new ToolCallPart(
                callId,
                new ToolReference(new ToolId(functionCall.Name), null, functionCall.Name),
                arguments,
                functionCall.Id is { Length: > 0 } id ? new ProviderToolCallId(id) : null,
                ExtensionData.Empty);

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
            return (new TextContentDelta(part.Text), new TextPart(part.Text, TextSemantics.Plain, ExtensionData.Empty));
        }

        return (null, new UnknownContentPart(_unknownPartTypeName, JsonSerializer.SerializeToElement(part, _serializerOptions), ExtensionData.Empty));
    }

    private static JsonElement ParseEmptyObject()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }

    private static ProviderResponseIdentity BuildIdentity(
        GoogleGeminiResponseParseContext context, string? resolvedModel, string? responseId) =>
        new(
            context.ProviderId,
            upstreamProviderId: null,
            context.ApiFamily,
            context.RequestedModelId,
            resolvedModel is { Length: > 0 } model ? new ModelId(model) : context.RequestedModelId,
            context.DeploymentId,
            context.ProviderRequestId,
            responseId is { Length: > 0 } id ? new ProviderResponseId(id) : null);

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
                    "tool_use_prompt_token_count",
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

        public string? Signature { get; set; }

        public GoogleGeminiPartDto? UnknownDto { get; set; }

        public bool Closed { get; set; }

        public ContentPart? Part { get; set; }
    }
}
