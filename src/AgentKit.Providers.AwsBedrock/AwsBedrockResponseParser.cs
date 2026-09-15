// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock;

using AgentKit.Providers.AwsBedrock.EventStream;
using AgentKit.Providers.AwsBedrock.Wire;

/// <summary>
/// The default <see cref="IAwsBedrockResponseParser"/>, parsing both
/// buffered JSON and binary AWS event-stream framed Bedrock Converse
/// responses as a typed state machine.
/// </summary>
public sealed class AwsBedrockResponseParser: IAwsBedrockResponseParser
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IIdentifierGenerator<ToolCallId> _toolCallIdGenerator;

    /// <summary>Initializes a new instance of the <see cref="AwsBedrockResponseParser"/> class.</summary>
    /// <param name="toolCallIdGenerator">
    /// Mints the internal <see cref="ToolCallId"/> assigned to each
    /// <c>toolUse</c> block the provider returns.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="toolCallIdGenerator"/> is null.</exception>
    public AwsBedrockResponseParser(IIdentifierGenerator<ToolCallId> toolCallIdGenerator)
    {
        ArgumentNullException.ThrowIfNull(toolCallIdGenerator);
        _toolCallIdGenerator = toolCallIdGenerator;
    }

    /// <inheritdoc/>
    public async Task<ModelAttemptResult> ParseBufferedAsync(
        Stream responseBody,
        AwsBedrockResponseParseContext context,
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

        AwsBedrockConverseResponseDto dto;
        try
        {
            dto = await JsonSerializer
                .DeserializeAsync<AwsBedrockConverseResponseDto>(responseBody, _serializerOptions, cancellationToken)
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
        var blocks = dto.Output?.Message?.Content ?? [];

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
                "The provider returned invalid usage evidence.",
                exception,
                cancellationToken,
                parts.ToImmutable()).ConfigureAwait(false);
        }
        if (dto.Usage is not null)
        {
            await observer.OnEventAsync(new ModelUsageUpdated(requestId, sequence++, usage), cancellationToken)
                .ConfigureAwait(false);
        }

        var response = new ModelResponse(
            requestId,
            BuildIdentity(context),
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
        AwsBedrockResponseParseContext context,
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

        var blocks = new SortedDictionary<int, BlockAccumulator>();
        AwsBedrockTokenUsageDto? usageDto = null;
        string? finalStopReason = null;
        var messageStopReceived = false;

        while (true)
        {
            AwsEventStreamMessage? message;
            try
            {
                message = await AwsEventStreamDecoder.ReadMessageAsync(responseBody, cancellationToken).ConfigureAwait(false);
            }
            catch (AwsEventStreamFormatException exception)
            {
                return await FailAsync(
                    observer,
                    context,
                    sequence,
                    "The provider returned a malformed event-stream frame.",
                    exception,
                    cancellationToken,
                    BuildPartialParts(blocks),
                    TryBuildRetainedUsage(usageDto)).ConfigureAwait(false);
            }

            if (message is null)
            {
                break;
            }

            AwsBedrockStreamEventDto payload;
            try
            {
                payload = JsonSerializer.Deserialize<AwsBedrockStreamEventDto>(message.Payload, _serializerOptions)
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
                    cancellationToken,
                    BuildPartialParts(blocks),
                    TryBuildRetainedUsage(usageDto)).ConfigureAwait(false);
            }

            if (message.MessageType == "exception")
            {
                var failure = BuildFailure(
                    context,
                    AwsBedrockErrorMapping.MapExceptionName(message.ExceptionType),
                    statusCode: null,
                    message.ExceptionType,
                    payload.Message ?? "The provider reported a streaming error.",
                    diagnosticCause: null);

                var partialParts = BuildPartialParts(blocks);
                var retainedUsage = TryBuildRetainedUsage(usageDto);
                await observer.OnEventAsync(
                        new ModelResponseFailed(requestId, sequence++, failure, partialParts, retainedUsage),
                        cancellationToken)
                    .ConfigureAwait(false);
                return new ModelAttemptFailed(failure, partialParts, retainedUsage);
            }

            switch (message.EventType)
            {
                case "contentBlockStart" when payload.ContentBlockIndex is { } startIndex:
                    await HandleContentBlockStartAsync(observer, requestId, startIndex, payload.Start, blocks, () => sequence++, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case "contentBlockDelta" when payload.ContentBlockIndex is { } deltaIndex
                    && blocks.TryGetValue(deltaIndex, out var deltaAccumulator):
                    await HandleContentBlockDeltaAsync(observer, requestId, deltaIndex, payload.Delta, deltaAccumulator, () => sequence++, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case "contentBlockStop" when payload.ContentBlockIndex is { } stopIndex
                    && blocks.TryGetValue(stopIndex, out var stopAccumulator):
                    stopAccumulator.Close();
                    await observer.OnEventAsync(
                            new ModelPartCompleted(requestId, sequence++, stopIndex, stopAccumulator.Part!),
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case "messageStop":
                    finalStopReason = payload.StopReason;
                    messageStopReceived = true;
                    break;

                case "metadata":
                    usageDto = payload.Usage ?? usageDto;
                    break;

                default:
                    // "messageStart" and any other unrecognized event kind carry no
                    // application-visible state beyond what is already captured and
                    // are safely ignored.
                    break;
            }
        }

        if (!messageStopReceived)
        {
            return await FailAsync(
                observer,
                context,
                sequence,
                "The provider's streaming response ended before a messageStop event was received.",
                diagnosticCause: null,
                cancellationToken,
                BuildPartialParts(blocks),
                TryBuildRetainedUsage(usageDto)).ConfigureAwait(false);
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
                    cancellationToken,
                    BuildPartialParts(blocks),
                    TryBuildRetainedUsage(usageDto)).ConfigureAwait(false);
            }

            parts.Add(accumulator.Part!);
        }

        ModelUsage usage;
        try
        {
            usage = BuildUsage(usageDto);
        }
        catch (ArgumentException exception)
        {
            return await FailAsync(
                observer,
                context,
                sequence,
                "The provider returned invalid usage evidence.",
                exception,
                cancellationToken,
                parts.ToImmutable()).ConfigureAwait(false);
        }
        if (usageDto is not null)
        {
            await observer.OnEventAsync(new ModelUsageUpdated(requestId, sequence++, usage), cancellationToken)
                .ConfigureAwait(false);
        }

        var response = new ModelResponse(
            requestId,
            BuildIdentity(context),
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
        AwsBedrockContentBlockStartDto? start,
        SortedDictionary<int, BlockAccumulator> blocks,
        Func<long> nextSequence,
        CancellationToken cancellationToken)
    {
        var accumulator = new BlockAccumulator();

        if (start?.ToolUse is { } toolUseStart)
        {
            accumulator.Kind = BlockKind.ToolUse;
            accumulator.ToolCallId = _toolCallIdGenerator.Create();
            accumulator.ToolName = toolUseStart.Name;
            accumulator.ProviderCallId = toolUseStart.ToolUseId;
        }

        blocks[index] = accumulator;
        await observer.OnEventAsync(new ModelPartStarted(requestId, nextSequence(), index), cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task HandleContentBlockDeltaAsync(
        IModelResponseObserver observer,
        ModelRequestId requestId,
        int index,
        AwsBedrockContentBlockDeltaDto? delta,
        BlockAccumulator accumulator,
        Func<long> nextSequence,
        CancellationToken cancellationToken)
    {
        if (delta?.Text is { Length: > 0 } textFragment)
        {
            _ = accumulator.Text.Append(textFragment);
            await observer.OnEventAsync(
                    new ModelPartDelta(requestId, nextSequence(), index, new TextContentDelta(textFragment)),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (delta?.ToolUse?.Input is { Length: > 0 } jsonFragment)
        {
            _ = accumulator.Json.Append(jsonFragment);
            await observer.OnEventAsync(
                    new ModelPartDelta(
                        requestId,
                        nextSequence(),
                        index,
                        new ToolArgumentsContentDelta(accumulator.ToolCallId!.Value, jsonFragment)),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Collects the truthful partial output of an interrupted stream: every content block in index order,
    /// using the closed block's final part when it has one and otherwise materializing the block's open
    /// accumulator as far as it was received.
    /// </summary>
    /// <param name="blocks">The streaming block accumulators keyed by Converse content-block index.</param>
    /// <returns>
    /// The parts in block-index order. An open tool-use block whose accumulated input is not yet complete JSON
    /// cannot be represented truthfully as a <see cref="ToolCallPart"/> and is omitted.
    /// </returns>
    private static ImmutableArray<ContentPart> BuildPartialParts(SortedDictionary<int, BlockAccumulator> blocks)
    {
        Debug.Assert(blocks is not null, "The streaming state machine always owns the block map.");

        var partial = ImmutableArray.CreateBuilder<ContentPart>();
        foreach (var accumulator in blocks.Values)
        {
            var part = accumulator.IsClosed ? accumulator.Part : accumulator.TryBuildPartialPart();
            if (part is not null)
            {
                partial.Add(part);
            }
        }

        return partial.ToImmutable();
    }

    /// <summary>
    /// Builds the usage retained so far for an interrupted stream, or null when the provider has not reported any
    /// usage or the reported values are not valid evidence; a failure exit never masks its own cause with a
    /// secondary usage-validation failure.
    /// </summary>
    /// <param name="usage">The latest <c>metadata</c> usage received, when any.</param>
    /// <returns>The retained usage evidence, or null when none can be truthfully reported.</returns>
    private static ModelUsage? TryBuildRetainedUsage(AwsBedrockTokenUsageDto? usage)
    {
        if (usage is null)
        {
            return null;
        }

        try
        {
            return BuildUsage(usage);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static async Task<ModelAttemptResult> FailAsync(
        IModelResponseObserver observer,
        AwsBedrockResponseParseContext context,
        long sequence,
        string safeMessage,
        Exception? diagnosticCause,
        CancellationToken cancellationToken,
        ImmutableArray<ContentPart> partialParts = default,
        ModelUsage? usage = null)
    {
        var normalizedPartialParts = partialParts.IsDefault ? [] : partialParts;
        var failure = BuildFailure(
            context,
            ProviderFailureKind.ProtocolViolation,
            statusCode: null,
            providerCode: null,
            safeMessage,
            diagnosticCause);

        await observer.OnEventAsync(
                new ModelResponseFailed(context.ModelRequestId, sequence, failure, normalizedPartialParts, usage),
                cancellationToken)
            .ConfigureAwait(false);

        return new ModelAttemptFailed(failure, normalizedPartialParts, usage);
    }

    private static ProviderFailure BuildFailure(
        AwsBedrockResponseParseContext context,
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
        AwsBedrockContentBlockDto block,
        IIdentifierGenerator<ToolCallId> toolCallIdGenerator)
    {
        if (block.Text is { } text)
        {
            return (new TextContentDelta(text), new TextPart(text, TextSemantics.Plain, ExtensionData.Empty));
        }

        return block.ToolUse is { } toolUse ? BuildToolUseBlock(toolUse, toolCallIdGenerator) : ((ContentDelta? Delta, ContentPart Part)) (null, BuildUnknownPart(block));
    }

    private static (ContentDelta Delta, ContentPart Part) BuildToolUseBlock(
        AwsBedrockToolUseBlockDto toolUse,
        IIdentifierGenerator<ToolCallId> toolCallIdGenerator)
    {
        var callId = toolCallIdGenerator.Create();
        var argumentsElement = toolUse.Input ?? ParseEmptyObject();
        var name = toolUse.Name ?? "unknown";

        var delta = new ToolArgumentsContentDelta(callId, argumentsElement.GetRawText());
        var part = new ToolCallPart(
            callId,
            new ToolReference(new ToolId(name), null, name),
            argumentsElement,
            toolUse.ToolUseId is { Length: > 0 } id ? new ProviderToolCallId(id) : null,
            ExtensionData.Empty);

        return (delta, part);
    }

    private static UnknownContentPart BuildUnknownPart(AwsBedrockContentBlockDto block)
    {
        var payload = JsonSerializer.SerializeToElement(block, _serializerOptions);
        return new UnknownContentPart("unknown", payload, ExtensionData.Empty);
    }

    private static JsonElement ParseEmptyObject()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }

    private static ProviderResponseIdentity BuildIdentity(AwsBedrockResponseParseContext context) =>
        new(
            context.ProviderId,
            upstreamProviderId: null,
            context.ApiFamily,
            context.RequestedModelId,
            context.RequestedModelId,
            context.DeploymentId,
            context.ProviderRequestId,
            responseId: null);

    private static ModelUsage BuildUsage(AwsBedrockTokenUsageDto? usage)
    {
        if (usage is null)
        {
            return ModelUsage.NotReported;
        }

        var extensions = usage.CacheWriteInputTokens is { } cacheWrite
            ? new ExtensionData(
                ImmutableDictionary<string, ExtensionValue>.Empty.Add(
                    "cache_write_input_tokens",
                    new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(cacheWrite)])))
            : ExtensionData.Empty;

        return new ModelUsage(ModelUsageReportState.Final,
            usage.InputTokens,
            usage.OutputTokens,
            usage.CacheReadInputTokens,
            reasoningTokens: null,
            estimatedCost: null,
            costCurrency: null,
            extensions);
    }

    private static NormalizedStopReason MapStopReason(string? stopReason) =>
        stopReason switch
        {
            "end_turn" or "stop_sequence" => NormalizedStopReason.Completed,
            "max_tokens" or "model_context_window_exceeded" => NormalizedStopReason.Length,
            "tool_use" => NormalizedStopReason.ToolUse,
            "guardrail_intervened" or "content_filtered" or "malformed_model_output" or "malformed_tool_use" =>
                NormalizedStopReason.Error,
            null => NormalizedStopReason.Pending,
            _ => NormalizedStopReason.Error,
        };

    /// <summary>
    /// Mutable, private streaming-accumulation state for one in-progress
    /// content block, keyed by its Converse content-block index. This is
    /// implementation-internal parsing state, never exposed outside this
    /// class.
    /// </summary>
    private sealed class BlockAccumulator
    {
        public BlockKind Kind { get; set; } = BlockKind.Text;

        public StringBuilder Text { get; } = new();

        public StringBuilder Json { get; } = new();

        public ToolCallId? ToolCallId { get; set; }

        public string? ToolName { get; set; }

        public string? ProviderCallId { get; set; }

        public bool IsClosed { get; private set; }

        public ContentPart? Part { get; private set; }

        public void Close()
        {
            Part = BuildPart();
            IsClosed = true;
        }

        /// <summary>
        /// Materializes the block as received so far without closing it, for reporting the partial output of an
        /// interrupted stream.
        /// </summary>
        /// <returns>The part built from the accumulated state, or null when a tool-use block's accumulated input is not yet complete JSON.</returns>
        public ContentPart? TryBuildPartialPart()
        {
            try
            {
                return BuildPart();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private ContentPart BuildPart() =>
            Kind switch
            {
                BlockKind.ToolUse => new ToolCallPart(
                    ToolCallId!.Value,
                    new ToolReference(new ToolId(ToolName ?? "unknown"), null, ToolName ?? "unknown"),
                    ParseArguments(Json.Length > 0 ? Json.ToString() : "{}"),
                    ProviderCallId is { Length: > 0 } id ? new ProviderToolCallId(id) : null,
                    ExtensionData.Empty),

                BlockKind.Text => new TextPart(Text.ToString(), TextSemantics.Plain, ExtensionData.Empty),

                _ => throw new UnreachableException($"Unrecognized {nameof(BlockKind)} value '{Kind}'."),
            };

        private static JsonElement ParseArguments(string json)
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }

    /// <summary>The discriminated kind of an in-progress streaming content block.</summary>
    private enum BlockKind
    {
        /// <summary>A plain text content block.</summary>
        Text,

        /// <summary>A tool call request content block.</summary>
        ToolUse,
    }
}
