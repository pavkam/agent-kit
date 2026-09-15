// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere;

using AgentKit.Providers.Cohere.Wire;

/// <summary>
/// The default <see cref="ICohereResponseParser"/>, parsing both buffered
/// and server-sent-events Cohere v2 Chat responses as a typed state
/// machine.
/// </summary>
/// <remarks>
/// Unlike the other native AgentKit provider integrations, Cohere's
/// streaming transport is a semantic event grammar rather than
/// small growing-response or OpenAI-style choice deltas: each content
/// block and tool call is explicitly bracketed by its own
/// <c>*-start</c>/<c>*-delta</c>/<c>*-end</c> triplet, correlated by an
/// <c>index</c> field, and the stream is authoritative-complete once a
/// <c>message-end</c> event arrives. This parser does not translate
/// citation events, the tool-use <c>tool_plan</c> narrative, or unmodeled
/// content-block kinds beyond wrapping the last one as an
/// <see cref="UnknownContentPart"/>.
/// </remarks>
public sealed class CohereResponseParser: ICohereResponseParser
{
    private const string _unknownBlockTypeName = "unknownBlock";

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IIdentifierGenerator<ToolCallId> _toolCallIdGenerator;

    /// <summary>Initializes a new instance of the <see cref="CohereResponseParser"/> class.</summary>
    /// <param name="toolCallIdGenerator">
    /// Mints the internal <see cref="ToolCallId"/> assigned to each tool
    /// call the provider returns.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="toolCallIdGenerator"/> is null.</exception>
    public CohereResponseParser(IIdentifierGenerator<ToolCallId> toolCallIdGenerator)
    {
        ArgumentNullException.ThrowIfNull(toolCallIdGenerator);
        _toolCallIdGenerator = toolCallIdGenerator;
    }

