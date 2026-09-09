// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI;

using AgentKit.Providers.MistralAI.Wire;

/// <summary>
/// The default <see cref="IMistralAIResponseParser"/>, parsing both
/// buffered and server-sent-events Mistral AI Chat Completions responses as
/// a typed state machine.
/// </summary>
/// <remarks>
/// This parser does not translate Mistral's <c>ThinkChunk</c> reasoning
/// content; a <c>"thinking"</c>-typed content-chunk is preserved as a
/// standalone <see cref="UnknownContentPart"/> per occurrence rather than
/// accumulated into a coherent reasoning trace, because Mistral's exact
/// incremental streaming shape for reasoning (a content array that
/// alternates with a plain string mid-stream) is not fully specified
/// without live verification. Streaming is terminated by a literal
/// <c>data: [DONE]</c> line, unlike Gemini's finish-reason-driven
/// termination; a stream that ends before that sentinel is a truncation.
/// </remarks>
public sealed class MistralAIResponseParser: IMistralAIResponseParser
{
    private const string _doneSentinel = "[DONE]";
    private const string _unknownChunkTypeName = "unknownChunk";

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IIdentifierGenerator<ToolCallId> _toolCallIdGenerator;

    /// <summary>Initializes a new instance of the <see cref="MistralAIResponseParser"/> class.</summary>
    /// <param name="toolCallIdGenerator">
    /// Mints the internal <see cref="ToolCallId"/> assigned to each tool
    /// call the provider returns.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="toolCallIdGenerator"/> is null.</exception>
    public MistralAIResponseParser(IIdentifierGenerator<ToolCallId> toolCallIdGenerator)
    {
        ArgumentNullException.ThrowIfNull(toolCallIdGenerator);
        _toolCallIdGenerator = toolCallIdGenerator;
    }

    /// <inheritdoc/>
    public async Task<ModelAttemptResult> ParseBufferedAsync(
        Stream responseBody,
        MistralAIResponseParseContext context,
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

        MistralAIChatCompletionResponseDto dto;
        try
        {
            dto = await JsonSerializer
                .DeserializeAsync<MistralAIChatCompletionResponseDto>(responseBody, _serializerOptions, cancellationToken)
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

        var choice = dto.Choices?.Count > 0 ? dto.Choices[0] : null;
        if (choice is null)
        {
            return await FailAsync(
                observer,
                context,
                sequence,
                ProviderFailureKind.ProtocolViolation,
                "The provider returned a response with no choices.",
                diagnosticCause: null,
                cancellationToken).ConfigureAwait(false);
        }

        var built = BuildParts(choice.Message, _toolCallIdGenerator);
        var parts = ImmutableArray.CreateBuilder<ContentPart>();

        for (var index = 0; index < built.Count; index++)
        {
            await observer.OnEventAsync(new ModelPartStarted(requestId, sequence++, index), cancellationToken)
                .ConfigureAwait(false);

            var (delta, part) = built[index];
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
            usage = BuildUsage(dto.Usage);
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
                cancellationToken)
                .ConfigureAwait(false);
        }
        if (dto.Usage is not null)
        {
            await observer.OnEventAsync(new ModelUsageUpdated(requestId, sequence++, usage), cancellationToken)
                .ConfigureAwait(false);
        }

        var response = new ModelResponse(
            requestId,
            BuildIdentity(context, dto.Model, dto.Id),
            parts.ToImmutable(),
            MapFinishReason(choice.FinishReason),
            usage,
            ExtensionData.Empty);

        await observer.OnEventAsync(new ModelResponseCompleted(requestId, sequence++, response), cancellationToken)
            .ConfigureAwait(false);

        return new ModelAttemptCompleted(response);
    }

