// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using AgentKit.Providers.Http;
using AgentKit.Providers.OpenAICompatible.Wire;

/// <summary>
/// The default <see cref="IOpenAIStreamParser"/>, parsing both buffered and
/// server-sent-events OpenAI-compatible chat completion responses as a
/// typed state machine.
/// </summary>
public sealed class OpenAIChatCompletionResponseParser: IOpenAIStreamParser
{
    private const string _doneSentinel = "[DONE]";
    private const int _textPartIndex = 0;

    /// <summary>
    /// The part index used for visible reasoning. Text occupies index 0 and tool calls occupy their wire index
    /// plus one, so reasoning takes a fixed index that cannot collide with either.
    /// </summary>
    private const int _reasoningPartIndex = -1;

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

        ModelUsage usage;
        try
        {
            usage = MapUsage(dto.Usage);
        }
        catch (ArgumentException exception)
        {
            return await FailAsync(
                observer,
                context,
                sequence,
                "The provider returned invalid usage evidence.",
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

        if (dto.Choices.Count > 1)
        {
            // The request pins `n` to 1 and the response contract carries exactly one candidate. Silently keeping
            // the first choice would discard model output the caller never sees, so extra choices fail closed.
            return await FailAsync(
                observer,
                context,
                sequence,
                "The provider returned more than one choice for a single-candidate request.",
                diagnosticCause: null,
                cancellationToken).ConfigureAwait(false);
        }

        var choice = dto.Choices[0];
        var parts = ImmutableArray.CreateBuilder<ContentPart>();

        if ((choice.Message.ReasoningContent ?? choice.Message.Reasoning) is { Length: > 0 } reasoningText)
        {
            await observer.OnEventAsync(new ModelPartStarted(requestId, sequence++, _reasoningPartIndex), cancellationToken)
                .ConfigureAwait(false);
            await observer.OnEventAsync(
                    new ModelPartDelta(requestId, sequence++, _reasoningPartIndex, new ReasoningContentDelta(reasoningText, ExtensionData.Empty)),
                    cancellationToken)
                .ConfigureAwait(false);
            var reasoningPart = new ReasoningPart(
                new ReasoningContent(reasoningText, ReasoningVisibility.Visible, signatureToken: null, ExtensionData.Empty),
                ExtensionData.Empty);
            await observer.OnEventAsync(new ModelPartCompleted(requestId, sequence++, _reasoningPartIndex, reasoningPart), cancellationToken)
                .ConfigureAwait(false);
            parts.Add(reasoningPart);
        }

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

                JsonElement arguments;
                try
                {
                    arguments = ParseArgumentsOrEmpty(toolCall.Function.Arguments);
                }
                catch (JsonException)
                {
                    return await FailAsync(
                        observer,
                        context,
                        sequence,
                        "The provider returned malformed tool-call arguments.",
                        diagnosticCause: null,
                        cancellationToken,
                        parts.ToImmutable(),
                        dto.Usage is null ? null : usage).ConfigureAwait(false);
                }

                var toolCallPart = new ToolCallPart(
                    callId,
                    new ToolReference(new ToolId(toolCall.Function.Name), null, toolCall.Function.Name),
                    arguments,
                    new ProviderToolCallId(toolCall.Id),
                    ExtensionData.Empty);

                await observer.OnEventAsync(new ModelPartCompleted(requestId, sequence++, partIndex, toolCallPart), cancellationToken)
                    .ConfigureAwait(false);
                parts.Add(toolCallPart);
            }
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

        var textBuilder = new StringBuilder();
        var textPartOpen = false;
        var reasoningBuilder = new StringBuilder();
        var reasoningPartOpen = false;
        var toolCallSlots = new List<ToolCallAccumulator>();
        string? finishReason = null;
        string? resolvedModel = null;
        string? responseId = null;
        OpenAIUsage? usageDto = null;
        ModelUsage? reportedUsage = null;

        await foreach (var streamEvent in ServerSentEventReader.ReadAsync(responseBody, cancellationToken).ConfigureAwait(false))
        {
            // The [DONE] sentinel is a Chat Completions dialect convention layered over SSE, so the shared reader
            // leaves it in the payload and this parser recognizes it.
            var payload = streamEvent.Data;
            if (payload is _doneSentinel)
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
                    cancellationToken,
                    BuildOpenPartialParts(reasoningPartOpen, reasoningBuilder, textPartOpen, textBuilder, toolCallSlots),
                    reportedUsage).ConfigureAwait(false);
            }

