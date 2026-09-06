// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible;

using AgentKit.Providers.OpenAICompatible.Wire;

/// <summary>
/// The default <see cref="IOpenAIStreamParser"/>, parsing both buffered and
/// server-sent-events OpenAI-compatible chat completion responses as a
/// typed state machine.
/// </summary>
public sealed class OpenAIChatCompletionResponseParser: IOpenAIStreamParser
{
    private const int _textPartIndex = 0;

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IIdentifierGenerator<ToolCallId> _toolCallIdGenerator;

    /// <summary>Initializes a new instance of the <see cref="OpenAIChatCompletionResponseParser"/> class.</summary>
    /// <param name="toolCallIdGenerator">
    /// Mints the internal <see cref="ToolCallId"/> assigned to each tool
    /// call the provider requests.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="toolCallIdGenerator"/> is null.</exception>
    public OpenAIChatCompletionResponseParser(IIdentifierGenerator<ToolCallId> toolCallIdGenerator)
    {
        ArgumentNullException.ThrowIfNull(toolCallIdGenerator);
        _toolCallIdGenerator = toolCallIdGenerator;
    }

    /// <inheritdoc/>
    public async Task<ModelAttemptResult> ParseBufferedAsync(
        Stream responseBody,
        OpenAIResponseParseContext context,
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

        OpenAIChatCompletionResponse dto;
        try
        {
            dto = await JsonSerializer
                .DeserializeAsync<OpenAIChatCompletionResponse>(responseBody, _serializerOptions, cancellationToken)
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

        if (dto.Choices.Count == 0)
        {
            return await FailAsync(
                observer,
                context,
                sequence,
                "The provider returned a response with no choices.",
                diagnosticCause: null,
                cancellationToken).ConfigureAwait(false);
        }

        var choice = dto.Choices[0];
        var parts = ImmutableArray.CreateBuilder<ContentPart>();

        if (!string.IsNullOrEmpty(choice.Message.Content))
        {
            await observer.OnEventAsync(new ModelPartStarted(requestId, sequence++, _textPartIndex), cancellationToken)
                .ConfigureAwait(false);
            await observer.OnEventAsync(
                    new ModelPartDelta(requestId, sequence++, _textPartIndex, new TextContentDelta(choice.Message.Content)),
                    cancellationToken)
                .ConfigureAwait(false);

            var textPart = new TextPart(choice.Message.Content, TextSemantics.Plain, ExtensionData.Empty);
            await observer.OnEventAsync(new ModelPartCompleted(requestId, sequence++, _textPartIndex, textPart), cancellationToken)
                .ConfigureAwait(false);
            parts.Add(textPart);
        }

        if (choice.Message.ToolCalls is { Count: > 0 } toolCalls)
        {
            for (var wireIndex = 0; wireIndex < toolCalls.Count; wireIndex++)
            {
                var toolCall = toolCalls[wireIndex];
                var partIndex = ToolCallPartIndex(wireIndex);
                var callId = _toolCallIdGenerator.Create();

                await observer.OnEventAsync(new ModelPartStarted(requestId, sequence++, partIndex), cancellationToken)
                    .ConfigureAwait(false);
                await observer.OnEventAsync(
                        new ModelPartDelta(
                            requestId,
                            sequence++,
                            partIndex,
                            new ToolArgumentsContentDelta(callId, toolCall.Function.Arguments)),
                        cancellationToken)
                    .ConfigureAwait(false);

                var toolCallPart = new ToolCallPart(
                    callId,
                    new ToolReference(new ToolId(toolCall.Function.Name), null, toolCall.Function.Name),
                    ParseArgumentsOrEmpty(toolCall.Function.Arguments),
                    new ProviderToolCallId(toolCall.Id),
                    ExtensionData.Empty);

                await observer.OnEventAsync(new ModelPartCompleted(requestId, sequence++, partIndex, toolCallPart), cancellationToken)
                    .ConfigureAwait(false);
                parts.Add(toolCallPart);
            }
        }

        var usage = MapUsage(dto.Usage);
        await observer.OnEventAsync(new ModelUsageUpdated(requestId, sequence++, usage), cancellationToken)
            .ConfigureAwait(false);

        var response = new ModelResponse(
            requestId,
            BuildIdentity(context, dto.Model, dto.Id),
            parts.ToImmutable(),
            MapStopReason(choice.FinishReason),
            usage,
            ExtensionData.Empty);

        await observer.OnEventAsync(new ModelResponseCompleted(requestId, sequence++, response), cancellationToken)
            .ConfigureAwait(false);

        return new ModelAttemptCompleted(response);
    }

    /// <inheritdoc/>
    public async Task<ModelAttemptResult> ParseStreamingAsync(
        Stream responseBody,
        OpenAIResponseParseContext context,
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

        var textBuilder = new StringBuilder();
        var textPartOpen = false;
        var toolCallSlots = new SortedDictionary<int, ToolCallAccumulator>();
        string? finishReason = null;
        string? resolvedModel = null;
        string? responseId = null;
        OpenAIUsage? usageDto = null;

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (line.Length == 0 || !line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var payload = line["data:".Length..].TrimStart();
            if (payload is "[DONE]")
            {
                break;
            }

            OpenAIChatCompletionChunk chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<OpenAIChatCompletionChunk>(payload, _serializerOptions)
                    ?? throw new JsonException("The chunk payload deserialized to a null value.");
            }
            catch (JsonException exception)
            {
                return await FailAsync(
                    observer,
                    context,
                    sequence,
                    "The provider returned a malformed streaming chunk.",
                    exception,
                    cancellationToken).ConfigureAwait(false);
            }

            resolvedModel ??= chunk.Model;
            responseId ??= chunk.Id;

            if (chunk.Usage is not null)
            {
                usageDto = chunk.Usage;
            }

            if (chunk.Choices is not { Count: > 0 })
            {
                continue;
            }

            var choice = chunk.Choices[0];
            if (choice.FinishReason is not null)
            {
                finishReason = choice.FinishReason;
            }

            var delta = choice.Delta;
            if (delta is null)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(delta.Content))
            {
                if (!textPartOpen)
                {
                    textPartOpen = true;
                    await observer.OnEventAsync(new ModelPartStarted(requestId, sequence++, _textPartIndex), cancellationToken)
                        .ConfigureAwait(false);
                }

                await observer.OnEventAsync(
                        new ModelPartDelta(requestId, sequence++, _textPartIndex, new TextContentDelta(delta.Content)),
                        cancellationToken)
                    .ConfigureAwait(false);
                _ = textBuilder.Append(delta.Content);
            }

            if (delta.ToolCalls is not { Count: > 0 } toolCallDeltas)
            {
                continue;
            }

            foreach (var toolCallDelta in toolCallDeltas)
            {
                if (!toolCallSlots.TryGetValue(toolCallDelta.Index, out var slot))
                {
                    slot = new ToolCallAccumulator(_toolCallIdGenerator.Create());
                    toolCallSlots[toolCallDelta.Index] = slot;
                    await observer.OnEventAsync(
                            new ModelPartStarted(requestId, sequence++, ToolCallPartIndex(toolCallDelta.Index)),
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                if (toolCallDelta.Id is { Length: > 0 } id)
                {
                    slot.ProviderCallId = id;
                }

                if (toolCallDelta.Function?.Name is { Length: > 0 } name)
                {
                    slot.ToolName ??= name;
                }

                if (toolCallDelta.Function?.Arguments is { Length: > 0 } argumentsFragment)
                {
                    await observer.OnEventAsync(
                            new ModelPartDelta(
                                requestId,
                                sequence++,
                                ToolCallPartIndex(toolCallDelta.Index),
                                new ToolArgumentsContentDelta(slot.CallId, argumentsFragment)),
                            cancellationToken)
                        .ConfigureAwait(false);
                    _ = slot.Arguments.Append(argumentsFragment);
                }
            }
        }

        if (finishReason is null)
        {
            return await FailAsync(
                observer,
                context,
                sequence,
                "The provider's streaming response ended before a finish reason was received.",
                diagnosticCause: null,
                cancellationToken).ConfigureAwait(false);
        }

        var parts = ImmutableArray.CreateBuilder<ContentPart>();

        if (textPartOpen)
        {
            var textPart = new TextPart(textBuilder.ToString(), TextSemantics.Plain, ExtensionData.Empty);
            await observer.OnEventAsync(new ModelPartCompleted(requestId, sequence++, _textPartIndex, textPart), cancellationToken)
                .ConfigureAwait(false);
            parts.Add(textPart);
        }

        foreach (var (wireIndex, slot) in toolCallSlots)
        {
            var toolName = slot.ToolName ?? slot.ProviderCallId ?? "unknown";
            var argumentsJson = slot.Arguments.Length > 0 ? slot.Arguments.ToString() : "{}";

            var toolCallPart = new ToolCallPart(
                slot.CallId,
                new ToolReference(new ToolId(toolName), null, toolName),
                ParseArgumentsOrEmpty(argumentsJson),
                slot.ProviderCallId is { } providerCallId ? new ProviderToolCallId(providerCallId) : null,
                ExtensionData.Empty);

            await observer.OnEventAsync(
                    new ModelPartCompleted(requestId, sequence++, ToolCallPartIndex(wireIndex), toolCallPart),
                    cancellationToken)
                .ConfigureAwait(false);
            parts.Add(toolCallPart);
        }

        var usage = MapUsage(usageDto);
        if (usageDto is not null)
        {
            await observer.OnEventAsync(new ModelUsageUpdated(requestId, sequence++, usage), cancellationToken)
                .ConfigureAwait(false);
        }

        var response = new ModelResponse(
            requestId,
            BuildIdentity(context, resolvedModel, responseId),
            parts.ToImmutable(),
            MapStopReason(finishReason),
            usage,
            ExtensionData.Empty);

        await observer.OnEventAsync(new ModelResponseCompleted(requestId, sequence++, response), cancellationToken)
            .ConfigureAwait(false);

        return new ModelAttemptCompleted(response);
    }

    private static async Task<ModelAttemptResult> FailAsync(
        IModelResponseObserver observer,
        OpenAIResponseParseContext context,
        long sequence,
        string safeMessage,
        Exception? diagnosticCause,
        CancellationToken cancellationToken)
    {
        var failure = new ProviderFailure(
            ProviderFailureKind.ProtocolViolation,
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

    private static ProviderResponseIdentity BuildIdentity(
        OpenAIResponseParseContext context, string? resolvedModel, string? responseId) =>
        new(
            context.ProviderId,
            upstreamProviderId: null,
            context.ApiFamily,
            context.RequestedModelId,
            resolvedModel is { Length: > 0 } model ? new ModelId(model) : context.RequestedModelId,
            context.DeploymentId,
            context.ProviderRequestId,
            responseId is { Length: > 0 } id ? new ProviderResponseId(id) : null);

    private static ModelUsage MapUsage(OpenAIUsage? usage) =>
        usage is null
            ? ModelUsage.Empty
            : new ModelUsage(
                usage.PromptTokens,
                usage.CompletionTokens,
                usage.PromptTokensDetails?.CachedTokens,
                usage.CompletionTokensDetails?.ReasoningTokens,
                estimatedCost: null,
                costCurrency: null,
                ExtensionData.Empty);

    private static NormalizedStopReason MapStopReason(string? finishReason) =>
        finishReason switch
        {
            "stop" => NormalizedStopReason.Completed,
            "length" => NormalizedStopReason.Length,
            "tool_calls" or "function_call" => NormalizedStopReason.ToolUse,
            "content_filter" => NormalizedStopReason.Error,
            null => NormalizedStopReason.Pending,
            _ => NormalizedStopReason.Error,
        };

    private static JsonElement ParseArgumentsOrEmpty(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            json = "{}";
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static int ToolCallPartIndex(int wireIndex) => wireIndex + 1;

    /// <summary>
    /// Mutable, private streaming-accumulation state for one in-progress
    /// tool call slot, keyed by its wire-level index. This is
    /// implementation-internal parsing state, never exposed outside this
    /// class.
    /// </summary>
    private sealed class ToolCallAccumulator
    {
        /// <summary>Initializes a new instance of the <see cref="ToolCallAccumulator"/> class.</summary>
        /// <param name="callId">The internal call identity minted for this slot.</param>
        public ToolCallAccumulator(ToolCallId callId) => CallId = callId;

        /// <summary>Gets the internal call identity minted for this slot.</summary>
        public ToolCallId CallId { get; }

        /// <summary>Gets or sets the provider-supplied call identifier, once seen.</summary>
        public string? ProviderCallId { get; set; }

        /// <summary>Gets or sets the tool/function name, once seen.</summary>
        public string? ToolName { get; set; }

        /// <summary>Gets the accumulated raw JSON argument text.</summary>
        public StringBuilder Arguments { get; } = new();
    }
}
