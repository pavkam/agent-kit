// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

using Microsoft.Extensions.Options;

/// <summary>
/// The default <see cref="IAgentLoop"/>: loads a run's eligible history,
/// assembles context for each turn, executes exactly one model attempt per
/// turn, invokes every requested tool call, commits every produced message,
/// and settles once no further tool call is pending, the turn limit is
/// reached, or an unrecoverable failure occurs.
/// </summary>
/// <remarks>
/// <para>
/// See <see cref="IAgentLoop"/> for the reduced-scope rationale shared by
/// every implementation of this contract: this loop resolves its model by
/// matching <see cref="AgentRunRequest.Model"/>'s
/// <see cref="ModelDescriptor.Alias"/> against the additively registered
/// <see cref="IChatModel"/> set, performs no queued-input admission, no
/// budget reservation, and no hook dispatch, and always executes exactly
/// one attempt per turn (same-model retry and fallback are out of scope).
/// </para>
/// <para>
/// Every message this loop commits is appended through
/// <see cref="ISessionCoordinator.AppendAsync"/> guarded by the branch's
/// last-observed <see cref="SessionVersion"/>, so a concurrent writer to the
/// same branch is detected as a conflict rather than silently lost. When a
/// model attempt fails or is cancelled after producing partial output, that
/// output is first committed as an <see cref="MessageState.Interrupted"/>
/// <see cref="AssistantMessage"/> so it is never discarded, before the run
/// settles with <see cref="AgentRunFailed"/> or <see cref="AgentRunCancelled"/>.
/// </para>
/// </remarks>
public sealed class DefaultAgentLoop: IAgentLoop
{
    private readonly ISessionCoordinator _sessionCoordinator;
    private readonly IContextAssembler _contextAssembler;
    private readonly IToolInvoker _toolInvoker;
    private readonly Dictionary<ModelAlias, IChatModel> _models;
    private readonly IIdentifierGenerator<OperationId> _operationIds;
    private readonly IIdentifierGenerator<TurnId> _turnIds;
    private readonly IIdentifierGenerator<ModelRequestId> _modelRequestIds;
    private readonly IIdentifierGenerator<MessageId> _messageIds;
    private readonly IIdentifierGenerator<SessionEntryId> _entryIds;
    private readonly TimeProvider _timeProvider;
    private readonly int _historyReadPageSize;

    /// <summary>Initializes a new instance of the <see cref="DefaultAgentLoop"/> class.</summary>
    /// <param name="sessionCoordinator">Loads eligible history and commits every produced message.</param>
    /// <param name="contextAssembler">Assembles the provider-ready request for each turn.</param>
    /// <param name="toolInvoker">Resolves, authorizes, and invokes every requested tool call.</param>
    /// <param name="chatModels">The additively registered chat models this loop may select from.</param>
    /// <param name="operationIds">Generates the run's causal operation identity.</param>
    /// <param name="turnIds">Generates each turn's identity.</param>
    /// <param name="modelRequestIds">Generates each model request's identity.</param>
    /// <param name="messageIds">Generates each committed message's identity.</param>
    /// <param name="entryIds">Generates each appended session entry's identity.</param>
    /// <param name="timeProvider">The clock used to timestamp committed messages and attempt deadlines.</param>
    /// <param name="options">The validated loop options.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="chatModels"/> contains more than one <see cref="IChatModel"/> registered for the same
    /// <see cref="ModelAlias"/>.
    /// </exception>
    public DefaultAgentLoop(
        ISessionCoordinator sessionCoordinator,
        IContextAssembler contextAssembler,
        IToolInvoker toolInvoker,
        IEnumerable<IChatModel> chatModels,
        IIdentifierGenerator<OperationId> operationIds,
        IIdentifierGenerator<TurnId> turnIds,
        IIdentifierGenerator<ModelRequestId> modelRequestIds,
        IIdentifierGenerator<MessageId> messageIds,
        IIdentifierGenerator<SessionEntryId> entryIds,
        TimeProvider timeProvider,
        IOptions<AgentLoopOptions> options)
    {
        ArgumentNullException.ThrowIfNull(sessionCoordinator);
        ArgumentNullException.ThrowIfNull(contextAssembler);
        ArgumentNullException.ThrowIfNull(toolInvoker);
        ArgumentNullException.ThrowIfNull(chatModels);
        ArgumentNullException.ThrowIfNull(operationIds);
        ArgumentNullException.ThrowIfNull(turnIds);
        ArgumentNullException.ThrowIfNull(modelRequestIds);
        ArgumentNullException.ThrowIfNull(messageIds);
        ArgumentNullException.ThrowIfNull(entryIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);

        var models = new Dictionary<ModelAlias, IChatModel>();
        foreach (var model in chatModels)
        {
            if (!models.TryAdd(model.Alias, model))
            {
                throw new ArgumentException(
                    $"More than one {nameof(IChatModel)} is registered for alias '{model.Alias}'.",
                    nameof(chatModels));
            }
        }

        _sessionCoordinator = sessionCoordinator;
        _contextAssembler = contextAssembler;
        _toolInvoker = toolInvoker;
        _models = models;
        _operationIds = operationIds;
        _turnIds = turnIds;
        _modelRequestIds = modelRequestIds;
        _messageIds = messageIds;
        _entryIds = entryIds;
        _timeProvider = timeProvider;
        _historyReadPageSize = options.Value.HistoryReadPageSize;
    }

