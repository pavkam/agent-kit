// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI;

using AgentKit.Providers.Http;
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

        List<(ContentDelta? Delta, ContentPart Part)> built;
        try
        {
            built = BuildParts(choice.Message, _toolCallIdGenerator);
        }
        catch (JsonException exception)
        {
            // Malformed tool-call arguments must never be replaced by an empty object: that would synthesize
            // a well-formed call the model never made.
            return await FailAsync(
                observer,
                context,
                sequence,
                ProviderFailureKind.ProtocolViolation,
                "The provider returned malformed tool-call arguments.",
                exception,
                cancellationToken).ConfigureAwait(false);
        }

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

        var state = new StreamState();
        string? resolvedModel = null;
        string? responseId = null;
        MistralAIUsageDto? usage = null;
        var usageIsFinal = false;
        string? finishReason = null;
        var sawDoneSentinel = false;

        await foreach (var streamEvent in ServerSentEventReader.ReadAsync(responseBody, cancellationToken).ConfigureAwait(false))
        {
            var payload = streamEvent.Data;
            if (payload.Length == 0)
            {
                continue;
            }

            // The [DONE] sentinel is a Chat Completions dialect convention layered over SSE, so the shared reader
            // leaves it in the payload and this parser recognizes it.
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
                    cancellationToken,
                    BuildPartialParts(state),
                    TryBuildRetainedUsage(usage, usageIsFinal)).ConfigureAwait(false);
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
                cancellationToken,
                BuildPartialParts(state),
                TryBuildRetainedUsage(usage, usageIsFinal)).ConfigureAwait(false);
        }

        var finalParts = ImmutableArray.CreateBuilder<ContentPart>();
        foreach (var slot in state.Slots)
        {
            if (!slot.Closed)
            {
                if (!HasMaterializableToolName(slot))
                {
                    return await FailAsync(
                        observer,
                        context,
                        sequence,
                        ProviderFailureKind.ProtocolViolation,
                        "The provider streamed a tool call without a function name.",
                        diagnosticCause: null,
                        cancellationToken,
                        BuildPartialParts(state),
                        TryBuildRetainedUsage(usage, usageIsFinal)).ConfigureAwait(false);
                }

                try
                {
                    slot.FinalPart = MaterializeSlot(slot);
                }
                catch (JsonException exception)
                {
                    return await FailAsync(
                        observer,
                        context,
                        sequence,
                        ProviderFailureKind.ProtocolViolation,
                        "The provider returned malformed tool-call arguments.",
                        exception,
                        cancellationToken,
                        BuildPartialParts(state),
                        TryBuildRetainedUsage(usage, usageIsFinal)).ConfigureAwait(false);
                }

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
                cancellationToken,
                finalParts.ToImmutable()).ConfigureAwait(false);
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

    /// <summary>
    /// Determines whether a slot can be materialized as far as its tool name is concerned: text and unknown slots
    /// always can, and a tool-call slot can only once at least one chunk carried a nonblank function name.
    /// </summary>
    /// <param name="slot">The slot to inspect.</param>
    /// <returns><see langword="false"/> only for a tool-call slot whose name never arrived.</returns>
    private static bool HasMaterializableToolName(Slot slot)
    {
        Debug.Assert(slot is not null, "Callers inspect an existing slot.");
        return slot.Kind != SlotKind.ToolCall || !string.IsNullOrWhiteSpace(slot.ToolCallName);
    }

    /// <summary>Builds the content part an open slot currently represents.</summary>
    /// <param name="slot">A slot that has not been closed; unknown-kind slots close at creation and never reach this method open.</param>
    /// <returns>The part built from the accumulated state.</returns>
    /// <exception cref="JsonException">A tool-call slot's accumulated arguments are not valid JSON.</exception>
    private static ContentPart MaterializeSlot(Slot slot)
    {
        Debug.Assert(slot is not null, "Callers materialize an existing slot.");
        Debug.Assert(!slot.Closed, "Closed slots already carry their final part.");
        Debug.Assert(HasMaterializableToolName(slot), "Callers reject or omit a tool-call slot whose name never arrived.");

        return slot.Kind switch
        {
            SlotKind.Text => new TextPart(slot.Text.ToString(), TextSemantics.Plain, ExtensionData.Empty),
            SlotKind.ToolCall => new ToolCallPart(
                slot.AssignedCallId,
                new ToolReference(new ToolId(slot.ToolCallName!), null, slot.ToolCallName!),
                ParseArguments(slot.ToolCallArguments.ToString()),
                slot.ToolCallId is { Length: > 0 } id ? new ProviderToolCallId(id) : null,
                ExtensionData.Empty),
            SlotKind.Unknown => throw new UnreachableException("An unknown-kind slot is always closed at creation."),
            _ => throw new UnreachableException($"Unrecognized {nameof(SlotKind)} value '{slot.Kind}'."),
        };
    }

    /// <summary>
    /// Collects the truthful partial output of an interrupted stream: every slot in part order, using the
    /// closed slot's final part when it has one and otherwise materializing the slot as far as it was received.
    /// </summary>
    /// <param name="state">The streaming state owning the slots.</param>
    /// <returns>
    /// The parts in part order. An open tool-call slot whose accumulated arguments are not yet complete JSON, or
    /// whose function name never arrived, cannot be represented truthfully as a <see cref="ToolCallPart"/> and is
    /// omitted.
    /// </returns>
    private static ImmutableArray<ContentPart> BuildPartialParts(StreamState state)
    {
        Debug.Assert(state is not null, "The streaming state machine always owns its state.");

        var partial = ImmutableArray.CreateBuilder<ContentPart>(state.Slots.Count);
        foreach (var slot in state.Slots)
        {
            if (slot.Closed)
            {
                partial.Add(slot.FinalPart!);
                continue;
            }

            if (!HasMaterializableToolName(slot))
            {
                // A tool call that never received its name has no truthful identity; it is omitted rather than fabricated.
                continue;
            }

            try
            {
                partial.Add(MaterializeSlot(slot));
            }
            catch (JsonException)
            {
                // An unfinished tool call has no truthful argument object yet; it is omitted rather than fabricated.
            }
        }

        return partial.ToImmutable();
    }

    /// <summary>
    /// Builds the usage retained so far for an interrupted stream, or null when the provider has not reported any
    /// usage or the reported values are not valid evidence; a failure exit never masks its own cause with a
    /// secondary usage-validation failure.
    /// </summary>
    /// <param name="usage">The latest chunk usage received, when any.</param>
    /// <param name="usageIsFinal">Whether that usage accompanied a finish reason.</param>
    /// <returns>The retained usage evidence, or null when none can be truthfully reported.</returns>
    private static ModelUsage? TryBuildRetainedUsage(MistralAIUsageDto? usage, bool usageIsFinal)
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
        MistralAIResponseParseContext context,
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

    /// <summary>Parses tool-call argument JSON; an absent value is an empty object, malformed JSON propagates as <see cref="JsonException"/>.</summary>
    private static JsonElement ParseArguments(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return ParseEmptyObject();
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
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