    /// <inheritdoc/>
    public async Task<ModelAttemptResult> ParseBufferedAsync(
        Stream responseBody,
        CohereResponseParseContext context,
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

        CohereChatResponseDto dto;
        try
        {
            dto = await JsonSerializer
                .DeserializeAsync<CohereChatResponseDto>(responseBody, _serializerOptions, cancellationToken)
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

        if (dto.Message is null)
        {
            return await FailAsync(
                observer,
                context,
                sequence,
                ProviderFailureKind.ProtocolViolation,
                "The provider returned a response with no message.",
                diagnosticCause: null,
                cancellationToken).ConfigureAwait(false);
        }

        List<(ContentDelta? Delta, ContentPart Part)> built;
        try
        {
            built = BuildParts(dto.Message, _toolCallIdGenerator);
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
            BuildIdentity(context, dto.Id),
            parts.ToImmutable(),
            MapFinishReason(dto.FinishReason),
            usage,
            ExtensionData.Empty);

        await observer.OnEventAsync(new ModelResponseCompleted(requestId, sequence++, response), cancellationToken)
            .ConfigureAwait(false);

        return new ModelAttemptCompleted(response);
    }

    /// <inheritdoc/>
    public async Task<ModelAttemptResult> ParseStreamingAsync(
        Stream responseBody,
        CohereResponseParseContext context,
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
        string? messageId = null;
        string? finishReason = null;
        CohereUsageDto? usage = null;
        var sawMessageEnd = false;

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

            CohereStreamEventDto streamEvent;
            try
            {
                streamEvent = JsonSerializer.Deserialize<CohereStreamEventDto>(payload, _serializerOptions)
                    ?? throw new JsonException("The event payload deserialized to a null value.");
            }
            catch (JsonException exception)
            {
                return await FailAsync(
                    observer,
                    context,
                    sequence,
                    ProviderFailureKind.ProtocolViolation,
                    "The provider returned a malformed streaming event.",
                    exception,
                    cancellationToken,
                    BuildPartialParts(state)).ConfigureAwait(false);
            }

            switch (streamEvent.Type)
            {
                case "message-start":
                    messageId ??= streamEvent.Id;
                    break;

                case "content-start":
                    sequence = await HandleContentStartAsync(observer, requestId, sequence, streamEvent, state, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case "content-delta":
                    sequence = await HandleContentDeltaAsync(observer, requestId, sequence, streamEvent, state, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case "content-end":
                    sequence = await HandleContentEndAsync(observer, requestId, sequence, streamEvent, state, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case "tool-call-start":
                    sequence = await HandleToolCallStartAsync(
                            observer, requestId, sequence, streamEvent, state, _toolCallIdGenerator, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case "tool-call-delta":
                    sequence = await HandleToolCallDeltaAsync(
                            observer, requestId, sequence, streamEvent, state, _toolCallIdGenerator, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case "tool-call-end":
                    if (TryGetOpenNamelessToolCall(state, streamEvent.Index ?? 0))
                    {
                        return await FailAsync(
                            observer,
                            context,
                            sequence,
                            ProviderFailureKind.ProtocolViolation,
                            "The provider streamed a tool call without a function name.",
                            diagnosticCause: null,
                            cancellationToken,
                            BuildPartialParts(state)).ConfigureAwait(false);
                    }

                    try
                    {
                        sequence = await HandleToolCallEndAsync(observer, requestId, sequence, streamEvent, state, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (JsonException exception)
                    {
                        // Malformed accumulated arguments are a typed protocol failure; they are never replaced by {}.
                        return await FailAsync(
                            observer,
                            context,
                            sequence,
                            ProviderFailureKind.ProtocolViolation,
                            "The provider returned malformed tool-call arguments.",
                            exception,
                            cancellationToken,
                            BuildPartialParts(state)).ConfigureAwait(false);
                    }

                    break;

                case "message-end":
                    finishReason = streamEvent.Delta?.FinishReason;
                    usage = streamEvent.Delta?.Usage ?? usage;
                    sawMessageEnd = true;
                    break;

                default:
                    // tool-plan-delta, citation-start, citation-end, and any future
                    // event kind are known, documented scope gaps: silently ignored.
                    break;
            }

            if (sawMessageEnd)
            {
                break;
            }
        }

        if (!sawMessageEnd)
        {
            return await FailAsync(
                observer,
                context,
                sequence,
                ProviderFailureKind.ProtocolViolation,
                "The provider's streaming response ended before a message-end event was received.",
                diagnosticCause: null,
                cancellationToken,
                BuildPartialParts(state)).ConfigureAwait(false);
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
                        BuildPartialParts(state)).ConfigureAwait(false);
                }

                try
                {
                    sequence = await FinalizeSlotAsync(observer, requestId, sequence, slot, cancellationToken).ConfigureAwait(false);
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
                        BuildPartialParts(state)).ConfigureAwait(false);
                }
            }

            finalParts.Add(slot.FinalPart!);
        }

        ModelUsage usageResult;
        try
        {
            usageResult = BuildUsage(usage);
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
            BuildIdentity(context, messageId),
            finalParts.ToImmutable(),
            MapFinishReason(finishReason),
            usageResult,
            ExtensionData.Empty);

        await observer.OnEventAsync(new ModelResponseCompleted(requestId, sequence++, response), cancellationToken)
            .ConfigureAwait(false);

        return new ModelAttemptCompleted(response);
    }

    private static async Task<long> HandleContentStartAsync(
        IModelResponseObserver observer,
        ModelRequestId requestId,
        long sequence,
        CohereStreamEventDto streamEvent,
        StreamState state,
        CancellationToken cancellationToken)
    {
        var wireIndex = streamEvent.Index ?? 0;
        var block = streamEvent.Delta?.Message?.Content;
        var kind = block?.Type switch
        {
            "text" => SlotKind.Text,
            "thinking" => SlotKind.Reasoning,
            _ => SlotKind.Unknown,
        };

        var slot = new Slot { Kind = kind, PartIndex = state.Slots.Count, UnknownSnapshot = block };
        state.Slots.Add(slot);
        state.ContentSlotsByWireIndex[wireIndex] = slot;

        await observer.OnEventAsync(new ModelPartStarted(requestId, sequence++, slot.PartIndex), cancellationToken)
            .ConfigureAwait(false);

        return await AppendContentFragmentAsync(observer, requestId, sequence, slot, block, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<long> HandleContentDeltaAsync(
        IModelResponseObserver observer,
        ModelRequestId requestId,
        long sequence,
        CohereStreamEventDto streamEvent,
        StreamState state,
        CancellationToken cancellationToken)
    {
        var wireIndex = streamEvent.Index ?? 0;
        var block = streamEvent.Delta?.Message?.Content;

        if (!state.ContentSlotsByWireIndex.TryGetValue(wireIndex, out var slot))
        {
            var kind = block?.Thinking is not null ? SlotKind.Reasoning : block?.Text is not null ? SlotKind.Text : SlotKind.Unknown;
            slot = new Slot { Kind = kind, PartIndex = state.Slots.Count };
            state.Slots.Add(slot);
            state.ContentSlotsByWireIndex[wireIndex] = slot;
            await observer.OnEventAsync(new ModelPartStarted(requestId, sequence++, slot.PartIndex), cancellationToken)
                .ConfigureAwait(false);
        }

        return await AppendContentFragmentAsync(observer, requestId, sequence, slot, block, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<long> AppendContentFragmentAsync(
        IModelResponseObserver observer,
        ModelRequestId requestId,
        long sequence,
        Slot slot,
        CohereContentBlockDto? block,
        CancellationToken cancellationToken)
    {
        if (slot.Closed || block is null)
        {
            return sequence;
        }

        switch (slot.Kind)
        {
            case SlotKind.Text when !string.IsNullOrEmpty(block.Text):
                _ = slot.Text.Append(block.Text);
                await observer.OnEventAsync(
                        new ModelPartDelta(requestId, sequence++, slot.PartIndex, new TextContentDelta(block.Text)),
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case SlotKind.Reasoning when !string.IsNullOrEmpty(block.Thinking):
                _ = slot.Text.Append(block.Thinking);
                await observer.OnEventAsync(
                        new ModelPartDelta(requestId, sequence++, slot.PartIndex, new ReasoningContentDelta(block.Thinking, ExtensionData.Empty)),
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case SlotKind.Unknown:
                slot.UnknownSnapshot = block;
                break;
            case SlotKind.Text:
                break;
            case SlotKind.Reasoning:
                break;
            case SlotKind.ToolCall:
                break;
            default:
                break;
        }

        return sequence;
    }

    private static async Task<long> HandleContentEndAsync(
        IModelResponseObserver observer,
        ModelRequestId requestId,
        long sequence,
        CohereStreamEventDto streamEvent,
        StreamState state,
        CancellationToken cancellationToken)
    {
        var wireIndex = streamEvent.Index ?? 0;
        return state.ContentSlotsByWireIndex.TryGetValue(wireIndex, out var slot) && !slot.Closed
            ? await FinalizeSlotAsync(observer, requestId, sequence, slot, cancellationToken).ConfigureAwait(false)
            : sequence;
    }

    private static async Task<long> HandleToolCallStartAsync(
        IModelResponseObserver observer,
        ModelRequestId requestId,
        long sequence,
        CohereStreamEventDto streamEvent,
        StreamState state,
        IIdentifierGenerator<ToolCallId> toolCallIdGenerator,
        CancellationToken cancellationToken)
    {
        var wireIndex = streamEvent.Index ?? 0;
        var toolCall = streamEvent.Delta?.Message?.ToolCalls;

        var slot = new Slot
        {
            Kind = SlotKind.ToolCall,
            PartIndex = state.Slots.Count,
            AssignedCallId = toolCallIdGenerator.Create(),
            ToolCallId = toolCall?.Id,
            ToolCallName = toolCall?.Function?.Name,
        };
        state.Slots.Add(slot);
        state.ToolCallSlotsByWireIndex[wireIndex] = slot;

        await observer.OnEventAsync(new ModelPartStarted(requestId, sequence++, slot.PartIndex), cancellationToken)
            .ConfigureAwait(false);

        return await AppendToolCallFragmentAsync(observer, requestId, sequence, slot, toolCall, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<long> HandleToolCallDeltaAsync(
        IModelResponseObserver observer,
        ModelRequestId requestId,
        long sequence,
        CohereStreamEventDto streamEvent,
        StreamState state,
        IIdentifierGenerator<ToolCallId> toolCallIdGenerator,
        CancellationToken cancellationToken)
    {
        var wireIndex = streamEvent.Index ?? 0;
        var toolCall = streamEvent.Delta?.Message?.ToolCalls;

        if (!state.ToolCallSlotsByWireIndex.TryGetValue(wireIndex, out var slot))
        {
            slot = new Slot { Kind = SlotKind.ToolCall, PartIndex = state.Slots.Count, AssignedCallId = toolCallIdGenerator.Create() };
            state.Slots.Add(slot);
            state.ToolCallSlotsByWireIndex[wireIndex] = slot;
            await observer.OnEventAsync(new ModelPartStarted(requestId, sequence++, slot.PartIndex), cancellationToken)
                .ConfigureAwait(false);
        }

        return await AppendToolCallFragmentAsync(observer, requestId, sequence, slot, toolCall, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<long> AppendToolCallFragmentAsync(
        IModelResponseObserver observer,
        ModelRequestId requestId,
        long sequence,
        Slot slot,
        CohereToolCallDto? toolCall,
        CancellationToken cancellationToken)
    {
        if (slot.Closed || toolCall is null)
        {
            return sequence;
        }

        slot.ToolCallId ??= toolCall.Id;
        slot.ToolCallName ??= toolCall.Function?.Name;

        var fragment = toolCall.Function?.Arguments;
        if (string.IsNullOrEmpty(fragment))
        {
            return sequence;
        }

        _ = slot.ToolCallArguments.Append(fragment);
        await observer.OnEventAsync(
                new ModelPartDelta(requestId, sequence++, slot.PartIndex, new ToolArgumentsContentDelta(slot.AssignedCallId, fragment)),
                cancellationToken)
            .ConfigureAwait(false);

        return sequence;
    }

    private static async Task<long> HandleToolCallEndAsync(
        IModelResponseObserver observer,
        ModelRequestId requestId,
        long sequence,
        CohereStreamEventDto streamEvent,
        StreamState state,
        CancellationToken cancellationToken)
    {
        var wireIndex = streamEvent.Index ?? 0;
        return state.ToolCallSlotsByWireIndex.TryGetValue(wireIndex, out var slot) && !slot.Closed
            ? await FinalizeSlotAsync(observer, requestId, sequence, slot, cancellationToken).ConfigureAwait(false)
            : sequence;
    }

    private static async Task<long> FinalizeSlotAsync(
        IModelResponseObserver observer,
        ModelRequestId requestId,
        long sequence,
        Slot slot,
        CancellationToken cancellationToken)
    {
        slot.FinalPart = MaterializeSlot(slot);
        slot.Closed = true;

        await observer.OnEventAsync(new ModelPartCompleted(requestId, sequence++, slot.PartIndex, slot.FinalPart), cancellationToken)
            .ConfigureAwait(false);

        return sequence;
    }

    /// <summary>
    /// Determines whether the open tool-call slot at <paramref name="wireIndex"/>, if any, still lacks a function
    /// name when the provider signals its end.
    /// </summary>
    /// <param name="state">The streaming state owning the slots.</param>
    /// <param name="wireIndex">The provider's tool-call index.</param>
    /// <returns><see langword="true"/> when an open tool-call slot exists at that index and its name never arrived.</returns>
    private static bool TryGetOpenNamelessToolCall(StreamState state, int wireIndex)
    {
        Debug.Assert(state is not null, "The streaming state machine always owns its state.");
        return state.ToolCallSlotsByWireIndex.TryGetValue(wireIndex, out var slot)
            && !slot.Closed
            && !HasMaterializableToolName(slot);
    }

    /// <summary>
    /// Determines whether a slot can be materialized as far as its tool name is concerned: text, reasoning, and
    /// unknown slots always can, and a tool-call slot can only once at least one event carried a nonblank
    /// function name.
    /// </summary>
    /// <param name="slot">The slot to inspect.</param>
    /// <returns><see langword="false"/> only for a tool-call slot whose name never arrived.</returns>
    private static bool HasMaterializableToolName(Slot slot)
    {
        Debug.Assert(slot is not null, "Callers inspect an existing slot.");
        return slot.Kind != SlotKind.ToolCall || !string.IsNullOrWhiteSpace(slot.ToolCallName);
    }

    /// <summary>Builds the content part an open slot currently represents.</summary>
    /// <param name="slot">A slot that has not been closed and, for a tool call, already carries its function name.</param>
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
            SlotKind.Reasoning => new ReasoningPart(
                new ReasoningContent(slot.Text.ToString(), ReasoningVisibility.Visible, signatureToken: null, ExtensionData.Empty),
                ExtensionData.Empty),
            SlotKind.Unknown => new UnknownContentPart(
                slot.UnknownSnapshot?.Type ?? _unknownBlockTypeName,
                JsonSerializer.SerializeToElement(slot.UnknownSnapshot, _serializerOptions),
                ExtensionData.Empty),
            SlotKind.ToolCall => new ToolCallPart(
                slot.AssignedCallId,
                new ToolReference(new ToolId(slot.ToolCallName!), null, slot.ToolCallName!),
                ParseArguments(slot.ToolCallArguments.ToString()),
                slot.ToolCallId is { Length: > 0 } id ? new ProviderToolCallId(id) : null,
                ExtensionData.Empty),
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

    private static async Task<ModelAttemptResult> FailAsync(
        IModelResponseObserver observer,
        CohereResponseParseContext context,
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
        CohereAssistantMessageDto message,
        IIdentifierGenerator<ToolCallId> toolCallIdGenerator)
    {
        var results = new List<(ContentDelta? Delta, ContentPart Part)>();

        if (message.Content is { } content)
        {
            foreach (var block in content)
            {
                switch (block.Type)
                {
                    case "text":
                        var text = block.Text ?? string.Empty;
                        results.Add((new TextContentDelta(text), new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)));
                        break;

                    case "thinking":
                        var thinking = block.Thinking ?? string.Empty;
                        results.Add((
                            new ReasoningContentDelta(thinking, ExtensionData.Empty),
                            new ReasoningPart(
                                new ReasoningContent(thinking, ReasoningVisibility.Visible, signatureToken: null, ExtensionData.Empty),
                                ExtensionData.Empty)));
                        break;

                    default:
                        results.Add((
                            null,
                            new UnknownContentPart(
                                block.Type ?? _unknownBlockTypeName,
                                JsonSerializer.SerializeToElement(block, _serializerOptions),
                                ExtensionData.Empty)));
                        break;
                }
            }
        }

        if (message.ToolCalls is { } toolCalls)
        {
            foreach (var toolCall in toolCalls)
            {
                if (toolCall.Function is not { } function || function.Name is not { Length: > 0 } name)
                {
                    continue;
                }

                var callId = toolCallIdGenerator.Create();
                var arguments = ParseArguments(function.Arguments);
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

    private static ProviderResponseIdentity BuildIdentity(CohereResponseParseContext context, string? responseId) =>
        new(
            context.ProviderId,
            upstreamProviderId: null,
            context.ApiFamily,
            context.RequestedModelId,
            context.RequestedModelId,
            context.DeploymentId,
            context.ProviderRequestId,
            responseId is { Length: > 0 } id ? new ProviderResponseId(id) : null);

    /// <summary>
    /// Builds final usage evidence from a complete Cohere message response.
    /// </summary>
    /// <param name="usage">The optional wire usage object.</param>
    /// <returns>Final usage when the provider supplied usage, or <see cref="ModelUsage.NotReported"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A supplied wire token count is negative.</exception>
    private static ModelUsage BuildUsage(CohereUsageDto? usage) =>
        usage is null
            ? ModelUsage.NotReported
            : new ModelUsage(ModelUsageReportState.Final,
                ToTokenCount(usage.Tokens?.InputTokens),
                ToTokenCount(usage.Tokens?.OutputTokens),
                ToTokenCount(usage.CachedTokens),
                reasoningTokens: null,
                estimatedCost: null,
                costCurrency: null,
                ExtensionData.Empty);

    /// <summary>
    /// Validates an optional raw Cohere token count before portable integer projection.
    /// </summary>
    /// <param name="value">The optional raw wire count.</param>
    /// <returns>The portable count when supplied; otherwise null.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> has a negative value.</exception>
    private static long? ToTokenCount(double? value)
    {
        if (value is not { } count)
        {
            return null;
        }

        ArgumentOutOfRangeException.ThrowIfNegative(count, nameof(value));
        return (long) count;
    }

    private static NormalizedStopReason MapFinishReason(string? finishReason) =>
        finishReason switch
        {
            "COMPLETE" or "STOP_SEQUENCE" => NormalizedStopReason.Completed,
            "MAX_TOKENS" => NormalizedStopReason.Length,
            "TOOL_CALL" => NormalizedStopReason.ToolUse,
            "ERROR" or "TIMEOUT" => NormalizedStopReason.Error,
            null => NormalizedStopReason.Pending,
            _ => NormalizedStopReason.Error,
        };

    private enum SlotKind
    {
        Text,
        Reasoning,
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

        public Dictionary<int, Slot> ContentSlotsByWireIndex { get; } = [];

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

        public CohereContentBlockDto? UnknownSnapshot { get; set; }

        public string? ToolCallId { get; set; }

        public string? ToolCallName { get; set; }

        public StringBuilder ToolCallArguments { get; } = new();

        public ToolCallId AssignedCallId { get; set; }

        public bool Closed { get; set; }

        public ContentPart? FinalPart { get; set; }
    }
}