            resolvedModel ??= chunk.Model;
            responseId ??= chunk.Id;

            if (chunk.Usage is not null)
            {
                usageDto = chunk.Usage;
                try
                {
                    reportedUsage = MapUsage(chunk.Usage);
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
                        BuildOpenPartialParts(reasoningPartOpen, reasoningBuilder, textPartOpen, textBuilder, toolCallSlots),
                        reportedUsage).ConfigureAwait(false);
                }
            }

            if (chunk.Error is { } errorFrame)
            {
                // An in-stream error frame is the provider's own failure report; it must surface with its
                // external code rather than being mistaken for a stream that merely ended early.
                return await FailWithProviderErrorAsync(
                        observer,
                        context,
                        sequence,
                        errorFrame,
                        BuildOpenPartialParts(reasoningPartOpen, reasoningBuilder, textPartOpen, textBuilder, toolCallSlots),
                        reportedUsage,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (chunk.Choices is not { Count: > 0 } choices)
            {
                continue;
            }

            if (choices.Count > 1 || choices[0].Index != 0)
            {
                // A streamed chunk for choice index 1+ belongs to a second candidate; merging its deltas into
                // choice 0 would corrupt the only candidate this operation represents.
                return await FailAsync(
                    observer,
                    context,
                    sequence,
                    "The provider streamed a choice other than the single requested candidate.",
                    diagnosticCause: null,
                    cancellationToken,
                    BuildOpenPartialParts(reasoningPartOpen, reasoningBuilder, textPartOpen, textBuilder, toolCallSlots),
                    reportedUsage).ConfigureAwait(false);
            }

            var choice = choices[0];
            if (choice.FinishReason is not null)
            {
                finishReason = choice.FinishReason;
            }

            var delta = choice.Delta;
            if (delta is null)
            {
                continue;
            }

            if ((delta.ReasoningContent ?? delta.Reasoning) is { Length: > 0 } reasoningFragment)
            {
                if (!reasoningPartOpen)
                {
                    reasoningPartOpen = true;
                    await observer.OnEventAsync(new ModelPartStarted(requestId, sequence++, _reasoningPartIndex), cancellationToken)
                        .ConfigureAwait(false);
                }

                await observer.OnEventAsync(
                        new ModelPartDelta(requestId, sequence++, _reasoningPartIndex, new ReasoningContentDelta(reasoningFragment, ExtensionData.Empty)),
                        cancellationToken)
                    .ConfigureAwait(false);
                _ = reasoningBuilder.Append(reasoningFragment);
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
                if (!TryResolveToolCallSlot(toolCallSlots, toolCallDelta, out var slot, out var created, out var violation))
                {
                    return await FailAsync(
                        observer,
                        context,
                        sequence,
                        violation,
                        diagnosticCause: null,
                        cancellationToken,
                        BuildOpenPartialParts(reasoningPartOpen, reasoningBuilder, textPartOpen, textBuilder, toolCallSlots),
                        reportedUsage).ConfigureAwait(false);
                }

                if (created)
                {
                    await observer.OnEventAsync(new ModelPartStarted(requestId, sequence++, slot.PartIndex), cancellationToken)
                        .ConfigureAwait(false);
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
                                slot.PartIndex,
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
                cancellationToken,
                BuildOpenPartialParts(reasoningPartOpen, reasoningBuilder, textPartOpen, textBuilder, toolCallSlots),
                reportedUsage).ConfigureAwait(false);
        }

        var parts = ImmutableArray.CreateBuilder<ContentPart>();

        if (reasoningPartOpen)
        {
            var reasoningPart = new ReasoningPart(
                new ReasoningContent(reasoningBuilder.ToString(), ReasoningVisibility.Visible, signatureToken: null, ExtensionData.Empty),
                ExtensionData.Empty);
            await observer.OnEventAsync(new ModelPartCompleted(requestId, sequence++, _reasoningPartIndex, reasoningPart), cancellationToken)
                .ConfigureAwait(false);
            parts.Add(reasoningPart);
        }

        if (textPartOpen)
        {
            var textPart = new TextPart(textBuilder.ToString(), TextSemantics.Plain, ExtensionData.Empty);
            await observer.OnEventAsync(new ModelPartCompleted(requestId, sequence++, _textPartIndex, textPart), cancellationToken)
                .ConfigureAwait(false);
            parts.Add(textPart);
        }

        foreach (var slot in toolCallSlots.OrderBy(static slot => slot.PartIndex))
        {
            // A tool name is the only identity the model actually requested. A provider call id or a placeholder is
            // not a tool, so a nameless slot fails closed instead of being fabricated into a canonical ToolId.
            if (slot.ToolName is not { } toolName)
            {
                return await FailAsync(
                    observer,
                    context,
                    sequence,
                    "The provider streamed a tool call without a function name.",
                    diagnosticCause: null,
                    cancellationToken,
                    parts.ToImmutable(),
                    reportedUsage).ConfigureAwait(false);
            }

            var argumentsJson = slot.Arguments.Length > 0 ? slot.Arguments.ToString() : "{}";

            JsonElement arguments;
            try
            {
                arguments = ParseArgumentsOrEmpty(argumentsJson);
            }
            catch (JsonException)
            {
                return await FailAsync(
                    observer,
                    context,
                    sequence,
                    "The provider returned malformed tool-call arguments.",
                    diagnosticCause: null,
                    cancellationToken,
                    parts.ToImmutable(),
                    reportedUsage).ConfigureAwait(false);
            }

            var toolCallPart = new ToolCallPart(
                slot.CallId,
                new ToolReference(new ToolId(toolName), null, toolName),
                arguments,
                slot.ProviderCallId is { } providerCallId ? new ProviderToolCallId(providerCallId) : null,
                ExtensionData.Empty);

            await observer.OnEventAsync(
                    new ModelPartCompleted(requestId, sequence++, slot.PartIndex, toolCallPart),
                    cancellationToken)
                .ConfigureAwait(false);
            parts.Add(toolCallPart);
        }

        var usage = reportedUsage ?? ModelUsage.NotReported;
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

    /// <summary>
    /// Fails the attempt with the provider's own in-stream error report, retaining its code and type as
    /// diagnostic evidence while exposing only a bounded, provider-neutral safe message.
    /// </summary>
    private static async Task<ModelAttemptResult> FailWithProviderErrorAsync(
        IModelResponseObserver observer,
        OpenAIResponseParseContext context,
        long sequence,
        OpenAIErrorDetail error,
        ImmutableArray<ContentPart> partialParts,
        ModelUsage? usage,
        CancellationToken cancellationToken)
    {
        var kind = error.Type switch
        {
            "rate_limit_error" or "rate_limit_exceeded" or "insufficient_quota" => ProviderFailureKind.Throttling,
            "authentication_error" => ProviderFailureKind.Authentication,
            "permission_error" => ProviderFailureKind.Authorization,
            "invalid_request_error" => ProviderFailureKind.InvalidRequest,
            "server_error" or "overloaded_error" => ProviderFailureKind.Unavailable,
            _ => ProviderFailureKind.Unknown,
        };
        var failure = new ProviderFailure(
            kind,
            context.ProviderId,
            context.ProviderRequestId,
            statusCode: null,
            providerCode: error.Code ?? error.Type,
            retryAfter: null,
            "The provider reported an error while streaming the response.",
            diagnosticCause: null,
            ExtensionData.Empty);

        await observer.OnEventAsync(
                new ModelResponseFailed(context.ModelRequestId, sequence, failure, partialParts, usage),
                cancellationToken)
            .ConfigureAwait(false);

        return new ModelAttemptFailed(failure, partialParts, usage);
    }

    /// <summary>
    /// Materializes the streaming accumulators that are still open when a stream fails so the terminal
    /// failure carries the content the model had actually produced. Chat Completions streams complete
    /// reasoning, text, and tool calls only once a finish reason arrives, so at any mid-stream failure no
    /// <see cref="ModelPartCompleted"/> has been delivered for them and they exist only in these builders.
    /// </summary>
    /// <param name="reasoningPartOpen">Whether a reasoning part has been started.</param>
    /// <param name="reasoningBuilder">The accumulated reasoning text.</param>
    /// <param name="textPartOpen">Whether a text part has been started.</param>
    /// <param name="textBuilder">The accumulated visible text.</param>
    /// <param name="toolCallSlots">The in-progress tool-call slots in arrival order.</param>
    /// <returns>
    /// Reasoning, then text, then tool calls in part-index order, matching the order the completed response
    /// would have used. A tool-call slot that has not yet received a function name, or whose accumulated
    /// arguments are not yet complete JSON, cannot be represented truthfully as a <see cref="ToolCallPart"/>
    /// and is omitted rather than fabricated.
    /// </returns>
    private static ImmutableArray<ContentPart> BuildOpenPartialParts(
        bool reasoningPartOpen,
        StringBuilder reasoningBuilder,
        bool textPartOpen,
        StringBuilder textBuilder,
        List<ToolCallAccumulator> toolCallSlots)
    {
        Debug.Assert(reasoningBuilder is not null, "The streaming state machine always owns a reasoning builder.");
        Debug.Assert(textBuilder is not null, "The streaming state machine always owns a text builder.");
        Debug.Assert(toolCallSlots is not null, "The streaming state machine always owns the tool-call slot map.");

        var partial = ImmutableArray.CreateBuilder<ContentPart>();
        if (reasoningPartOpen)
        {
            partial.Add(new ReasoningPart(
                new ReasoningContent(reasoningBuilder.ToString(), ReasoningVisibility.Visible, signatureToken: null, ExtensionData.Empty),
                ExtensionData.Empty));
        }

        if (textPartOpen)
        {
            partial.Add(new TextPart(textBuilder.ToString(), TextSemantics.Plain, ExtensionData.Empty));
        }

        foreach (var slot in toolCallSlots.OrderBy(static slot => slot.PartIndex))
        {
            if (slot.ToolName is not { } toolName)
            {
                continue;
            }

            JsonElement arguments;
            try
            {
                arguments = ParseArgumentsOrEmpty(slot.Arguments.Length > 0 ? slot.Arguments.ToString() : "{}");
            }
            catch (JsonException)
            {
                continue;
            }

            partial.Add(new ToolCallPart(
                slot.CallId,
                new ToolReference(new ToolId(toolName), null, toolName),
                arguments,
                slot.ProviderCallId is { } providerCallId ? new ProviderToolCallId(providerCallId) : null,
                ExtensionData.Empty));
        }

        return partial.ToImmutable();
    }

    private static async Task<ModelAttemptResult> FailAsync(
        IModelResponseObserver observer,
        OpenAIResponseParseContext context,
        long sequence,
        string safeMessage,
        Exception? diagnosticCause,
        CancellationToken cancellationToken,
        ImmutableArray<ContentPart> partialParts = default,
        ModelUsage? usage = null)
    {
        var normalizedPartialParts = partialParts.IsDefault ? [] : partialParts;
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
                new ModelResponseFailed(context.ModelRequestId, sequence, failure, normalizedPartialParts, usage),
                cancellationToken)
            .ConfigureAwait(false);

        return new ModelAttemptFailed(failure, normalizedPartialParts, usage);
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
            ? ModelUsage.NotReported
            : new ModelUsage(ModelUsageReportState.Final,
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
    /// Locates or opens the accumulation slot one streamed <c>tool_calls[]</c> fragment belongs to, keying by the
    /// wire <c>index</c> when present and by the provider call <c>id</c> otherwise.
    /// </summary>
    /// <param name="slots">The slots opened so far, in arrival order; a newly opened slot is appended.</param>
    /// <param name="delta">The fragment to place.</param>
    /// <param name="slot">The resolved slot when the method returns <see langword="true"/>.</param>
    /// <param name="created">Whether <paramref name="slot"/> was opened by this call and still needs its start event.</param>
    /// <param name="violation">The safe failure message when the method returns <see langword="false"/>.</param>
    /// <returns>
    /// <see langword="true"/> when the fragment was placed unambiguously. The rule is: a fragment with an
    /// <c>index</c> continues the slot opened under that index or opens it; an unindexed fragment with an
    /// <c>id</c> continues the slot already carrying that id or, when no slot does, opens a new one; an
    /// unindexed fragment without an <c>id</c> continues the most recently opened slot. A slot's provider call
    /// id is set once and never overwritten. The method returns <see langword="false"/> when a fragment has
    /// neither an index nor an id and no slot is open, or when an indexed fragment carries an id that differs
    /// from the id already bound to that index, because either fragment cannot be attributed truthfully.
    /// </returns>
    private bool TryResolveToolCallSlot(
        List<ToolCallAccumulator> slots,
        OpenAIToolCallDelta delta,
        [NotNullWhen(true)] out ToolCallAccumulator? slot,
        out bool created,
        [NotNullWhen(false)] out string? violation)
    {
        Debug.Assert(slots is not null, "The streaming state machine always owns the slot list.");
        Debug.Assert(delta is not null, "Deserialized tool-call fragments are never null elements.");
        created = false;
        violation = null;
        var id = delta.Id is { Length: > 0 } candidate ? candidate : null;

        if (delta.Index is { } wireIndex)
        {
            slot = slots.Find(existing => existing.WireIndex == wireIndex);
            if (slot is null)
            {
                slot = new ToolCallAccumulator(_toolCallIdGenerator.Create(), wireIndex, ToolCallPartIndex(wireIndex));
                created = true;
            }
            else if (id is not null && slot.ProviderCallId is { } bound && !string.Equals(bound, id, StringComparison.Ordinal))
            {
                slot = null;
                violation = "The provider streamed conflicting tool-call identifiers for one tool-call index.";
                return false;
            }
        }
        else if (id is not null)
        {
            slot = slots.Find(existing => string.Equals(existing.ProviderCallId, id, StringComparison.Ordinal));
            if (slot is null)
            {
                slot = new ToolCallAccumulator(_toolCallIdGenerator.Create(), wireIndex: null, NextUnindexedPartIndex(slots));
                created = true;
            }
        }
        else if (slots.Count > 0)
        {
            slot = slots[^1];
        }
        else
        {
            slot = null;
            violation = "The provider streamed a tool-call fragment that cannot be attributed to any tool call.";
            return false;
        }

        if (created)
        {
            slots.Add(slot);
        }

        slot.ProviderCallId ??= id;
        return true;
    }

    /// <summary>Chooses the next part index for a slot the provider did not index, after every index already in use.</summary>
    /// <param name="slots">The slots opened so far.</param>
    /// <returns>One more than the highest part index in <paramref name="slots"/>, or the first tool-call part index when none is open.</returns>
    private static int NextUnindexedPartIndex(List<ToolCallAccumulator> slots)
    {
        Debug.Assert(slots is not null, "The streaming state machine always owns the slot list.");
        var next = ToolCallPartIndex(0);
        foreach (var existing in slots)
        {
            next = Math.Max(next, existing.PartIndex + 1);
        }

        return next;
    }

    /// <summary>
    /// Mutable, private streaming-accumulation state for one in-progress
    /// tool call slot, keyed by its wire-level index when the provider sends
    /// one and by its provider call id otherwise. This is
    /// implementation-internal parsing state, never exposed outside this
    /// class.
    /// </summary>
    private sealed class ToolCallAccumulator
    {
        /// <summary>Initializes a new instance of the <see cref="ToolCallAccumulator"/> class.</summary>
        /// <param name="callId">The internal call identity minted for this slot.</param>
        /// <param name="wireIndex">The provider's <c>index</c> for this slot, or <see langword="null"/> when it was keyed by id.</param>
        /// <param name="partIndex">The stable part index used for every response event this slot emits.</param>
        public ToolCallAccumulator(ToolCallId callId, int? wireIndex, int partIndex)
        {
            CallId = callId;
            WireIndex = wireIndex;
            PartIndex = partIndex;
        }

        /// <summary>Gets the internal call identity minted for this slot.</summary>
        public ToolCallId CallId { get; }

        /// <summary>Gets the provider's <c>index</c> for this slot, or <see langword="null"/> when the slot is keyed by id.</summary>
        public int? WireIndex { get; }

        /// <summary>Gets the part index used for this slot's start, delta, and completion events.</summary>
        public int PartIndex { get; }

        /// <summary>Gets or sets the provider-supplied call identifier, bound once on first sight and never overwritten.</summary>
        public string? ProviderCallId { get; set; }

        /// <summary>Gets or sets the tool/function name, once seen.</summary>
        public string? ToolName { get; set; }

        /// <summary>Gets the accumulated raw JSON argument text.</summary>
        public StringBuilder Arguments { get; } = new();
    }
}