    /// <inheritdoc/>
    public async Task<AgentLoopResult> RunAsync(AgentRunRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var operationId = _operationIds.Create();
        var runCorrelation = new InRunOperationCorrelation(operationId, request.RunId, turnId: null);
        var sessionContext = new SessionOperationContext(request.AgentId, request.SessionId, runCorrelation, request.Identity);

        var historyLoad = await LoadHistoryAsync(sessionContext, request.BranchId, cancellationToken).ConfigureAwait(false);
        if (historyLoad is not var (initialEntries, loadedVersion))
        {
            return BuildResult(
                request,
                new AgentRunSessionOperationFailed("The run's eligible session history could not be loaded."),
                [],
                new SessionVersion(0));
        }

        var currentVersion = loadedVersion;

        if (!_models.TryGetValue(request.Model.Alias, out var chatModel))
        {
            var failure = new ProviderFailure(
                ProviderFailureKind.InvalidRequest,
                request.Model.ProviderId,
                requestId: null,
                statusCode: null,
                providerCode: null,
                retryAfter: null,
                $"No chat model is registered for model alias '{request.Model.Alias}'.",
                diagnosticCause: null,
                ExtensionData.Empty);

            return BuildResult(request, new AgentRunFailed(failure), [], currentVersion);
        }

        var committedMessages = ImmutableArray.CreateBuilder<AgentMessage>();
        var history = ToMessages(initialEntries);

        for (var turn = 1; turn <= request.MaxTurns; turn++)
        {
            var result = await RunTurnAsync(
                request,
                chatModel,
                operationId,
                turn,
                history,
                committedMessages,
                currentVersion,
                cancellationToken).ConfigureAwait(false);

            if (result.Outcome is not null)
            {
                return BuildResult(request, result.Outcome, committedMessages.ToImmutable(), result.Version);
            }

            currentVersion = result.Version;
            history = history.AddRange(result.NewMessages);
        }

        return BuildResult(request, new AgentRunTurnLimitReached(request.MaxTurns), committedMessages.ToImmutable(), currentVersion);
    }

