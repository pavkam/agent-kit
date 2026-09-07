// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic;

using AgentKit.Providers.Anthropic.Wire;

/// <summary>
/// The default <see cref="IAnthropicMessageStreamParser"/>, parsing both
/// buffered and server-sent-events Anthropic Messages responses as a typed
/// state machine.
/// </summary>
public sealed class AnthropicMessageStreamParser: IAnthropicMessageStreamParser
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IIdentifierGenerator<ToolCallId> _toolCallIdGenerator;

    /// <summary>Initializes a new instance of the <see cref="AnthropicMessageStreamParser"/> class.</summary>
    /// <param name="toolCallIdGenerator">
    /// Mints the internal <see cref="ToolCallId"/> assigned to each
    /// <c>tool_use</c> block the provider returns.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="toolCallIdGenerator"/> is null.</exception>
    public AnthropicMessageStreamParser(IIdentifierGenerator<ToolCallId> toolCallIdGenerator)
    {
        ArgumentNullException.ThrowIfNull(toolCallIdGenerator);
        _toolCallIdGenerator = toolCallIdGenerator;
    }

    /// <inheritdoc/>
    public async Task<ModelAttemptResult> ParseBufferedAsync(
        Stream responseBody,
        AnthropicResponseParseContext context,
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

        AnthropicMessageResponseDto dto;
        try
        {
            dto = await JsonSerializer
                .DeserializeAsync<AnthropicMessageResponseDto>(responseBody, _serializerOptions, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new JsonException("The response body deserialized to a null value.");
        }
        catch (JsonException exception)
        {
            return await FailAsync(
                observer,
                context,
                sequence,
                "The provider returned a response body that could not be parsed.",
                exception,
                cancellationToken).ConfigureAwait(false);
        }

        var parts = ImmutableArray.CreateBuilder<ContentPart>();
        var blocks = dto.Content ?? [];

        for (var index = 0; index < blocks.Count; index++)
        {
            await observer.OnEventAsync(new ModelPartStarted(requestId, sequence++, index), cancellationToken)
                .ConfigureAwait(false);

            var (delta, part) = BuildBlock(blocks[index], _toolCallIdGenerator);
            if (delta is not null)
            {
                await observer.OnEventAsync(new ModelPartDelta(requestId, sequence++, index, delta), cancellationToken)
                    .ConfigureAwait(false);
            }

            await observer.OnEventAsync(new ModelPartCompleted(requestId, sequence++, index, part), cancellationToken)
                .ConfigureAwait(false);
            parts.Add(part);
        }

        var usage = BuildUsage(dto.Usage);
        if (dto.Usage is not null)
        {
            await observer.OnEventAsync(new ModelUsageUpdated(requestId, sequence++, usage), cancellationToken)
                .ConfigureAwait(false);
        }

        var response = new ModelResponse(
            requestId,
            BuildIdentity(context, dto.Model, dto.Id),
            parts.ToImmutable(),
            MapStopReason(dto.StopReason),
            usage,
            ExtensionData.Empty);

        await observer.OnEventAsync(new ModelResponseCompleted(requestId, sequence++, response), cancellationToken)
            .ConfigureAwait(false);

        return new ModelAttemptCompleted(response);
    }

    /// <inheritdoc/>
    public async Task<ModelAttemptResult> ParseStreamingAsync(
        Stream responseBody,
        AnthropicResponseParseContext context,
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

        var blocks = new SortedDictionary<int, BlockAccumulator>();
        string? resolvedModel = null;
        string? responseId = null;
        AnthropicUsageDto? initialUsage = null;
        AnthropicUsageDto? finalUsage = null;
        string? finalStopReason = null;
        var messageStopReceived = false;

        while (!messageStopReceived
            && await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
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

            AnthropicStreamEventDto streamEvent;
            try
            {
                streamEvent = JsonSerializer.Deserialize<AnthropicStreamEventDto>(payload, _serializerOptions)
                    ?? throw new JsonException("The event payload deserialized to a null value.");
            }
            catch (JsonException exception)
            {
                return await FailAsync(
                    observer,
                    context,
                    sequence,
                    "The provider returned a malformed streaming event.",
                    exception,
                    cancellationToken).ConfigureAwait(false);
            }

            switch (streamEvent.Type)
            {
                case "message_start":
                    resolvedModel = streamEvent.Message?.Model;
                    responseId = streamEvent.Message?.Id;
                    initialUsage = streamEvent.Message?.Usage;
                    break;

                case "content_block_start" when streamEvent is { Index: { } startIndex, ContentBlock: { } startBlock }:
                    await HandleContentBlockStartAsync(observer, requestId, startIndex, startBlock, blocks, () => sequence++, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case "content_block_delta" when streamEvent is { Index: { } deltaIndex, Delta: { } delta }
                    && blocks.TryGetValue(deltaIndex, out var deltaAccumulator):
                    await HandleContentBlockDeltaAsync(observer, requestId, context, deltaIndex, delta, deltaAccumulator, () => sequence++, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case "content_block_stop" when streamEvent is { Index: { } stopIndex }
                    && blocks.TryGetValue(stopIndex, out var stopAccumulator):
                    stopAccumulator.Close();
                    await observer.OnEventAsync(
                            new ModelPartCompleted(requestId, sequence++, stopIndex, stopAccumulator.Part!),
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case "message_delta":
                    finalStopReason = streamEvent.Delta?.StopReason ?? finalStopReason;
                    finalUsage = streamEvent.Usage ?? finalUsage;
                    break;

                case "message_stop":
                    messageStopReceived = true;
                    break;

                case "error":
                    {
                        var failure = BuildFailure(
                            context,
                            AnthropicErrorMapping.MapErrorType(streamEvent.Error?.Type),
                            statusCode: null,
                            streamEvent.Error?.Type,
                            streamEvent.Error?.Message ?? "The provider reported a streaming error.",
                            diagnosticCause: null);

                        await observer.OnEventAsync(
                                new ModelResponseFailed(requestId, sequence++, failure, [], usage: null),
                                cancellationToken)
                            .ConfigureAwait(false);
                        return new ModelAttemptFailed(failure, [], usage: null);
                    }

                default:
                    // "ping" and any other unrecognized top-level event kind carry no
                    // application-visible state and are safely ignored.
                    break;
            }
        }

        if (!messageStopReceived)
        {
            return await FailAsync(
                observer,
                context,
                sequence,
                "The provider's streaming response ended before a message_stop event was received.",
                diagnosticCause: null,
                cancellationToken).ConfigureAwait(false);
        }

        var parts = ImmutableArray.CreateBuilder<ContentPart>();
        foreach (var (index, accumulator) in blocks)
        {
            if (!accumulator.IsClosed)
            {
                return await FailAsync(
                    observer,
                    context,
                    sequence,
                    $"The provider's streaming response ended before content block {index} was closed.",
                    diagnosticCause: null,
                    cancellationToken).ConfigureAwait(false);
            }

            parts.Add(accumulator.Part!);
        }

        var usage = BuildUsage(initialUsage, finalUsage);
        if (initialUsage is not null || finalUsage is not null)
        {
            await observer.OnEventAsync(new ModelUsageUpdated(requestId, sequence++, usage), cancellationToken)
                .ConfigureAwait(false);
        }

        var response = new ModelResponse(
            requestId,
            BuildIdentity(context, resolvedModel, responseId),
            parts.ToImmutable(),
            MapStopReason(finalStopReason),
            usage,
            ExtensionData.Empty);

        await observer.OnEventAsync(new ModelResponseCompleted(requestId, sequence++, response), cancellationToken)
            .ConfigureAwait(false);

        return new ModelAttemptCompleted(response);
    }

    private async Task HandleContentBlockStartAsync(
        IModelResponseObserver observer,
        ModelRequestId requestId,
        int index,
        AnthropicContentBlockDto startBlock,
        SortedDictionary<int, BlockAccumulator> blocks,
        Func<long> nextSequence,
        CancellationToken cancellationToken)
    {
        var accumulator = new BlockAccumulator(startBlock.Type);

        switch (startBlock.Type)
        {
            case "tool_use":
                accumulator.ToolCallId = _toolCallIdGenerator.Create();
                accumulator.ToolName = startBlock.Name;
                accumulator.ProviderCallId = startBlock.Id;
                break;

            case "text":
                _ = accumulator.Text.Append(startBlock.Text);
                break;

            case "thinking":
                _ = accumulator.Text.Append(startBlock.Thinking);
                accumulator.Signature = startBlock.Signature;
                break;

            case "redacted_thinking":
                accumulator.RedactedData = startBlock.Data;
                break;

            default:
                accumulator.UnknownBlock = startBlock;
                break;
        }

        blocks[index] = accumulator;
        await observer.OnEventAsync(new ModelPartStarted(requestId, nextSequence(), index), cancellationToken)
            .ConfigureAwait(false);

        if (startBlock.Type == "text" && !string.IsNullOrEmpty(startBlock.Text))
        {
            await observer.OnEventAsync(
                    new ModelPartDelta(requestId, nextSequence(), index, new TextContentDelta(startBlock.Text)),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (startBlock.Type == "thinking" && !string.IsNullOrEmpty(startBlock.Thinking))
        {
            await observer.OnEventAsync(
                    new ModelPartDelta(
                        requestId,
                        nextSequence(),
                        index,
                        new ReasoningContentDelta(startBlock.Thinking, ExtensionData.Empty)),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task HandleContentBlockDeltaAsync(
        IModelResponseObserver observer,
        ModelRequestId requestId,
        AnthropicResponseParseContext context,
        int index,
        AnthropicStreamDeltaDto delta,
        BlockAccumulator accumulator,
        Func<long> nextSequence,
        CancellationToken cancellationToken)
    {
        switch (delta.Type)
        {
            case "text_delta" when delta.Text is { Length: > 0 } textFragment:
                _ = accumulator.Text.Append(textFragment);
                await observer.OnEventAsync(
                        new ModelPartDelta(requestId, nextSequence(), index, new TextContentDelta(textFragment)),
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case "input_json_delta" when delta.PartialJson is { Length: > 0 } jsonFragment:
                _ = accumulator.Json.Append(jsonFragment);
                await observer.OnEventAsync(
                        new ModelPartDelta(
                            requestId,
                            nextSequence(),
                            index,
                            new ToolArgumentsContentDelta(accumulator.ToolCallId!.Value, jsonFragment)),
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case "thinking_delta" when delta.Thinking is { Length: > 0 } thinkingFragment:
                _ = accumulator.Text.Append(thinkingFragment);
                await observer.OnEventAsync(
                        new ModelPartDelta(
                            requestId,
                            nextSequence(),
                            index,
                            new ReasoningContentDelta(thinkingFragment, ExtensionData.Empty)),
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case "signature_delta" when delta.Signature is { Length: > 0 } signatureFragment:
                accumulator.Signature = (accumulator.Signature ?? string.Empty) + signatureFragment;
                break;

            default:
                await observer.OnEventAsync(
                        new ModelPartDelta(
                            requestId,
                            nextSequence(),
                            index,
                            new ProviderContentDelta(context.ProviderId, BuildRawExtension(delta))),
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
        }
    }

    private static async Task<ModelAttemptResult> FailAsync(
        IModelResponseObserver observer,
        AnthropicResponseParseContext context,
        long sequence,
        string safeMessage,
        Exception? diagnosticCause,
        CancellationToken cancellationToken)
    {
        var failure = BuildFailure(
            context,
            ProviderFailureKind.ProtocolViolation,
            statusCode: null,
            providerCode: null,
            safeMessage,
            diagnosticCause);

        await observer.OnEventAsync(
                new ModelResponseFailed(context.ModelRequestId, sequence, failure, [], usage: null),
                cancellationToken)
            .ConfigureAwait(false);

        return new ModelAttemptFailed(failure, [], usage: null);
    }

    private static ProviderFailure BuildFailure(
        AnthropicResponseParseContext context,
        ProviderFailureKind kind,
        int? statusCode,
        string? providerCode,
        string safeMessage,
        Exception? diagnosticCause) =>
        new(
            kind,
            context.ProviderId,
            context.ProviderRequestId,
            statusCode,
            providerCode,
            retryAfter: null,
            safeMessage,
            diagnosticCause,
            ExtensionData.Empty);

    private static (ContentDelta? Delta, ContentPart Part) BuildBlock(
        AnthropicContentBlockDto block,
        IIdentifierGenerator<ToolCallId> toolCallIdGenerator) =>
        block.Type switch
        {
            "text" => (
                new TextContentDelta(block.Text ?? string.Empty),
                new TextPart(block.Text ?? string.Empty, TextSemantics.Plain, ExtensionData.Empty)),

            "tool_use" => BuildToolUseBlock(block, toolCallIdGenerator),

            "thinking" => (
                new ReasoningContentDelta(block.Thinking ?? string.Empty, ExtensionData.Empty),
                new ReasoningPart(
                    new ReasoningContent(block.Thinking, ReasoningVisibility.Visible, block.Signature, ExtensionData.Empty),
                    ExtensionData.Empty)),

            "redacted_thinking" => (
                null,
                new ReasoningPart(
                    new ReasoningContent(text: null, ReasoningVisibility.Redacted, block.Data, ExtensionData.Empty),
                    ExtensionData.Empty)),

            _ => (null, BuildUnknownPart(block)),
        };

    private static (ContentDelta Delta, ContentPart Part) BuildToolUseBlock(
        AnthropicContentBlockDto block,
        IIdentifierGenerator<ToolCallId> toolCallIdGenerator)
    {
        var callId = toolCallIdGenerator.Create();
        var argumentsElement = block.Input ?? ParseEmptyObject();
        var name = block.Name ?? "unknown";

        var delta = new ToolArgumentsContentDelta(callId, argumentsElement.GetRawText());
        var part = new ToolCallPart(
            callId,
            new ToolReference(new ToolId(name), null, name),
            argumentsElement,
            block.Id is { Length: > 0 } id ? new ProviderToolCallId(id) : null,
            ExtensionData.Empty);

        return (delta, part);
    }

    private static UnknownContentPart BuildUnknownPart(AnthropicContentBlockDto block)
    {
        var payload = JsonSerializer.SerializeToElement(block, _serializerOptions);
        return new UnknownContentPart(block.Type, payload, ExtensionData.Empty);
    }

    private static ExtensionData BuildRawExtension(AnthropicStreamDeltaDto delta)
    {
        var raw = JsonSerializer.SerializeToUtf8Bytes(delta, _serializerOptions);
        return new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add("raw", new ExtensionValue([.. raw])));
    }

    private static JsonElement ParseEmptyObject()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }

    private static ProviderResponseIdentity BuildIdentity(
        AnthropicResponseParseContext context, string? resolvedModel, string? responseId) =>
        new(
            context.ProviderId,
            upstreamProviderId: null,
            context.ApiFamily,
            context.RequestedModelId,
            resolvedModel is { Length: > 0 } model ? new ModelId(model) : context.RequestedModelId,
            context.DeploymentId,
            context.ProviderRequestId,
            responseId is { Length: > 0 } id ? new ProviderResponseId(id) : null);

    private static ModelUsage BuildUsage(AnthropicUsageDto? usage) => BuildUsage(usage, final: null);

    private static ModelUsage BuildUsage(AnthropicUsageDto? initial, AnthropicUsageDto? final)
    {
        if (initial is null && final is null)
        {
            return ModelUsage.Empty;
        }

        var cacheCreationInputTokens = final?.CacheCreationInputTokens ?? initial?.CacheCreationInputTokens;
        var extensions = cacheCreationInputTokens is { } cacheCreation
            ? new ExtensionData(
                ImmutableDictionary<string, ExtensionValue>.Empty.Add(
                    "cache_creation_input_tokens",
                    new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(cacheCreation)])))
            : ExtensionData.Empty;

        return new ModelUsage(
            final?.InputTokens ?? initial?.InputTokens,
            final?.OutputTokens ?? initial?.OutputTokens,
            final?.CacheReadInputTokens ?? initial?.CacheReadInputTokens,
            reasoningTokens: null,
            estimatedCost: null,
            costCurrency: null,
            extensions);
    }

    private static NormalizedStopReason MapStopReason(string? stopReason) =>
        stopReason switch
        {
            "end_turn" or "stop_sequence" => NormalizedStopReason.Completed,
            "max_tokens" => NormalizedStopReason.Length,
            "tool_use" => NormalizedStopReason.ToolUse,
            "pause_turn" => NormalizedStopReason.Deferred,
            "refusal" => NormalizedStopReason.Error,
            null => NormalizedStopReason.Pending,
            _ => NormalizedStopReason.Error,
        };

    /// <summary>
    /// Mutable, private streaming-accumulation state for one in-progress
    /// content block, keyed by its Anthropic content-block index. This is
    /// implementation-internal parsing state, never exposed outside this
    /// class.
    /// </summary>
    private sealed class BlockAccumulator
    {
        public BlockAccumulator(string type) => Type = type;

        public string Type { get; }

        public StringBuilder Text { get; } = new();

        public StringBuilder Json { get; } = new();

        public ToolCallId? ToolCallId { get; set; }

        public string? ToolName { get; set; }

        public string? ProviderCallId { get; set; }

        public string? Signature { get; set; }

        public string? RedactedData { get; set; }

        public AnthropicContentBlockDto? UnknownBlock { get; set; }

        public bool IsClosed { get; private set; }

        public ContentPart? Part { get; private set; }

        public void Close()
        {
            Part = Type switch
            {
                "text" => new TextPart(Text.ToString(), TextSemantics.Plain, ExtensionData.Empty),

                "tool_use" => new ToolCallPart(
                    ToolCallId!.Value,
                    new ToolReference(new ToolId(ToolName ?? "unknown"), null, ToolName ?? "unknown"),
                    ParseArguments(Json.Length > 0 ? Json.ToString() : "{}"),
                    ProviderCallId is { Length: > 0 } id ? new ProviderToolCallId(id) : null,
                    ExtensionData.Empty),

                "thinking" => new ReasoningPart(
                    new ReasoningContent(Text.ToString(), ReasoningVisibility.Visible, Signature, ExtensionData.Empty),
                    ExtensionData.Empty),

                "redacted_thinking" => new ReasoningPart(
                    new ReasoningContent(text: null, ReasoningVisibility.Redacted, RedactedData, ExtensionData.Empty),
                    ExtensionData.Empty),

                _ => new UnknownContentPart(
                    Type,
                    JsonSerializer.SerializeToElement(UnknownBlock, _serializerOptions),
                    ExtensionData.Empty),
            };

            IsClosed = true;
        }

        private static JsonElement ParseArguments(string json)
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }
}
