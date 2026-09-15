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
/// every implementation of this contract: this loop delegates model choice
/// to the configured <see cref="IModelSelector"/> over the engine-wide
/// <see cref="IModelCatalog"/> and resolves the chosen descriptor to its
/// adapter through <see cref="ILlmModelResolver"/>. It performs no
/// queued-input admission, no budget reservation, and no hook dispatch, and
/// always executes exactly one attempt per turn (same-model retry and
/// cross-model fallback after a failed attempt are out of scope).
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
    private readonly ISecurityProfileSelector _securityProfileSelector;
    private readonly IContextAssembler _contextAssembler;
    private readonly IToolInvoker _toolInvoker;
    private readonly IModelCatalog _modelCatalog;
    private readonly IModelSelector _modelSelector;
    private readonly ILlmModelResolver _llmModelResolver;
    private readonly IIdentifierGenerator<OperationId> _operationIds;
    private readonly IIdentifierGenerator<TurnId> _turnIds;
    private readonly IIdentifierGenerator<ModelRequestId> _modelRequestIds;
    private readonly IIdentifierGenerator<MessageId> _messageIds;
    private readonly IIdentifierGenerator<SessionEntryId> _entryIds;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DefaultAgentLoop> _logger;
    private readonly int _historyReadPageSize;

    /// <summary>The most a single append retries after a concurrent writer advanced the branch, before giving up.</summary>
    /// <remarks>
    /// A tool invoked mid-turn (the plan/todo tool, for one) may commit its own session entries directly through
    /// <see cref="ISessionCoordinator"/>, independently of this loop's own turn-scoped <c>currentVersion</c>
    /// tracking. <see cref="SessionAppendConflict"/> documents that the store deliberately never rebases or
    /// retries on the caller's behalf; a bounded retry in <see cref="AppendWithDiagnosticsAsync"/>, rebasing onto
    /// the conflict's own reported <see cref="SessionAppendConflict.ActualVersion"/>, is exactly the
    /// reload-and-reattempt the type's own remarks describe. The bound exists only to turn a pathological runaway
    /// writer into a clear failure instead of an unbounded loop; an ordinary interleaved tool append settles on
    /// the first retry.
    /// </remarks>
    private const int _maxAppendConflictRetries = 5;

    /// <summary>Initializes a new instance of the <see cref="DefaultAgentLoop"/> class.</summary>
    /// <param name="sessionCoordinator">Loads eligible history and commits every produced message.</param>
    /// <param name="securityProfileSelector">Captures fresh authorization for each newly identified run operation.</param>
    /// <param name="contextAssembler">Assembles the provider-ready request for each turn.</param>
    /// <param name="toolInvoker">Resolves, authorizes, and invokes every requested tool call.</param>
    /// <param name="modelCatalog">Supplies the engine-wide versioned view of configured models.</param>
    /// <param name="modelSelector">Chooses one configured model for each run.</param>
    /// <param name="llmModelResolver">Resolves the chosen descriptor to its provider adapter.</param>
    /// <param name="operationIds">Generates the run's causal operation identity.</param>
    /// <param name="turnIds">Generates each turn's identity.</param>
    /// <param name="modelRequestIds">Generates each model request's identity.</param>
    /// <param name="messageIds">Generates each committed message's identity.</param>
    /// <param name="entryIds">Generates each appended session entry's identity.</param>
    /// <param name="timeProvider">The clock used to timestamp committed messages and attempt deadlines.</param>
    /// <param name="options">The validated loop options.</param>
    /// <param name="logger">
    /// The optional logger that receives safe run-lifecycle diagnostics; a
    /// Microsoft null logger is used when omitted.
    /// </param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public DefaultAgentLoop(
        ISessionCoordinator sessionCoordinator,
        ISecurityProfileSelector securityProfileSelector,
        IContextAssembler contextAssembler,
        IToolInvoker toolInvoker,
        IModelCatalog modelCatalog,
        IModelSelector modelSelector,
        ILlmModelResolver llmModelResolver,
        IIdentifierGenerator<OperationId> operationIds,
        IIdentifierGenerator<TurnId> turnIds,
        IIdentifierGenerator<ModelRequestId> modelRequestIds,
        IIdentifierGenerator<MessageId> messageIds,
        IIdentifierGenerator<SessionEntryId> entryIds,
        TimeProvider timeProvider,
        IOptions<AgentLoopOptions> options,
        ILogger<DefaultAgentLoop>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(sessionCoordinator);
        ArgumentNullException.ThrowIfNull(securityProfileSelector);
        ArgumentNullException.ThrowIfNull(contextAssembler);
        ArgumentNullException.ThrowIfNull(toolInvoker);
        ArgumentNullException.ThrowIfNull(modelCatalog);
        ArgumentNullException.ThrowIfNull(modelSelector);
        ArgumentNullException.ThrowIfNull(llmModelResolver);
        ArgumentNullException.ThrowIfNull(operationIds);
        ArgumentNullException.ThrowIfNull(turnIds);
        ArgumentNullException.ThrowIfNull(modelRequestIds);
        ArgumentNullException.ThrowIfNull(messageIds);
        ArgumentNullException.ThrowIfNull(entryIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);

        _sessionCoordinator = sessionCoordinator;
        _securityProfileSelector = securityProfileSelector;
        _contextAssembler = contextAssembler;
        _toolInvoker = toolInvoker;
        _modelCatalog = modelCatalog;
        _modelSelector = modelSelector;
        _llmModelResolver = llmModelResolver;
        _operationIds = operationIds;
        _turnIds = turnIds;
        _modelRequestIds = modelRequestIds;
        _messageIds = messageIds;
        _entryIds = entryIds;
        _timeProvider = timeProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultAgentLoop>.Instance;
        _historyReadPageSize = options.Value.HistoryReadPageSize;
    }

    /// <inheritdoc/>
    public async Task<AgentLoopResult> RunAsync(AgentRunRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var operationId = _operationIds.Create();
        var startedTimestamp = _timeProvider.GetTimestamp();
        using var activity = AgentKitDiagnostics.Activities.StartActivity(
            AgentKitActivityNames.InvokeAgent,
            ActivityKind.Internal,
            parentContext: Activity.Current?.Context ?? default,
            tags: new ActivityTagsCollection
            {
                { AgentKitTagNames.GenAiOperationName, AgentKitActivityNames.InvokeAgent },
                { AgentKitTagNames.AgentId, request.AgentId.ToString() },
                { AgentKitTagNames.SessionId, request.SessionId.ToString() },
                { AgentKitTagNames.RunId, request.RunId.ToString() },
                { AgentKitTagNames.OperationId, operationId.ToString() },
            });
        LoopLog.RunStarted(_logger, request.RunId, request.AgentId, request.SessionId);

        try
        {
            var result = await RunCoreAsync(request, operationId, cancellationToken).ConfigureAwait(false);
            var outcome = result.Outcome.GetType().Name;
            if (result.Outcome is AgentRunCompleted)
            {
                activity.SetSuccessful(outcome);
                LoopLog.RunCompleted(_logger, request.RunId, result.NewMessages.Length);
            }
            else
            {
                activity.SetFailed(outcome, outcome);
                LoopLog.RunEndedWithoutSuccess(_logger, request.RunId, outcome);
            }

            RecordRunMetrics(outcome, startedTimestamp);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", "cancellation");
            RecordRunMetrics("cancelled", startedTimestamp);
            LoopLog.RunCancelled(_logger, request.RunId);
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            activity.SetFailed("faulted", errorType);
            RecordRunMetrics("faulted", startedTimestamp);
            LoopLog.RunFaulted(_logger, request.RunId, exception.GetType().FullName ?? exception.GetType().Name);
            throw;
        }
    }

    private async Task<AgentLoopResult> RunCoreAsync(
        AgentRunRequest request,
        OperationId operationId,
        CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "A validated run request is required by the loop core.");
        var runCorrelation = new InRunOperationCorrelation(operationId, request.RunId, turnId: null);
        var (runAuthorization, runCaptureFailure) = await CaptureAuthorizationAsync(
            request, runCorrelation, cancellationToken)
            .ConfigureAwait(false);
        if (runAuthorization is null)
        {
            return BuildResult(
                request,
                new AgentRunSessionOperationFailed(runCaptureFailure!),
                [],
                new SessionVersion(0));
        }

        var sessionContext = new SessionOperationContext(
            request.AgentId,
            request.SessionId,
            executionLaneId: null,
            runCorrelation,
            request.Identity,
            runAuthorization);

        var historyLoad = await LoadHistoryAsync(
            sessionContext, request.SessionProfile, request.BranchId, cancellationToken).ConfigureAwait(false);
        if (historyLoad is not var (initialEntries, initialCursor))
        {
            return BuildResult(
                request,
                new AgentRunSessionOperationFailed("The run's eligible session history could not be loaded."),
                [],
                new SessionVersion(0));
        }

        var currentVersion = initialCursor.Version;

        var modelResolution = await ResolveModelAsync(request, operationId, cancellationToken)
            .ConfigureAwait(false);

        if (modelResolution.Outcome is { } selectionFailure)
        {
            return BuildResult(request, selectionFailure, [], currentVersion);
        }

        var model = modelResolution.Model!;
        var llmModel = modelResolution.Adapter!;
        _ = Activity.Current?.SetTag(AgentKitTagNames.RequestModel, model.ModelId.ToString());
        _ = Activity.Current?.SetTag(AgentKitTagNames.ProviderName, model.ProviderId.ToString());

        var committedMessages = ImmutableArray.CreateBuilder<AgentMessage>();
        var history = new HistoryView(initialCursor, ToMessages(initialEntries), []);

        for (var turn = 1; turn <= request.MaxTurns; turn++)
        {
            var result = await RunTurnAsync(
                request,
                model,
                llmModel,
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
            Debug.Assert(result.Cursor is not null, "A continuing turn retains an exact updated history cursor.");
            history = new HistoryView(result.Cursor, history.Messages.AddRange(result.NewMessages), []);
        }

        return BuildResult(request, new AgentRunTurnLimitReached(request.MaxTurns), committedMessages.ToImmutable(), currentVersion);
    }

    /// <summary>
    /// Chooses this run's model and resolves it to an executable adapter.
    /// </summary>
    /// <remarks>
    /// Selection happens once per run rather than once per turn, so every
    /// turn of a run talks to the same model and the same catalog version. A
    /// mid-run catalog reload therefore cannot silently move a conversation
    /// to a different provider.
    /// </remarks>
    private async Task<ModelResolution> ResolveModelAsync(
        AgentRunRequest request,
        OperationId operationId,
        CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "A validated run request is required to resolve a model.");

        var catalog = await _modelCatalog.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var scope = new SecurityAuthorizationScope(
            request.AgentId,
            request.SessionId,
            new InRunOperationCorrelation(operationId, request.RunId, turnId: null));

        var selectionRequest = new ModelSelectionRequest(
            scope,
            _modelRequestIds.Create(),
            request.Agent?.Models ?? request.ModelPolicy,
            request.Agent?.ModelRequirements ?? request.ModelRequirements,
            catalog);

        var selection = await _modelSelector
            .SelectAsync(selectionRequest, cancellationToken)
            .ConfigureAwait(false);

        switch (selection)
        {
            case InvalidModelPolicy invalid:
                LoopLog.ModelSelectionFailed(_logger, request.RunId, invalid.Reason);
                return ModelResolution.Failed(
                    new AgentRunModelSelectionFailed(invalid.Reason, []));

            case NoCompatibleModel none:
                LoopLog.ModelSelectionFailed(
                    _logger,
                    request.RunId,
                    "no compatible model");
                return ModelResolution.Failed(new AgentRunModelSelectionFailed(
                    "No configured model satisfies this run's requirements.",
                    none.Diagnostics));

            case ModelSelected selected:
                var descriptor = selected.Decision.Model;
                var adapter = _llmModelResolver.Resolve(descriptor);
                if (adapter is null)
                {
                    LoopLog.ModelSelectionFailed(
                        _logger,
                        request.RunId,
                        "no adapter registered for the selected model");
                    return ModelResolution.Failed(new AgentRunModelSelectionFailed(
                        $"Model alias '{descriptor.Alias}' is configured in the catalog but no "
                        + "LLM model adapter is registered to execute it.",
                        selected.Decision.Diagnostics));
                }

                return ModelResolution.Resolved(descriptor, adapter);

            default:
                throw new InvalidOperationException(
                    $"Unrecognized {nameof(ModelSelectionResult)} kind '{selection.GetType()}'.");
        }
    }

    private async Task<TurnOutcome> RunTurnAsync(
        AgentRunRequest request,
        ModelDescriptor model,
        ILlmModel llmModel,
        OperationId operationId,
        int turn,
        HistoryView history,
        ImmutableArray<AgentMessage>.Builder committedMessages,
        SessionVersion currentVersion,
        CancellationToken cancellationToken)
    {
        var turnId = _turnIds.Create();
        var turnCorrelation = new InRunOperationCorrelation(operationId, request.RunId, turnId);
        var (turnAuthorization, turnCaptureFailure) = await CaptureAuthorizationAsync(
            request, turnCorrelation, cancellationToken)
            .ConfigureAwait(false);
        if (turnAuthorization is null)
        {
            return TurnOutcome.Settled(
                new AgentRunSessionOperationFailed(turnCaptureFailure!), currentVersion);
        }

        var turnSessionContext = new SessionOperationContext(
            request.AgentId,
            request.SessionId,
            executionLaneId: null,
            turnCorrelation,
            request.Identity,
            turnAuthorization);
        var modelRequestId = _modelRequestIds.Create();
        using var turnActivity = AgentKitDiagnostics.Activities.StartActivity(
            AgentKitActivityNames.AgentTurn,
            ActivityKind.Internal,
            parentContext: Activity.Current?.Context ?? default,
            tags: new ActivityTagsCollection
            {
                { AgentKitTagNames.GenAiOperationName, AgentKitActivityNames.AgentTurn },
                { AgentKitTagNames.AgentId, request.AgentId.ToString() },
                { AgentKitTagNames.SessionId, request.SessionId.ToString() },
                { AgentKitTagNames.RunId, request.RunId.ToString() },
                { AgentKitTagNames.TurnId, turnId.ToString() },
                { AgentKitTagNames.TurnNumber, turn },
                { AgentKitTagNames.ModelRequestId, modelRequestId.ToString() },
            });
        LoopLog.TurnStarted(_logger, request.RunId, turnId, turn);

        var assembleRequest = request is { Agent: { } agent, Configuration: { } configuration }
            ? new ContextAssemblyRequest(
                request.AgentId,
                request.SessionId,
                request.BranchId,
                request.RunId,
                turnId,
                modelRequestId,
                model,
                agent.Instructions,
                new ContextAssemblyEvidence(agent, request.Identity, history, turnAuthorization, configuration),
                agent.Tools,
                agent.ToolChoice,
                agent.Settings,
                ExtensionData.Empty)
            : new ContextAssemblyRequest(
                request.AgentId,
                request.SessionId,
                request.BranchId,
                request.RunId,
                turnId,
                modelRequestId,
                model,
                request.Instructions,
                history.Messages,
                request.Tools,
                request.ToolChoice,
                request.Settings,
                ExtensionData.Empty);

        var assembleResult = await _contextAssembler.AssembleAsync(assembleRequest, cancellationToken).ConfigureAwait(false);

        if (assembleResult is ContextPreparationFailed prepFailed)
        {
            turnActivity.SetFailed("context_preparation_failed", prepFailed.Failure.Kind.ToString());
            LoopLog.TurnFailed(_logger, request.RunId, turnId, "context_preparation_failed");
            return TurnOutcome.Settled(new AgentRunContextPreparationFailed(prepFailed.Failure), currentVersion);
        }

        var context = ((ContextReady) assembleResult).Context;
        var deadline = _timeProvider.GetUtcNow() + request.AttemptTimeout;
        var chatRequest = new LlmModelRequest(context, attempt: 1, deadline, ProviderRequestOptions.Empty);

        ModelAttemptResult attemptResult;
        using (var modelActivity = AgentKitDiagnostics.Activities.StartActivity(
            AgentKitActivityNames.Chat,
            ActivityKind.Client,
            parentContext: Activity.Current?.Context ?? default,
            tags: new ActivityTagsCollection
            {
                { AgentKitTagNames.GenAiOperationName, AgentKitActivityNames.Chat },
                { AgentKitTagNames.ModelRequestId, modelRequestId.ToString() },
                { AgentKitTagNames.RequestModel, model.ModelId.ToString() },
                { AgentKitTagNames.ProviderName, model.ProviderId.ToString() },
            }))
        {
            LoopLog.ModelRequestStarted(_logger, request.RunId, turnId, modelRequestId, model.Alias);
            try
            {
                attemptResult = await llmModel.ExecuteAsync(
                    chatRequest,
                    request.Observer is null
                        ? NoOpModelResponseObserver.Instance
                        : new RunModelResponseObserver(
                            turnId,
                            (runEvent, token) => ObserveAsync(request, runEvent, token)),
                    cancellationToken).ConfigureAwait(false);
                if (attemptResult is ModelAttemptCompleted)
                {
                    modelActivity.SetSuccessful("completed");
                }
                else
                {
                    modelActivity.SetFailed(attemptResult.GetType().Name, attemptResult.GetType().Name);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                modelActivity.SetFailed("cancelled", "cancellation");
                throw;
            }
            catch (Exception exception)
            {
                modelActivity.SetFailed("faulted", exception.GetType().FullName ?? exception.GetType().Name);
                throw;
            }
        }

        var turnOutcome = attemptResult switch
        {
            ModelAttemptFailed failed => await SettleInterruptedAsync(
                request, model, history.SourceCursor, turnSessionContext, turnCorrelation, turnId, modelRequestId,
                failed.PartialParts, failed.Usage, NormalizedStopReason.Error, failed.Failure.RequestId,
                new AgentRunFailed(failed.Failure), committedMessages, currentVersion)
                .ConfigureAwait(false),

            ModelAttemptCancelled cancelled => await SettleInterruptedAsync(
                request, model, history.SourceCursor, turnSessionContext, turnCorrelation, turnId, modelRequestId,
                cancelled.PartialParts, cancelled.Usage, NormalizedStopReason.Cancelled,
                cancelled.Cancellation.RequestId, new AgentRunCancelled(cancelled.Cancellation.SafeMessage),
                committedMessages, currentVersion)
                .ConfigureAwait(false),

            ModelAttemptCompleted completed => await SettleCompletedAsync(
                request, model, history.SourceCursor, turnSessionContext, turnCorrelation, turnId, turn, completed.Response,
                committedMessages, currentVersion, cancellationToken)
                .ConfigureAwait(false),

            _ => throw new InvalidOperationException(
                $"Unrecognized {nameof(ModelAttemptResult)} kind '{attemptResult.GetType()}'."),
        };

        if (turnOutcome.Outcome is null or AgentRunCompleted)
        {
            turnActivity.SetSuccessful(turnOutcome.Outcome?.GetType().Name ?? "continue");
            LoopLog.TurnCompleted(_logger, request.RunId, turnId, turnOutcome.Outcome?.GetType().Name ?? "continue");
        }
        else
        {
            var outcome = turnOutcome.Outcome.GetType().Name;
            turnActivity.SetFailed(outcome, outcome);
            LoopLog.TurnFailed(_logger, request.RunId, turnId, outcome);
        }

        return turnOutcome;
    }

    private async Task<TurnOutcome> SettleCompletedAsync(
        AgentRunRequest request,
        ModelDescriptor model,
        MessageCursor sourceCursor,
        SessionOperationContext turnSessionContext,
        InRunOperationCorrelation turnCorrelation,
        TurnId turnId,
        int turn,
        ModelResponse response,
        ImmutableArray<AgentMessage>.Builder committedMessages,
        SessionVersion currentVersion,
        CancellationToken cancellationToken)
    {
        // A terminal is accepted as a complete turn only when the provider reports it finished by choice
        // (Completed) or by requesting tools (ToolUse). Length, Pending, Error, or Cancelled terminals are
        // truthful partial output: they are preserved as an Interrupted message and settle the run as a typed
        // failure rather than pretending the agent chose to finish.
        if (response.StopReason is not (NormalizedStopReason.Completed or NormalizedStopReason.ToolUse))
        {
            LoopLog.ModelResponseNotAccepted(_logger, request.RunId, turnId, $"stop reason {response.StopReason}");
            return await SettleInterruptedAsync(
                request, model, sourceCursor, turnSessionContext, turnCorrelation, turnId, response.RequestId,
                response.Parts, response.Usage, response.StopReason, response.Identity.RequestId,
                new AgentRunFailed(new ProviderFailure(
                    ProviderFailureKind.Unknown, response.Identity.ProviderId, response.Identity.RequestId,
                    statusCode: null, providerCode: null, retryAfter: null,
                    $"The model stopped before completing its output (stop reason: {response.StopReason}).",
                    diagnosticCause: null, ExtensionData.Empty)),
                committedMessages, currentVersion).ConfigureAwait(false);
        }

        // Duplicate call identities cannot be honoured: one identity must never produce two effects, and the
        // history contract requires exactly one terminal result per call. The response is a protocol violation.
        var requestedCalls = response.Parts.OfType<ToolCallPart>().ToImmutableArray();
        if (requestedCalls.Select(static call => call.CallId).Distinct().Count() != requestedCalls.Length)
        {
            LoopLog.ModelResponseNotAccepted(_logger, request.RunId, turnId, "duplicate tool call identities");
            return await SettleInterruptedAsync(
                request, model, sourceCursor, turnSessionContext, turnCorrelation, turnId, response.RequestId,
                response.Parts, response.Usage, NormalizedStopReason.Error, response.Identity.RequestId,
                new AgentRunFailed(new ProviderFailure(
                    ProviderFailureKind.ProtocolViolation, response.Identity.ProviderId, response.Identity.RequestId,
                    statusCode: null, providerCode: null, retryAfter: null,
                    "The model response requested the same tool call identity more than once.",
                    diagnosticCause: null, ExtensionData.Empty)),
                committedMessages, currentVersion).ConfigureAwait(false);
        }

        var now = _timeProvider.GetUtcNow();
        var assistantMessage = new AssistantMessage(
            _messageIds.Create(),
            request.AgentId,
            request.SessionId,
            sourceCursor.ConversationId,
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
            new SessionSequence(sourceCursor.Sequence.Value + 1),
            causalParentId: null,
            now,
            new SchemaVersion("1"),
            assistantMessage);

        var appendResult = await AppendWithDiagnosticsAsync(
            new SessionAppendRequest(
                turnSessionContext,
                request.BranchId,
                currentVersion,
                new IdempotencyKey($"run:{request.RunId}:turn:{turnId}:assistant"),
                [assistantEntry]),
            request.SessionProfile,
            cancellationToken).ConfigureAwait(false);

        if (appendResult is not SessionAppended appended)
        {
            return TurnOutcome.Settled(
                new AgentRunSessionOperationFailed(DescribeAppendFailure(appendResult)), currentVersion);
        }

        currentVersion = appended.NewVersion;
        committedMessages.Add(assistantMessage);

        var toolCalls = requestedCalls;

        return toolCalls switch
        {
            { IsEmpty: true } => TurnOutcome.Settled(new AgentRunCompleted(assistantMessage), currentVersion),
            _ when turn == request.MaxTurns => await SettleRejectedAtTurnLimitAsync(
                request, sourceCursor, turnSessionContext, turnCorrelation, turnId, assistantEntryId, toolCalls,
                committedMessages, currentVersion, appended.CommittedEntries[^1].Sequence)
                .ConfigureAwait(false),
            _ => await InvokeToolsAsync(
                request, sourceCursor, turnSessionContext, turnCorrelation, turnId, assistantEntryId, assistantMessage, toolCalls,
                committedMessages, currentVersion, appended.CommittedEntries[^1].Sequence, cancellationToken)
                .ConfigureAwait(false),
        };
    }

    /// <summary>
    /// Settles every tool call requested on the final permitted turn with a rejected terminal result, so the
    /// already-committed assistant message never leaves a call without its exactly-one result.
    /// </summary>
    private async Task<TurnOutcome> SettleRejectedAtTurnLimitAsync(
        AgentRunRequest request,
        MessageCursor sourceCursor,
        SessionOperationContext turnSessionContext,
        InRunOperationCorrelation turnCorrelation,
        TurnId turnId,
        SessionEntryId assistantEntryId,
        ImmutableArray<ToolCallPart> toolCalls,
        ImmutableArray<AgentMessage>.Builder committedMessages,
        SessionVersion currentVersion,
        SessionSequence currentSequence)
    {
        LoopLog.ToolBatchRejectedAtTurnLimit(_logger, request.RunId, turnId, toolCalls.Length);
        var resultParts = ImmutableArray.CreateBuilder<ContentPart>(toolCalls.Length);
        foreach (var toolCall in toolCalls)
        {
            var rejected = new ToolResultPart(
                toolCall.CallId,
                toolCall.Tool,
                new ToolCallOutcome(
                    ToolCallOutcomeKind.Rejected,
                    ToolTerminalStatus.Denied,
                    SideEffectCertainty.DefinitelyNotPerformed,
                    retryable: false,
                    "The run reached its turn limit before this tool call could be invoked.",
                    ExtensionData.Empty),
                [],
                ExtensionData.Empty);
            resultParts.Add(rejected);
            await ObserveAsync(request, new AgentRunToolCallCompleted(turnId, rejected), CancellationToken.None)
                .ConfigureAwait(false);
        }

        var appendResult = await CommitToolMessageAsync(
            request, sourceCursor, turnSessionContext, turnCorrelation, turnId, assistantEntryId,
            resultParts.ToImmutable(), currentVersion, currentSequence, committedMessages).ConfigureAwait(false);

        return appendResult is SessionAppended appended
            ? TurnOutcome.Settled(new AgentRunTurnLimitReached(request.MaxTurns), appended.NewVersion)
            : TurnOutcome.Settled(new AgentRunSessionOperationFailed(DescribeAppendFailure(appendResult)), currentVersion);
    }

    /// <summary>
    /// Builds and durably commits the tool message carrying one terminal result per requested call. The commit
    /// always uses <see cref="CancellationToken.None"/>: the assistant message that requested these calls is already
    /// committed, so its results must land no matter how the run itself settles.
    /// </summary>
    private async ValueTask<SessionAppendResult> CommitToolMessageAsync(
        AgentRunRequest request,
        MessageCursor sourceCursor,
        SessionOperationContext turnSessionContext,
        InRunOperationCorrelation turnCorrelation,
        TurnId turnId,
        SessionEntryId assistantEntryId,
        ImmutableArray<ContentPart> resultParts,
        SessionVersion currentVersion,
        SessionSequence currentSequence,
        ImmutableArray<AgentMessage>.Builder committedMessages)
    {
        var now = _timeProvider.GetUtcNow();
        var toolMessage = new ToolMessage(
            _messageIds.Create(),
            request.AgentId,
            request.SessionId,
            sourceCursor.ConversationId,
            request.BranchId,
            request.RunId,
            turnId,
            now,
            MessageState.Complete,
            resultParts,
            ExtensionData.Empty);

        var toolEntry = new MessageSessionEntry(
            _entryIds.Create(),
            turnSessionContext.ToAddress(),
            turnCorrelation,
            request.BranchId,
            new SessionSequence(currentSequence.Value + 1),
            assistantEntryId,
            now,
            new SchemaVersion("1"),
            toolMessage);

        var appendResult = await AppendWithDiagnosticsAsync(
            new SessionAppendRequest(
                turnSessionContext,
                request.BranchId,
                currentVersion,
                new IdempotencyKey($"run:{request.RunId}:turn:{turnId}:tools"),
                [toolEntry]),
            request.SessionProfile,
            CancellationToken.None).ConfigureAwait(false);

        if (appendResult is SessionAppended)
        {
            committedMessages.Add(toolMessage);
        }

        return appendResult;
    }

    private async Task<TurnOutcome> InvokeToolsAsync(
        AgentRunRequest request,
        MessageCursor sourceCursor,
        SessionOperationContext turnSessionContext,
        InRunOperationCorrelation turnCorrelation,
        TurnId turnId,
        SessionEntryId assistantEntryId,
        AgentMessage assistantMessage,
        ImmutableArray<ToolCallPart> toolCalls,
        ImmutableArray<AgentMessage>.Builder committedMessages,
        SessionVersion currentVersion,
        SessionSequence currentSequence,
        CancellationToken cancellationToken)
    {
        using var activity = AgentKitDiagnostics.Activities.StartActivity(
            AgentKitActivityNames.ToolBatch,
            ActivityKind.Internal,
            parentContext: Activity.Current?.Context ?? default,
            tags: new ActivityTagsCollection
            {
                { AgentKitTagNames.GenAiOperationName, AgentKitActivityNames.ToolBatch },
                { AgentKitTagNames.AgentId, request.AgentId.ToString() },
                { AgentKitTagNames.SessionId, request.SessionId.ToString() },
                { AgentKitTagNames.RunId, request.RunId.ToString() },
                { AgentKitTagNames.TurnId, turnId.ToString() },
                { "agentkit.tool.count", toolCalls.Length },
            });
        LoopLog.ToolBatchStarted(_logger, request.RunId, turnId, toolCalls.Length);
        var resultParts = ImmutableArray.CreateBuilder<ContentPart>(toolCalls.Length);
        var interrupted = false;
        foreach (var toolCall in toolCalls)
        {
            // A prior call may have absorbed cancellation into an ordinary settled outcome instead of throwing
            // (see the remark on the commit below), so cancellation is also checked explicitly here: once
            // requested, no further not-yet-attempted call in this batch is started.
            if (interrupted || cancellationToken.IsCancellationRequested)
            {
                interrupted = true;
                var skippedResult = InterruptedResultPart(toolCall);
                resultParts.Add(skippedResult);
                await ObserveAsync(
                    request,
                    new AgentRunToolCallCompleted(turnId, skippedResult),
                    CancellationToken.None).ConfigureAwait(false);
                continue;
            }

            var toolContext = new ToolExecutionContext(
                request.AgentId,
                request.SessionId,
                toolCall.CallId,
                turnCorrelation,
                request.Identity,
                turnSessionContext.Authorization,
                request.SessionProfile);

            ToolResultPart resultPart;
            try
            {
                await ObserveAsync(
                    request,
                    new AgentRunToolCallStarted(turnId, toolCall),
                    cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                var invocationResult = await _toolInvoker.InvokeAsync(
                    new ToolCallRequest(toolCall.Tool.Id, toolContext, toolCall.Arguments, _timeProvider.GetUtcNow()),
                    cancellationToken).ConfigureAwait(false);

                resultPart = new ToolResultPart(
                    toolCall.CallId, toolCall.Tool, invocationResult.Outcome, invocationResult.Content, ExtensionData.Empty);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A cancelled tool call must still receive a matching terminal result: the assistant message
                // that requested these calls is already committed, and leaving any of them without a result
                // would permanently break causality for every later turn in this session (context assembly
                // requires exactly one terminal result per requested call). Every remaining, not-yet-attempted
                // call in this batch is settled the same way below, and the whole batch is committed with
                // CancellationToken.None before this cancellation is allowed to propagate.
                interrupted = true;
                resultPart = InterruptedResultPart(toolCall);
            }
            catch (Exception exception)
            {
                // An invoker fault (resolution, authorization, or an unguarded tool exception) is a terminal
                // failure for this call, not for the session: the call still receives its exactly-one result.
                LoopLog.ToolCallFaulted(_logger, request.RunId, toolCall.CallId, exception.GetType().FullName ?? exception.GetType().Name);
                resultPart = new ToolResultPart(
                    toolCall.CallId,
                    toolCall.Tool,
                    new ToolCallOutcome(
                        ToolCallOutcomeKind.Failed,
                        ToolTerminalStatus.InvocationFailed,
                        SideEffectCertainty.Unknown,
                        retryable: false,
                        "The tool invocation faulted before it produced a result.",
                        ExtensionData.Empty),
                    [],
                    ExtensionData.Empty);
            }

            resultParts.Add(resultPart);
            await ObserveAsync(
                request,
                new AgentRunToolCallCompleted(turnId, resultPart),
                CancellationToken.None).ConfigureAwait(false);
        }

        // The commit deliberately ignores the caller's cancellation (see CommitToolMessageAsync). A tool invoker
        // may also absorb cancellation into an ordinary ToolCallOutcomeKind.Cancelled result instead of throwing
        // (for example, a process runner that kills its child process and returns a settled "cancelled" outcome) —
        // cancellationToken.IsCancellationRequested is checked explicitly below, after this commit, so that case
        // still propagates cancellation to the caller instead of silently continuing to the next turn.
        var appendResult = await CommitToolMessageAsync(
            request, sourceCursor, turnSessionContext, turnCorrelation, turnId, assistantEntryId,
            resultParts.ToImmutable(), currentVersion, currentSequence, committedMessages).ConfigureAwait(false);

        if (appendResult is not SessionAppended appended)
        {
            activity.SetFailed("session_append_failed", appendResult.GetType().Name);
            LoopLog.ToolBatchFailed(_logger, request.RunId, turnId, appendResult.GetType().Name);
            return TurnOutcome.Settled(
                new AgentRunSessionOperationFailed(DescribeAppendFailure(appendResult)), currentVersion);
        }

        var toolMessage = committedMessages[^1];

        if (interrupted || cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", "cancellation");
            LoopLog.ToolBatchInterrupted(_logger, request.RunId, turnId);
            cancellationToken.ThrowIfCancellationRequested();
        }

        activity.SetSuccessful("completed");
        LoopLog.ToolBatchCompleted(_logger, request.RunId, turnId, toolCalls.Length);
        var committedSequence = appended.CommittedEntries[^1].Sequence;
        var nextCursor = new MessageCursor(
            sourceCursor.AgentId,
            sourceCursor.SessionId,
            sourceCursor.ConversationId,
            sourceCursor.BranchId,
            appended.NewVersion,
            committedSequence);
        return TurnOutcome.Continue(nextCursor, [assistantMessage, toolMessage]);
    }

    /// <summary>Builds a settled, cancelled terminal result for a tool call that was interrupted or never attempted.</summary>
    /// <param name="toolCall">The requested call to settle.</param>
    /// <returns>A result part recording <see cref="ToolTerminalStatus.Interrupted"/> with no returned content.</returns>
    private static ToolResultPart InterruptedResultPart(ToolCallPart toolCall) => new(
        toolCall.CallId,
        toolCall.Tool,
        new ToolCallOutcome(
            ToolCallOutcomeKind.Cancelled,
            ToolTerminalStatus.Interrupted,
            SideEffectCertainty.Unknown,
            retryable: true,
            "The run was cancelled before this tool call completed.",
            ExtensionData.Empty),
        [],
        ExtensionData.Empty);

    /// <summary>Delivers optional run progress without allowing presentation failure to alter run semantics.</summary>
    /// <param name="request">The run whose observer receives the event.</param>
    /// <param name="runEvent">The immutable event to deliver.</param>
    /// <param name="cancellationToken">Bounds delivery; cancellation is isolated like every observer failure.</param>
    /// <returns>An operation completing after delivery succeeds or is safely dropped.</returns>
    private async ValueTask ObserveAsync(
        AgentRunRequest request,
        AgentRunEvent runEvent,
        CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "A validated run request is required for observer delivery.");
        Debug.Assert(runEvent is not null, "A run event is required for observer delivery.");
        if (request.Observer is null)
        {
            return;
        }

        try
        {
            await request.Observer.OnEventAsync(runEvent, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            LoopLog.RunObserverFailed(_logger, request.RunId, runEvent.GetType().Name);
        }
    }

    private async ValueTask<SessionAppendResult> AppendWithDiagnosticsAsync(
        SessionAppendRequest request,
        SessionProfileSnapshot sessionProfile,
        CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "A validated append request is required for session diagnostics.");
        using var activity = AgentKitDiagnostics.Activities.StartActivity(
            AgentKitActivityNames.SessionCommit,
            ActivityKind.Internal,
            parentContext: Activity.Current?.Context ?? default,
            tags: new ActivityTagsCollection
            {
                { AgentKitTagNames.GenAiOperationName, AgentKitActivityNames.SessionCommit },
                { AgentKitTagNames.AgentId, request.Context.AgentId.ToString() },
                { AgentKitTagNames.SessionId, request.Context.SessionId.ToString() },
                { AgentKitTagNames.OperationId, request.Context.Correlation.OperationId.ToString() },
            });

        var attemptRequest = request;
        SessionAppendResult result;
        for (var attempt = 1; ; attempt++)
        {
            result = await _sessionCoordinator.AppendAsync(
                attemptRequest, sessionProfile, cancellationToken).ConfigureAwait(false);
            if (result is not SessionAppendConflict conflict || attempt >= _maxAppendConflictRetries)
            {
                break;
            }

            LoopLog.SessionAppendConflictRetried(
                _logger, request.Context.SessionId, conflict.ExpectedVersion, conflict.ActualVersion, attempt);

            // Each entry's own Sequence was assigned from the stale tip at build time; rebasing only ExpectedVersion
            // is not enough. Version and sequence advance independently (one version per append, one sequence per
            // entry), so the actual tip sequence must be re-read rather than derived from the conflict's version.
            var tipResult = await _sessionCoordinator.ReadAsync(
                new SessionReadRequest(attemptRequest.Context, attemptRequest.BranchId, attemptRequest.Entries[0].Sequence, 1),
                sessionProfile,
                cancellationToken).ConfigureAwait(false);
            if (tipResult is not SessionPage { Snapshot: { } tip })
            {
                break;
            }

            var rebasedEntries = attemptRequest.Entries
                .Select((entry, index) => entry with { Sequence = new SessionSequence(tip.UpperSequence.Value + index + 1) })
                .ToImmutableArray();
            attemptRequest = new SessionAppendRequest(
                attemptRequest.Context,
                attemptRequest.BranchId,
                tip.Version,
                attemptRequest.IdempotencyKey,
                rebasedEntries);
        }

        if (result is SessionAppended)
        {
            activity.SetSuccessful("committed");
        }
        else
        {
            activity.SetFailed("failed", result.GetType().Name);
            LoopLog.SessionCommitFailed(_logger, request.Context.SessionId, result.GetType().Name);
        }

        return result;
    }

    private async Task<TurnOutcome> SettleInterruptedAsync(
        AgentRunRequest request,
        ModelDescriptor model,
        MessageCursor sourceCursor,
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
        SessionVersion currentVersion)
    {
        if (partialParts.IsEmpty)
        {
            return TurnOutcome.Settled(outcome, currentVersion);
        }

        var now = _timeProvider.GetUtcNow();
        var identity = new ProviderResponseIdentity(
            model.ProviderId,
            upstreamProviderId: null,
            model.ApiFamily,
            model.ModelId,
            model.ModelId,
            model.DeploymentId,
            providerRequestId,
            responseId: null);

        var interruptedMessage = new AssistantMessage(
            _messageIds.Create(),
            request.AgentId,
            request.SessionId,
            sourceCursor.ConversationId,
            request.BranchId,
            request.RunId,
            turnId,
            now,
            MessageState.Interrupted,
            partialParts,
            new AssistantResponseMetadata(
                modelRequestId, identity, stopReason, rawStopReason: null, usage ?? ModelUsage.NotReported, ExtensionData.Empty),
            ExtensionData.Empty);

        var entry = new MessageSessionEntry(
            _entryIds.Create(),
            turnSessionContext.ToAddress(),
            turnCorrelation,
            request.BranchId,
            new SessionSequence(sourceCursor.Sequence.Value + 1),
            causalParentId: null,
            now,
            new SchemaVersion("1"),
            interruptedMessage);

        // Partial output is committed with CancellationToken.None: the caller's token is usually the very reason
        // the attempt was interrupted, and a cancelled token would otherwise discard output the class promises to keep.
        var appendResult = await AppendWithDiagnosticsAsync(
            new SessionAppendRequest(
                turnSessionContext,
                request.BranchId,
                currentVersion,
                new IdempotencyKey($"run:{request.RunId}:turn:{turnId}:interrupted"),
                [entry]),
            request.SessionProfile,
            CancellationToken.None).ConfigureAwait(false);

        if (appendResult is not SessionAppended appended)
        {
            return TurnOutcome.Settled(
                new AgentRunSessionOperationFailed(DescribeAppendFailure(appendResult)), currentVersion);
        }

        committedMessages.Add(interruptedMessage);
        return TurnOutcome.Settled(outcome, appended.NewVersion);
    }

    private async Task<(ImmutableArray<SessionEntry> Entries, MessageCursor Cursor)?> LoadHistoryAsync(
        SessionOperationContext sessionContext,
        SessionProfileSnapshot sessionProfile,
        BranchId branchId,
        CancellationToken cancellationToken)
    {
        var loadResult = await _sessionCoordinator.LoadAsync(sessionContext, sessionProfile, cancellationToken)
            .ConfigureAwait(false);
        if (loadResult is not SessionLoaded loaded)
        {
            return null;
        }

        if (loaded.Descriptor.Address != sessionContext.ToAddress())
        {
            return null;
        }

        var entries = ImmutableArray.CreateBuilder<SessionEntry>();
        var cursor = new SessionSequence(0);
        SessionReadSnapshot? snapshot = null;

        while (true)
        {
            var pageResult = await _sessionCoordinator.ReadAsync(
                snapshot is null
                    ? new SessionReadRequest(sessionContext, branchId, cursor, _historyReadPageSize)
                    : new SessionReadRequest(sessionContext, branchId, cursor, _historyReadPageSize, snapshot),
                sessionProfile,
                cancellationToken)
                .ConfigureAwait(false);

            if (pageResult is not SessionPage { Snapshot: { } pageSnapshot } page)
            {
                return null;
            }

            snapshot ??= pageSnapshot;
            if (pageSnapshot != snapshot)
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

        Debug.Assert(snapshot is not null, "A successful first page supplies exact snapshot evidence.");
        var reachedBoundary = cursor == snapshot.UpperSequence
            || (entries.Count == 0 && cursor.Value > snapshot.UpperSequence.Value);
        return reachedBoundary
            ? (
                entries.ToImmutable(),
                new MessageCursor(
                    sessionContext.AgentId,
                    sessionContext.SessionId,
                    loaded.Descriptor.ConversationId,
                    branchId,
                    snapshot.Version,
                    snapshot.UpperSequence))
            : null;
    }

    private async ValueTask<(SecurityAuthorizationContext? Authorization, string? SafeFailure)>
        CaptureAuthorizationAsync(
            AgentRunRequest request,
            InRunOperationCorrelation correlation,
            CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "A validated run request is required for authorization capture.");
        Debug.Assert(correlation is not null, "A concrete in-run correlation is required for authorization capture.");

        var baseline = request.Authorization;
        var scope = new SecurityAuthorizationScope(request.AgentId, request.SessionId, correlation);
        var result = await _securityProfileSelector.SelectAsync(
            new SecurityAuthorizationCaptureRequest(
                scope,
                baseline.ProfileKey,
                baseline.AgentDefinitionRevision,
                baseline.ConfigurationVersion,
                request.Identity),
            cancellationToken).ConfigureAwait(false);

        if (result is not SecurityAuthorizationCaptured captured)
        {
            return (null, result is SecurityAuthorizationCaptureUnavailable unavailable
                ? unavailable.SafeReason
                : "The security profile could not be captured for this operation.");
        }

        var authorization = captured.Authorization;
        if (authorization.Scope != scope
            || authorization.Identity != request.Identity
            || authorization.ProfileKey != baseline.ProfileKey
            || authorization.ProfileVersion != baseline.ProfileVersion
            || authorization.PolicySnapshot != baseline.PolicySnapshot
            || authorization.AuthorityKey != baseline.AuthorityKey
            || authorization.AgentDefinitionRevision != baseline.AgentDefinitionRevision
            || authorization.ConfigurationVersion != baseline.ConfigurationVersion)
        {
            return (null, "The captured security profile differs from the run-start evidence.");
        }

        return (authorization, null);
    }

    private void RecordRunMetrics(string outcome, long startedTimestamp)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(outcome), "A normalized outcome is required for run metrics.");
        var tags = new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome);
        LoopMetrics.Runs.Add(1, tags);
        LoopMetrics.RunDuration.Record(_timeProvider.GetElapsedTime(startedTimestamp).TotalSeconds, tags);
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

    /// <summary>
    /// The result of choosing this run's model: either a terminal outcome
    /// that settles the run before any turn starts, or the chosen descriptor
    /// paired with the adapter that executes it.
    /// </summary>
    private readonly struct ModelResolution
    {
        private ModelResolution(AgentRunOutcome? outcome, ModelDescriptor? model, ILlmModel? adapter)
        {
            Outcome = outcome;
            Model = model;
            Adapter = adapter;
        }

        /// <summary>Gets the terminal outcome, when no model could be used.</summary>
        public AgentRunOutcome? Outcome { get; }

        /// <summary>Gets the chosen descriptor, when resolution succeeded.</summary>
        public ModelDescriptor? Model { get; }

        /// <summary>Gets the adapter that executes the chosen model, when resolution succeeded.</summary>
        public ILlmModel? Adapter { get; }

        /// <summary>Creates a successful resolution.</summary>
        public static ModelResolution Resolved(ModelDescriptor model, ILlmModel adapter) =>
            new(null, model, adapter);

        /// <summary>Creates a resolution that settles the run before it starts.</summary>
        public static ModelResolution Failed(AgentRunOutcome outcome) => new(outcome, null, null);
    }

    /// <summary>The result of running one turn: either it settled the run, or it should continue to another turn.</summary>
    private readonly struct TurnOutcome
    {
        private TurnOutcome(AgentRunOutcome? outcome, SessionVersion version, MessageCursor? cursor, ImmutableArray<AgentMessage> newMessages)
        {
            Outcome = outcome;
            Version = version;
            Cursor = cursor;
            NewMessages = newMessages;
        }

        /// <summary>Gets the terminal outcome, when the run settled during this turn.</summary>
        public AgentRunOutcome? Outcome { get; }

        /// <summary>Gets the session version after this turn's commits.</summary>
        public SessionVersion Version { get; }

        /// <summary>Gets the exact history cursor after a continuing turn.</summary>
        public MessageCursor? Cursor { get; }

        /// <summary>Gets the messages newly visible to the next turn's history, when continuing.</summary>
        public ImmutableArray<AgentMessage> NewMessages { get; }

        /// <summary>Creates a settled outcome.</summary>
        public static TurnOutcome Settled(AgentRunOutcome outcome, SessionVersion version) => new(outcome, version, null, []);

        /// <summary>Creates a continuation outcome.</summary>
        public static TurnOutcome Continue(MessageCursor cursor, ImmutableArray<AgentMessage> newMessages)
        {
            ArgumentNullException.ThrowIfNull(cursor);
            return new(null, cursor.Version, cursor, newMessages);
        }
    }
}