    private async Task<TurnOutcome> RunTurnAsync(
        AgentRunRequest request,
        IChatModel chatModel,
        OperationId operationId,
        int turn,
        ImmutableArray<AgentMessage> history,
        ImmutableArray<AgentMessage>.Builder committedMessages,
        SessionVersion currentVersion,
        CancellationToken cancellationToken)
    {
        var turnId = _turnIds.Create();
        var turnCorrelation = new InRunOperationCorrelation(operationId, request.RunId, turnId);
        var turnSessionContext = new SessionOperationContext(request.AgentId, request.SessionId, turnCorrelation, request.Identity);
        var modelRequestId = _modelRequestIds.Create();

        var assembleRequest = new ContextAssemblyRequest(
            request.AgentId,
            request.SessionId,
            request.BranchId,
            request.RunId,
            turnId,
            modelRequestId,
            request.Model,
            request.Instructions,
            history,
            request.Tools,
            request.ToolChoice,
            request.Settings,
            ExtensionData.Empty);

        var assembleResult = await _contextAssembler.AssembleAsync(assembleRequest, cancellationToken).ConfigureAwait(false);

        if (assembleResult is ContextPreparationFailed prepFailed)
        {
            return TurnOutcome.Settled(new AgentRunContextPreparationFailed(prepFailed.Failure), currentVersion);
        }

        var context = ((ContextReady) assembleResult).Context;
        var deadline = _timeProvider.GetUtcNow() + request.AttemptTimeout;
        var chatRequest = new ChatModelRequest(context, attempt: 1, deadline, ProviderRequestOptions.Empty);

        var attemptResult = await chatModel.ExecuteAsync(chatRequest, NoOpModelResponseObserver.Instance, cancellationToken)
            .ConfigureAwait(false);

        return attemptResult switch
        {
            ModelAttemptFailed failed => await SettleInterruptedAsync(
                request, turnSessionContext, turnCorrelation, turnId, modelRequestId,
                failed.PartialParts, failed.Usage, NormalizedStopReason.Error, failed.Failure.RequestId,
                new AgentRunFailed(failed.Failure), committedMessages, currentVersion, cancellationToken)
                .ConfigureAwait(false),

            ModelAttemptCancelled cancelled => await SettleInterruptedAsync(
                request, turnSessionContext, turnCorrelation, turnId, modelRequestId,
                cancelled.PartialParts, cancelled.Usage, NormalizedStopReason.Cancelled,
                cancelled.Cancellation.RequestId, new AgentRunCancelled(cancelled.Cancellation.SafeMessage),
                committedMessages, currentVersion, cancellationToken)
                .ConfigureAwait(false),

            ModelAttemptCompleted completed => await SettleCompletedAsync(
                request, turnSessionContext, turnCorrelation, turnId, turn, completed.Response,
                committedMessages, currentVersion, cancellationToken)
                .ConfigureAwait(false),

            _ => throw new InvalidOperationException(
                $"Unrecognized {nameof(ModelAttemptResult)} kind '{attemptResult.GetType()}'."),
        };
    }

    private async Task<TurnOutcome> SettleCompletedAsync(
        AgentRunRequest request,
        SessionOperationContext turnSessionContext,
        InRunOperationCorrelation turnCorrelation,
        TurnId turnId,
        int turn,
        ModelResponse response,
        ImmutableArray<AgentMessage>.Builder committedMessages,
        SessionVersion currentVersion,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var assistantMessage = new AssistantMessage(
            _messageIds.Create(),
            request.AgentId,
            request.SessionId,
            conversationId: null,
            request.BranchId,
            request.RunId,
            turnId,
            now,
            MessageState.Complete,
            response.Parts,
            new AssistantResponseMetadata(
                response.RequestId, response.Identity, response.StopReason, rawStopReason: null, response.Usage, ExtensionData.Empty),
            ExtensionData.Empty);

        var assistantEntryId = _entryIds.Create();
        var assistantEntry = new MessageSessionEntry(
            assistantEntryId,
            turnSessionContext.ToAddress(),
            turnCorrelation,
            request.BranchId,
            new SessionSequence(currentVersion.Value + 1),
            causalParentId: null,
            now,
            new SchemaVersion("1.0"),
            assistantMessage);

        var appendResult = await _sessionCoordinator.AppendAsync(
            new SessionAppendRequest(
                turnSessionContext,
                request.BranchId,
                currentVersion,
                new IdempotencyKey($"run:{request.RunId}:turn:{turnId}:assistant"),
                [assistantEntry]),
            cancellationToken).ConfigureAwait(false);

        if (appendResult is not SessionAppended appended)
        {
            return TurnOutcome.Settled(
                new AgentRunSessionOperationFailed(DescribeAppendFailure(appendResult)), currentVersion);
        }

        currentVersion = appended.NewVersion;
        committedMessages.Add(assistantMessage);

        var toolCalls = response.Parts.OfType<ToolCallPart>().ToImmutableArray();

        return toolCalls switch
        {
            { IsEmpty: true } => TurnOutcome.Settled(new AgentRunCompleted(assistantMessage), currentVersion),
            _ when turn == request.MaxTurns =>
                TurnOutcome.Settled(new AgentRunTurnLimitReached(request.MaxTurns), currentVersion),
            _ => await InvokeToolsAsync(
                request, turnSessionContext, turnCorrelation, turnId, assistantEntryId, assistantMessage, toolCalls,
                committedMessages, currentVersion, cancellationToken)
                .ConfigureAwait(false),
        };
    }

