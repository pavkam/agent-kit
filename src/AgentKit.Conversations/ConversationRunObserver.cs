// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Projects neutral run progress into the conversation event surface.</summary>
internal sealed class ConversationRunObserver: IAgentRunObserver
{
    private readonly IConversationEventObserver _observer;
    private readonly Func<ToolResultPart, string> _describeToolResult;
    private readonly IToolPresenter? _toolPresenter;
    private readonly ImmutableDictionary<ToolId, ConversationToolPresentationBinding> _bindings;
    private readonly HashSet<(ModelRequestId RequestId, int PartIndex)> _streamedParts = [];
    private readonly Dictionary<ModelRequestId, ModelUsage> _reportedUsage = [];

    /// <summary>Initializes a conversation run observer.</summary>
    /// <param name="observer">The conversation observer receiving projected events.</param>
    /// <param name="describeToolResult">Builds the bounded display summary for a terminal tool result.</param>
    /// <param name="toolPresenter">The optional bounded observational presenter.</param>
    /// <param name="bindings">The immutable exact descriptor bindings captured with the advertised tools.</param>
    /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
    internal ConversationRunObserver(
        IConversationEventObserver observer,
        Func<ToolResultPart, string> describeToolResult,
        IToolPresenter? toolPresenter,
        ImmutableDictionary<ToolId, ConversationToolPresentationBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(describeToolResult);
        ArgumentNullException.ThrowIfNull(bindings);
        _observer = observer;
        _describeToolResult = describeToolResult;
        _toolPresenter = toolPresenter;
        _bindings = bindings;
    }

    /// <inheritdoc/>
    public async ValueTask OnEventAsync(AgentRunEvent runEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runEvent);
        var conversationEvent = await ProjectAsync(runEvent, cancellationToken).ConfigureAwait(false);
        if (conversationEvent is not null)
        {
            await _observer.OnEventAsync(conversationEvent, cancellationToken).ConfigureAwait(false);
        }

        // Completion usage supersedes any interim update; it is suppressed only when the very same usage value
        // was already delivered for this request, so a Final report is never dropped in favour of a stale Interim one.
        if (runEvent is AgentRunModelResponseEvent { ResponseEvent: ModelResponseCompleted completed }
            && completed.Response.Usage.ReportState != ModelUsageReportState.NotReported
            && !(_reportedUsage.TryGetValue(completed.RequestId, out var delivered) && delivered == completed.Response.Usage))
        {
            _reportedUsage[completed.RequestId] = completed.Response.Usage;
            await _observer.OnEventAsync(
                new ConversationUsageEvent(completed.Response.Usage),
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Projects one neutral run event while retaining per-request duplicate-suppression state.</summary>
    /// <param name="runEvent">The validated run event.</param>
    /// <param name="cancellationToken">Cancels best-effort presentation and event delivery.</param>
    /// <returns>The display event to deliver, or null when the event has no conversation projection.</returns>
    private async ValueTask<ConversationEvent?> ProjectAsync(
        AgentRunEvent runEvent,
        CancellationToken cancellationToken)
    {
        if (runEvent is AgentRunToolCallStarted started)
        {
            var presentation = await PresentAsync(started.Call, cancellationToken).ConfigureAwait(false);
            return new ConversationToolCallEvent(
                started.Call.CallId,
                started.Call.Tool.ProviderAlias.Value,
                started.Call.Arguments.GetRawText(),
                presentation);
        }

        if (runEvent is AgentRunToolCallCompleted completed)
        {
            var presentation = await PresentAsync(completed.Result, cancellationToken).ConfigureAwait(false);
            return new ConversationToolResultEvent(
                completed.Result.CallId,
                completed.Result.Tool.ProviderAlias.Value,
                completed.Result.Outcome.Kind == ToolCallOutcomeKind.Success,
                _describeToolResult(completed.Result),
                presentation);
        }

        return Project(runEvent);
    }

    /// <summary>Projects one neutral non-tool event while retaining per-request duplicate-suppression state.</summary>
    /// <param name="runEvent">The validated run event.</param>
    /// <returns>The display event to deliver, or null when the event has no conversation projection.</returns>
    private ConversationEvent? Project(AgentRunEvent runEvent) => runEvent switch
    {
        AgentRunModelResponseEvent { ResponseEvent: ModelPartDelta deltaEvent }
            when deltaEvent.Delta is TextContentDelta text => ProjectTextDelta(deltaEvent, text),
        AgentRunModelResponseEvent { ResponseEvent: ModelPartDelta deltaEvent }
            when deltaEvent.Delta is ReasoningContentDelta reasoning => ProjectReasoningDelta(deltaEvent, reasoning),
        AgentRunModelResponseEvent { ResponseEvent: ModelPartCompleted completed }
            when completed.Part is TextPart text && !_streamedParts.Contains((completed.RequestId, completed.PartIndex)) =>
            string.IsNullOrWhiteSpace(text.Text) ? null : new ConversationAssistantTextEvent(text.Text),
        AgentRunModelResponseEvent { ResponseEvent: ModelPartCompleted completed }
            when completed.Part is ReasoningPart { Content.Text: { } text }
                 && !_streamedParts.Contains((completed.RequestId, completed.PartIndex)) =>
            new ConversationReasoningEvent(text),
        AgentRunModelResponseEvent { ResponseEvent: ModelUsageUpdated usage } => ProjectUsage(usage),
        _ => null,
    };

    /// <summary>Creates bounded presentation without allowing observational failure to suppress the generic event.</summary>
    /// <param name="part">The original immutable call or result projection.</param>
    /// <param name="cancellationToken">The caller token passed only as best-effort presentation cancellation.</param>
    /// <returns>The bounded presentation, or null when no presenter exists or presentation fails.</returns>
    private ValueTask<ToolPresentation?> PresentAsync(ContentPart part, CancellationToken cancellationToken) =>
        ConversationToolPresentationProjector.PresentAsync(part, _toolPresenter, _bindings, cancellationToken);

    /// <summary>Records and projects an exact assistant-text fragment.</summary>
    /// <param name="responseEvent">The correlated part event.</param>
    /// <param name="delta">The exact text fragment.</param>
    /// <returns>The incremental conversation event.</returns>
    private ConversationAssistantTextDeltaEvent ProjectTextDelta(ModelPartDelta responseEvent, TextContentDelta delta)
    {
        _ = _streamedParts.Add((responseEvent.RequestId, responseEvent.PartIndex));
        return new ConversationAssistantTextDeltaEvent(delta.Text);
    }

    /// <summary>Records and projects provider-exposed reasoning.</summary>
    /// <param name="responseEvent">The correlated part event.</param>
    /// <param name="delta">The exposed reasoning fragment.</param>
    /// <returns>The reasoning conversation event.</returns>
    private ConversationReasoningEvent ProjectReasoningDelta(ModelPartDelta responseEvent, ReasoningContentDelta delta)
    {
        _ = _streamedParts.Add((responseEvent.RequestId, responseEvent.PartIndex));
        return new ConversationReasoningEvent(delta.Text);
    }

    /// <summary>Records and projects the latest reported usage snapshot.</summary>
    /// <param name="usage">The correlated usage update.</param>
    /// <returns>The usage conversation event.</returns>
    private ConversationUsageEvent ProjectUsage(ModelUsageUpdated usage)
    {
        _reportedUsage[usage.RequestId] = usage.Usage;
        return new ConversationUsageEvent(usage.Usage);
    }
}
