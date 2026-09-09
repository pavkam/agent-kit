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
        if (historyLoad is not var (initialEntries, loadedVersion))
        {
            return BuildResult(
                request,
                new AgentRunSessionOperationFailed("The run's eligible session history could not be loaded."),
                [],
                new SessionVersion(0));
        }

        var currentVersion = loadedVersion;

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
        var history = ToMessages(initialEntries);

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
            history = history.AddRange(result.NewMessages);
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
            request.ModelPolicy,
            request.ModelRequirements,
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
        ImmutableArray<AgentMessage> history,
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

        var assembleRequest = new ContextAssemblyRequest(
            request.AgentId,
            request.SessionId,
            request.BranchId,
            request.RunId,
            turnId,
            modelRequestId,
            model,
            request.Instructions,
            history,
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
                    NoOpModelResponseObserver.Instance,
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
                request, model, turnSessionContext, turnCorrelation, turnId, modelRequestId,
                failed.PartialParts, failed.Usage, NormalizedStopReason.Error, failed.Failure.RequestId,
                new AgentRunFailed(failed.Failure), committedMessages, currentVersion, cancellationToken)
                .ConfigureAwait(false),

            ModelAttemptCancelled cancelled => await SettleInterruptedAsync(
                request, model, turnSessionContext, turnCorrelation, turnId, modelRequestId,
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
        foreach (var toolCall in toolCalls)
        {
            var toolContext = new ToolExecutionContext(
                request.AgentId,
                request.SessionId,
                toolCall.CallId,
                turnCorrelation,
                request.Identity,
                turnSessionContext.Authorization,
                request.SessionProfile);

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

        var appendResult = await AppendWithDiagnosticsAsync(
            new SessionAppendRequest(
                turnSessionContext,
                request.BranchId,
                currentVersion,
                new IdempotencyKey($"run:{request.RunId}:turn:{turnId}:tools"),
                [toolEntry]),
            request.SessionProfile,
            cancellationToken).ConfigureAwait(false);

        if (appendResult is not SessionAppended appended)
        {
            activity.SetFailed("session_append_failed", appendResult.GetType().Name);
            LoopLog.ToolBatchFailed(_logger, request.RunId, turnId, appendResult.GetType().Name);
            return TurnOutcome.Settled(
                new AgentRunSessionOperationFailed(DescribeAppendFailure(appendResult)), currentVersion);
        }

        committedMessages.Add(toolMessage);
        activity.SetSuccessful("completed");
        LoopLog.ToolBatchCompleted(_logger, request.RunId, turnId, toolCalls.Length);
        return TurnOutcome.Continue(appended.NewVersion, [assistantMessage, toolMessage]);
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

        var result = await _sessionCoordinator.AppendAsync(
            request, sessionProfile, cancellationToken).ConfigureAwait(false);
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
            conversationId: null,
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
            new SessionSequence(currentVersion.Value + 1),
            causalParentId: null,
            now,
            new SchemaVersion("1.0"),
            interruptedMessage);

        var appendResult = await AppendWithDiagnosticsAsync(
            new SessionAppendRequest(
                turnSessionContext,
                request.BranchId,
                currentVersion,
                new IdempotencyKey($"run:{request.RunId}:turn:{turnId}:interrupted"),
                [entry]),
            request.SessionProfile,
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
        SessionOperationContext sessionContext,
        SessionProfileSnapshot sessionProfile,
        BranchId branchId,
        CancellationToken cancellationToken)
    {
        var entries = ImmutableArray.CreateBuilder<SessionEntry>();
        var cursor = new SessionSequence(0);

        while (true)
        {
            var pageResult = await _sessionCoordinator.ReadAsync(
                new SessionReadRequest(sessionContext, branchId, cursor, _historyReadPageSize),
                sessionProfile,
                cancellationToken)
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