    private async Task<TurnOutcome> InvokeToolsAsync(
        AgentRunRequest request,
        SessionOperationContext turnSessionContext,
        InRunOperationCorrelation turnCorrelation,
        TurnId turnId,
        SessionEntryId assistantEntryId,
        AgentMessage assistantMessage,
        ImmutableArray<ToolCallPart> toolCalls,
        ImmutableArray<AgentMessage>.Builder committedMessages,
        SessionVersion currentVersion,
        CancellationToken cancellationToken)
    {
        var resultParts = ImmutableArray.CreateBuilder<ContentPart>(toolCalls.Length);
        foreach (var toolCall in toolCalls)
        {
            var toolContext = new ToolExecutionContext(
                request.AgentId, request.SessionId, toolCall.CallId, turnCorrelation, request.Identity);

            var invocationResult = await _toolInvoker.InvokeAsync(
                new ToolCallRequest(toolCall.Tool.Id, toolContext, toolCall.Arguments, _timeProvider.GetUtcNow()),
                cancellationToken).ConfigureAwait(false);

            resultParts.Add(new ToolResultPart(
                toolCall.CallId, toolCall.Tool, invocationResult.Outcome, invocationResult.Content, ExtensionData.Empty));
        }

        var now = _timeProvider.GetUtcNow();
        var toolMessage = new ToolMessage(
            _messageIds.Create(),
            request.AgentId,
            request.SessionId,
            conversationId: null,
            request.BranchId,
            request.RunId,
            turnId,
            now,
            MessageState.Complete,
            resultParts.ToImmutable(),
            ExtensionData.Empty);

        var toolEntry = new MessageSessionEntry(
            _entryIds.Create(),
            turnSessionContext.ToAddress(),
            turnCorrelation,
            request.BranchId,
            new SessionSequence(currentVersion.Value + 1),
            assistantEntryId,
            now,
            new SchemaVersion("1.0"),
            toolMessage);

        var appendResult = await _sessionCoordinator.AppendAsync(
            new SessionAppendRequest(
                turnSessionContext,
                request.BranchId,
                currentVersion,
                new IdempotencyKey($"run:{request.RunId}:turn:{turnId}:tools"),
                [toolEntry]),
            cancellationToken).ConfigureAwait(false);

        if (appendResult is not SessionAppended appended)
        {
            return TurnOutcome.Settled(
                new AgentRunSessionOperationFailed(DescribeAppendFailure(appendResult)), currentVersion);
        }

        committedMessages.Add(toolMessage);
        return TurnOutcome.Continue(appended.NewVersion, [assistantMessage, toolMessage]);
    }

    private async Task<TurnOutcome> SettleInterruptedAsync(
        AgentRunRequest request,
        SessionOperationContext turnSessionContext,
        InRunOperationCorrelation turnCorrelation,
        TurnId turnId,
        ModelRequestId modelRequestId,
        ImmutableArray<ContentPart> partialParts,
        ModelUsage? usage,
        NormalizedStopReason stopReason,
        ProviderRequestId? providerRequestId,
        AgentRunOutcome outcome,
        ImmutableArray<AgentMessage>.Builder committedMessages,
        SessionVersion currentVersion,
        CancellationToken cancellationToken)
    {
        if (partialParts.IsEmpty)
        {
            return TurnOutcome.Settled(outcome, currentVersion);
        }

        var now = _timeProvider.GetUtcNow();
        var identity = new ProviderResponseIdentity(
            request.Model.ProviderId,
            upstreamProviderId: null,
            request.Model.ApiFamily,
            request.Model.ModelId,
            request.Model.ModelId,
            request.Model.DeploymentId,
            providerRequestId,
            responseId: null);

        var interruptedMessage = new AssistantMessage(
            _messageIds.Create(),
            request.AgentId,
            request.SessionId,
            conversationId: null,
            request.BranchId,
            request.RunId,
            turnId,
            now,
            MessageState.Interrupted,
            partialParts,
            new AssistantResponseMetadata(
                modelRequestId, identity, stopReason, rawStopReason: null, usage ?? ModelUsage.Empty, ExtensionData.Empty),
            ExtensionData.Empty);

        var entry = new MessageSessionEntry(
            _entryIds.Create(),
            turnSessionContext.ToAddress(),
            turnCorrelation,
            request.BranchId,
            new SessionSequence(currentVersion.Value + 1),
            causalParentId: null,
            now,
            new SchemaVersion("1.0"),
            interruptedMessage);

        var appendResult = await _sessionCoordinator.AppendAsync(
            new SessionAppendRequest(
                turnSessionContext,
                request.BranchId,
                currentVersion,
                new IdempotencyKey($"run:{request.RunId}:turn:{turnId}:interrupted"),
                [entry]),
            cancellationToken).ConfigureAwait(false);

        if (appendResult is not SessionAppended appended)
        {
            return TurnOutcome.Settled(
                new AgentRunSessionOperationFailed(DescribeAppendFailure(appendResult)), currentVersion);
        }

        committedMessages.Add(interruptedMessage);
        return TurnOutcome.Settled(outcome, appended.NewVersion);
    }