    /// <inheritdoc/>
    public async Task<ModelAttemptResult> ParseStreamingAsync(
        Stream responseBody,
        MistralAIResponseParseContext context,
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

        var state = new StreamState();
        string? resolvedModel = null;
        string? responseId = null;
        MistralAIUsageDto? usage = null;
        var usageIsFinal = false;
        string? finishReason = null;
        var sawDoneSentinel = false;

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

            if (payload == _doneSentinel)
            {
                sawDoneSentinel = true;
                break;
            }

            MistralAICompletionChunkDto chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<MistralAICompletionChunkDto>(payload, _serializerOptions)
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

            resolvedModel ??= chunk.Model;
            responseId ??= chunk.Id;
            var choice = chunk.Choices?.Count > 0 ? chunk.Choices[0] : null;
            if (chunk.Usage is not null)
            {
                usage = chunk.Usage;
                usageIsFinal = choice?.FinishReason is not null || finishReason is not null;
            }
            if (choice is null)
            {
                continue;
            }

            finishReason = choice.FinishReason ?? finishReason;

            if (choice.Delta is { } delta)
            {
                sequence = await ProcessDeltaAsync(observer, requestId, sequence, delta, state, _toolCallIdGenerator, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (!sawDoneSentinel || finishReason is null)
        {
            return await FailAsync(
                observer,
                context,
                sequence,
                ProviderFailureKind.ProtocolViolation,
                "The provider's streaming response ended before the terminal [DONE] sentinel and a finish " +
                "reason were both received.",
                diagnosticCause: null,
                cancellationToken).ConfigureAwait(false);
        }

        var finalParts = ImmutableArray.CreateBuilder<ContentPart>();
        foreach (var slot in state.Slots)
        {
            if (!slot.Closed)
            {
                slot.FinalPart = slot.Kind switch
                {
                    SlotKind.Text => new TextPart(slot.Text.ToString(), TextSemantics.Plain, ExtensionData.Empty),
                    SlotKind.ToolCall => new ToolCallPart(
                        slot.AssignedCallId,
                        new ToolReference(new ToolId(slot.ToolCallName ?? string.Empty), null, slot.ToolCallName ?? string.Empty),
                        ParseArguments(slot.ToolCallArguments.ToString()),
                        slot.ToolCallId is { Length: > 0 } id ? new ProviderToolCallId(id) : null,
                        ExtensionData.Empty),
                    SlotKind.Unknown => throw new UnreachableException("An unknown-kind slot is always closed at creation."),
                    _ => throw new UnreachableException($"Unrecognized {nameof(SlotKind)} value '{slot.Kind}'."),
                };
                slot.Closed = true;

                await observer.OnEventAsync(
                        new ModelPartCompleted(requestId, sequence++, slot.PartIndex, slot.FinalPart),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            finalParts.Add(slot.FinalPart!);
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
                cancellationToken)
                .ConfigureAwait(false);
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
            MapFinishReason(finishReason),
            usageResult,
            ExtensionData.Empty);

        await observer.OnEventAsync(new ModelResponseCompleted(requestId, sequence++, response), cancellationToken)
            .ConfigureAwait(false);

        return new ModelAttemptCompleted(response);
    }

    private static async Task<long> ProcessDeltaAsync(
        IModelResponseObserver observer,
        ModelRequestId requestId,
        long sequence,
        MistralAIDeltaMessageDto delta,
        StreamState state,
        IIdentifierGenerator<ToolCallId> toolCallIdGenerator,
        CancellationToken cancellationToken)
    {
        if (delta.Content is { } content)
        {
            switch (content.ValueKind)
            {
                case JsonValueKind.String:
                    sequence = await AppendTextFragmentAsync(observer, requestId, sequence, content.GetString(), state, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case JsonValueKind.Array:
                    foreach (var element in content.EnumerateArray())
                    {
                        var chunk = element.Deserialize<MistralAIContentChunkDto>(_serializerOptions);
                        sequence = chunk?.Type == "text"
                            ? await AppendTextFragmentAsync(observer, requestId, sequence, chunk.Text, state, cancellationToken)
                                .ConfigureAwait(false)
                            : await AppendUnknownChunkAsync(
                                    observer, requestId, sequence, chunk?.Type ?? _unknownChunkTypeName, element.Clone(), state, cancellationToken)
                                .ConfigureAwait(false);
                    }

                    break;
                case JsonValueKind.Undefined:
                    break;
                case JsonValueKind.Object:
                    break;
                case JsonValueKind.Number:
                    break;
                case JsonValueKind.True:
                    break;
                case JsonValueKind.False:
                    break;
                case JsonValueKind.Null:
                    break;
                default:
                    break;
            }
        }

        if (delta.ToolCalls is { } toolCalls)
        {
            foreach (var toolCall in toolCalls)
            {
                sequence = await AppendToolCallFragmentAsync(observer, requestId, sequence, toolCall, state, toolCallIdGenerator, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return sequence;
    }

    private static async Task<long> AppendTextFragmentAsync(
        IModelResponseObserver observer,
        ModelRequestId requestId,
        long sequence,
        string? fragment,
        StreamState state,
        CancellationToken cancellationToken)
    {
        if (state.TextSlot is null)
        {
            state.TextSlot = new Slot { Kind = SlotKind.Text, PartIndex = state.Slots.Count };
            state.Slots.Add(state.TextSlot);
            await observer.OnEventAsync(new ModelPartStarted(requestId, sequence++, state.TextSlot.PartIndex), cancellationToken)
                .ConfigureAwait(false);
        }

        if (string.IsNullOrEmpty(fragment))
        {
            return sequence;
        }

        _ = state.TextSlot.Text.Append(fragment);
        await observer.OnEventAsync(
                new ModelPartDelta(requestId, sequence++, state.TextSlot.PartIndex, new TextContentDelta(fragment)),
                cancellationToken)
            .ConfigureAwait(false);

        return sequence;
    }

    private static async Task<long> AppendUnknownChunkAsync(
        IModelResponseObserver observer,
        ModelRequestId requestId,
        long sequence,
        string typeName,
        JsonElement payload,
        StreamState state,
        CancellationToken cancellationToken)
    {
        var slot = new Slot
        {
            Kind = SlotKind.Unknown,
            PartIndex = state.Slots.Count,
            Closed = true,
            FinalPart = new UnknownContentPart(typeName, payload, ExtensionData.Empty),
        };
        state.Slots.Add(slot);

        await observer.OnEventAsync(new ModelPartStarted(requestId, sequence++, slot.PartIndex), cancellationToken)
            .ConfigureAwait(false);
        await observer.OnEventAsync(new ModelPartCompleted(requestId, sequence++, slot.PartIndex, slot.FinalPart), cancellationToken)
            .ConfigureAwait(false);

        return sequence;
    }

    private static async Task<long> AppendToolCallFragmentAsync(
        IModelResponseObserver observer,
        ModelRequestId requestId,
        long sequence,
        MistralAIToolCallDto toolCall,
        StreamState state,
        IIdentifierGenerator<ToolCallId> toolCallIdGenerator,
        CancellationToken cancellationToken)
    {
        var wireIndex = toolCall.Index ?? 0;
        if (!state.ToolCallSlotsByWireIndex.TryGetValue(wireIndex, out var slot))
        {
            slot = new Slot { Kind = SlotKind.ToolCall, PartIndex = state.Slots.Count, AssignedCallId = toolCallIdGenerator.Create() };
            state.Slots.Add(slot);
            state.ToolCallSlotsByWireIndex[wireIndex] = slot;
            await observer.OnEventAsync(new ModelPartStarted(requestId, sequence++, slot.PartIndex), cancellationToken)
                .ConfigureAwait(false);
        }

        slot.ToolCallId ??= toolCall.Id;
        if (toolCall.Function is { } function)
        {
            slot.ToolCallName ??= function.Name;

            var fragment = ExtractArgumentsFragmentText(function.Arguments);
            if (!string.IsNullOrEmpty(fragment))
            {
                _ = slot.ToolCallArguments.Append(fragment);
                await observer.OnEventAsync(
                        new ModelPartDelta(requestId, sequence++, slot.PartIndex, new ToolArgumentsContentDelta(slot.AssignedCallId, fragment)),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return sequence;
    }

    private static string? ExtractArgumentsFragmentText(JsonElement? arguments) =>
        arguments switch
        {
            { ValueKind: JsonValueKind.String } element => element.GetString(),
            { ValueKind: JsonValueKind.Object } element => element.GetRawText(),
            _ => null,
        };

    private static async Task<ModelAttemptResult> FailAsync(
        IModelResponseObserver observer,
        MistralAIResponseParseContext context,
        long sequence,
        ProviderFailureKind kind,
        string safeMessage,
        Exception? diagnosticCause,
        CancellationToken cancellationToken)
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
                new ModelResponseFailed(context.ModelRequestId, sequence, failure, [], usage: null),
                cancellationToken)
            .ConfigureAwait(false);

        return new ModelAttemptFailed(failure, [], usage: null);
    }

    private static List<(ContentDelta? Delta, ContentPart Part)> BuildParts(
        MistralAIAssistantMessageDto? message,
        IIdentifierGenerator<ToolCallId> toolCallIdGenerator)
    {
        var results = new List<(ContentDelta? Delta, ContentPart Part)>();

        if (message?.Content is { } content && content.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
        {
            switch (content.ValueKind)
            {
                case JsonValueKind.String:
                    var text = content.GetString() ?? string.Empty;
                    results.Add((new TextContentDelta(text), new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)));
                    break;

                case JsonValueKind.Array:
                    foreach (var element in content.EnumerateArray())
                    {
                        var chunk = element.Deserialize<MistralAIContentChunkDto>(_serializerOptions);
                        if (chunk?.Type == "text")
                        {
                            var chunkText = chunk.Text ?? string.Empty;
                            results.Add((new TextContentDelta(chunkText), new TextPart(chunkText, TextSemantics.Plain, ExtensionData.Empty)));
                        }
                        else
                        {
                            results.Add((null, new UnknownContentPart(chunk?.Type ?? _unknownChunkTypeName, element.Clone(), ExtensionData.Empty)));
                        }
                    }

                    break;
                case JsonValueKind.Undefined:
                    break;
                case JsonValueKind.Object:
                    break;
                case JsonValueKind.Number:
                    break;
                case JsonValueKind.True:
                    break;
                case JsonValueKind.False:
                    break;
                case JsonValueKind.Null:
                    break;
                default:
                    break;
            }
        }

        if (message?.ToolCalls is { } toolCalls)
        {
            foreach (var toolCall in toolCalls)
            {
                if (toolCall.Function is not { } function || function.Name is not { Length: > 0 } name)
                {
                    continue;
                }

                var callId = toolCallIdGenerator.Create();
                var arguments = ParseArgumentsElement(function.Arguments);
                var toolCallPart = new ToolCallPart(
                    callId,
                    new ToolReference(new ToolId(name), null, name),
                    arguments,
                    toolCall.Id is { Length: > 0 } id ? new ProviderToolCallId(id) : null,
                    ExtensionData.Empty);

                results.Add((new ToolArgumentsContentDelta(callId, arguments.GetRawText()), toolCallPart));
            }
        }

        return results;
    }

    private static JsonElement ParseArgumentsElement(JsonElement? argumentsElement) =>
        argumentsElement switch
        {
            { ValueKind: JsonValueKind.String } element => ParseArguments(element.GetString()),
            { ValueKind: JsonValueKind.Object } element => element,
            _ => ParseArguments(null),
        };

    private static JsonElement ParseArguments(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return ParseEmptyObject();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return ParseEmptyObject();
        }
    }

    private static JsonElement ParseEmptyObject()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }

    private static ProviderResponseIdentity BuildIdentity(
        MistralAIResponseParseContext context, string? resolvedModel, string? responseId) =>
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
        MistralAIUsageDto? usage,
        ModelUsageReportState reportState = ModelUsageReportState.Final)
    {
        Debug.Assert(Enum.IsDefined(reportState), "Callers supply a defined usage report state.");
        return usage is null
            ? ModelUsage.NotReported
            : new ModelUsage(reportState,
                usage.PromptTokens,
                usage.CompletionTokens,
                cachedInputTokens: null,
                reasoningTokens: null,
                estimatedCost: null,
                costCurrency: null,
                ExtensionData.Empty);
    }

    private static NormalizedStopReason MapFinishReason(string? finishReason) =>
        finishReason switch
        {
            "stop" => NormalizedStopReason.Completed,
            "length" or "model_length" => NormalizedStopReason.Length,
            "tool_calls" => NormalizedStopReason.ToolUse,
            "error" => NormalizedStopReason.Error,
            null => NormalizedStopReason.Pending,
            _ => NormalizedStopReason.Error,
        };

    private enum SlotKind
    {
        Text,
        Unknown,
        ToolCall,
    }

    /// <summary>
    /// Mutable, private streaming-accumulation state for all in-progress
    /// content parts in this response. This is implementation-internal
    /// parsing state, never exposed outside this class.
    /// </summary>
    private sealed class StreamState
    {
        public List<Slot> Slots { get; } = [];

        public Slot? TextSlot { get; set; }

        public Dictionary<int, Slot> ToolCallSlotsByWireIndex { get; } = [];
    }

    /// <summary>
    /// Mutable, private streaming-accumulation state for one in-progress
    /// content part, keyed by its assigned overall part index. This is
    /// implementation-internal parsing state, never exposed outside this
    /// class.
    /// </summary>
    private sealed class Slot
    {
        public SlotKind Kind { get; set; }

        public int PartIndex { get; set; }

        public StringBuilder Text { get; } = new();

        public string? ToolCallId { get; set; }

        public string? ToolCallName { get; set; }

        public StringBuilder ToolCallArguments { get; } = new();

        public ToolCallId AssignedCallId { get; set; }

        public bool Closed { get; set; }

        public ContentPart? FinalPart { get; set; }
    }
}