    private async Task<(ImmutableArray<SessionEntry> Entries, SessionVersion Version)?> LoadHistoryAsync(
        SessionOperationContext sessionContext, BranchId branchId, CancellationToken cancellationToken)
    {
        var entries = ImmutableArray.CreateBuilder<SessionEntry>();
        var cursor = new SessionSequence(0);

        while (true)
        {
            var pageResult = await _sessionCoordinator.ReadAsync(
                new SessionReadRequest(sessionContext, branchId, cursor, _historyReadPageSize), cancellationToken)
                .ConfigureAwait(false);

            if (pageResult is not SessionPage page)
            {
                return null;
            }

            entries.AddRange(page.Entries);
            cursor = page.ThroughSequence;

            if (!page.HasMore || page.Entries.IsEmpty)
            {
                break;
            }
        }

        return (entries.ToImmutable(), new SessionVersion(cursor.Value));
    }

    private static ImmutableArray<AgentMessage> ToMessages(ImmutableArray<SessionEntry> entries)
    {
        var builder = ImmutableArray.CreateBuilder<AgentMessage>();
        foreach (var entry in entries)
        {
            if (entry is MessageSessionEntry messageEntry)
            {
                builder.Add(messageEntry.Message);
            }
        }

        return builder.ToImmutable();
    }

    private static string DescribeAppendFailure(SessionAppendResult result) => result switch
    {
        SessionAppendConflict conflict =>
            $"The session branch advanced from the expected version {conflict.ExpectedVersion} to " +
                $"{conflict.ActualVersion} before the append committed.",
        SessionAppendNotFound => "The session or branch no longer exists.",
        SessionAppendFailed failed => failed.SafeMessage,
        _ => "The session append failed for an unknown reason.",
    };

    private static AgentLoopResult BuildResult(
        AgentRunRequest request, AgentRunOutcome outcome, ImmutableArray<AgentMessage> newMessages, SessionVersion finalVersion) =>
        new(request.AgentId, request.SessionId, request.BranchId, request.RunId, outcome, newMessages, finalVersion);

    /// <summary>The result of running one turn: either it settled the run, or it should continue to another turn.</summary>
    private readonly struct TurnOutcome
    {
        private TurnOutcome(AgentRunOutcome? outcome, SessionVersion version, ImmutableArray<AgentMessage> newMessages)
        {
            Outcome = outcome;
            Version = version;
            NewMessages = newMessages;
        }

        /// <summary>Gets the terminal outcome, when the run settled during this turn.</summary>
        public AgentRunOutcome? Outcome { get; }

        /// <summary>Gets the session version after this turn's commits.</summary>
        public SessionVersion Version { get; }

        /// <summary>Gets the messages newly visible to the next turn's history, when continuing.</summary>
        public ImmutableArray<AgentMessage> NewMessages { get; }

        /// <summary>Creates a settled outcome.</summary>
        public static TurnOutcome Settled(AgentRunOutcome outcome, SessionVersion version) => new(outcome, version, []);

        /// <summary>Creates a continuation outcome.</summary>
        public static TurnOutcome Continue(SessionVersion version, ImmutableArray<AgentMessage> newMessages) =>
            new(null, version, newMessages);
    }
}
