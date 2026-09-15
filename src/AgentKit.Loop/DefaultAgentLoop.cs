// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

using Microsoft.Extensions.DependencyInjection;
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
/// model attempt fails or is cancelled after producing partial output, or a
/// completed attempt reports a stop reason other than
/// <see cref="NormalizedStopReason.Completed"/> or
/// <see cref="NormalizedStopReason.ToolUse"/>, that output is first committed
/// as an <see cref="MessageState.Interrupted"/> <see cref="AssistantMessage"/>
/// so it is never discarded, before the run settles with the typed outcome
/// naming the cause: <see cref="AgentRunFailed"/>, <see cref="AgentRunCancelled"/>,
/// <see cref="AgentRunOutputLengthLimitReached"/>, or <see cref="AgentRunInvalidState"/>
/// for a deferral this loop cannot resume.
/// </para>
/// <para>
/// Caller cancellation propagates as <see cref="OperationCanceledException"/>
/// only while this run has committed nothing. After the first durable commit,
/// cancellation observed anywhere (a cancelled model attempt, a cancelled or
/// skipped tool call, or a cancelled wait between turns) settles the run with a
/// typed <see cref="AgentRunCancelled"/> outcome. The result then carries every
/// committed message and the exact branch version, and every requested tool
/// call in an interrupted batch has already received its terminal result.
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
    private readonly IRunContinuationPolicy _continuationPolicy;
    private readonly IIdentifierGenerator<OperationId> _operationIds;
    private readonly IIdentifierGenerator<TurnId> _turnIds;
    private readonly IIdentifierGenerator<ModelRequestId> _modelRequestIds;
    private readonly IIdentifierGenerator<MessageId> _messageIds;
    private readonly IIdentifierGenerator<SessionEntryId> _entryIds;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DefaultAgentLoop> _logger;
    private readonly int _historyReadPageSize;

    /// <summary>The most a single append is retried after a concurrent writer advanced the branch, before giving up.</summary>
    /// <remarks>
    /// A tool invoked mid-turn (the plan/todo tool, for one) may commit its own session entries directly through
    /// <see cref="ISessionCoordinator"/>, independently of this loop's own turn-scoped <c>currentVersion</c>
    /// tracking. <see cref="SessionAppendConflict"/> documents that the store deliberately never rebases or
    /// retries on the caller's behalf; a bounded retry in <see cref="AppendWithDiagnosticsAsync"/>, rebasing onto
    /// the re-read branch tip, is exactly the reload-and-reattempt the type's own remarks describe. Configured
    /// through <see cref="AgentLoopOptions.AppendConflictRetryLimit"/>.
    /// </remarks>
    private readonly int _appendConflictRetryLimit;

    /// <summary>The bound on each required terminal commit; see <see cref="AgentLoopOptions.SettlementTimeout"/>.</summary>
    private readonly TimeSpan _settlementTimeout;

    /// <summary>The bound on each detached observer delivery; see <see cref="AgentLoopOptions.ObserverDeliveryTimeout"/>.</summary>
    private readonly TimeSpan _observerDeliveryTimeout;

    /// <summary>Whether the final permitted turn is requested without tools; see <see cref="AgentLoopOptions.DisableToolsOnFinalTurn"/>.</summary>
    private readonly bool _disableToolsOnFinalTurn;

    /// <summary>
    /// The first backoff before a required terminal commit that the store reported as failed is retried under its
    /// unchanged idempotency key. It doubles per retry up to <see cref="_settlementRetryMaxDelay"/>; the overall
    /// bound is <see cref="AgentLoopOptions.SettlementTimeout"/>.
    /// </summary>
    private static readonly TimeSpan _settlementRetryBaseDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>The longest single backoff between settlement retries.</summary>
    private static readonly TimeSpan _settlementRetryMaxDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The single immutable run-policy version of this reduced loop. Its continuation-relevant behaviour is fixed
    /// in code rather than compiled from a per-agent policy snapshot, so every evaluation names the same version.
    /// </summary>
    private static readonly RunPolicyVersion _policyVersion = new(1);

    /// <summary>Initializes a new instance of the <see cref="DefaultAgentLoop"/> class.</summary>
    /// <param name="sessionCoordinator">Loads eligible history and commits every produced message.</param>
    /// <param name="securityProfileSelector">Captures fresh authorization for each newly identified run operation.</param>
    /// <param name="contextAssembler">Assembles the provider-ready request for each turn.</param>
    /// <param name="toolInvoker">Resolves, authorizes, and invokes every requested tool call.</param>
    /// <param name="modelCatalog">Supplies the engine-wide versioned view of configured models.</param>
    /// <param name="modelSelector">Chooses one configured model for each run.</param>
    /// <param name="llmModelResolver">Resolves the chosen descriptor to its provider adapter.</param>
    /// <param name="continuationPolicy">
    /// Decides, at every committed-turn boundary, whether the run continues, completes, or halts. Resolved from
    /// the keyed registration named by <see cref="AgentLoopDefaults.ContinuationPolicyKeyValue"/>.
    /// </param>
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
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="options"/> carries a non-positive <see cref="AgentLoopOptions.HistoryReadPageSize"/>,
    /// a negative <see cref="AgentLoopOptions.AppendConflictRetryLimit"/>, or a non-positive
    /// <see cref="AgentLoopOptions.SettlementTimeout"/> or <see cref="AgentLoopOptions.ObserverDeliveryTimeout"/>.
    /// </exception>
    public DefaultAgentLoop(
        ISessionCoordinator sessionCoordinator,
        ISecurityProfileSelector securityProfileSelector,
        IContextAssembler contextAssembler,
        IToolInvoker toolInvoker,
        IModelCatalog modelCatalog,
        IModelSelector modelSelector,
        ILlmModelResolver llmModelResolver,
        [FromKeyedServices(AgentLoopDefaults.ContinuationPolicyKeyValue)] IRunContinuationPolicy continuationPolicy,
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
        ArgumentNullException.ThrowIfNull(continuationPolicy);
        ArgumentNullException.ThrowIfNull(operationIds);
        ArgumentNullException.ThrowIfNull(turnIds);
        ArgumentNullException.ThrowIfNull(modelRequestIds);
        ArgumentNullException.ThrowIfNull(messageIds);
        ArgumentNullException.ThrowIfNull(entryIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        var loopOptions = options.Value;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(loopOptions.HistoryReadPageSize, 0, nameof(options));
        ArgumentOutOfRangeException.ThrowIfNegative(loopOptions.AppendConflictRetryLimit, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(loopOptions.SettlementTimeout, TimeSpan.Zero, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(loopOptions.ObserverDeliveryTimeout, TimeSpan.Zero, nameof(options));

        _sessionCoordinator = sessionCoordinator;
        _securityProfileSelector = securityProfileSelector;
        _contextAssembler = contextAssembler;
        _toolInvoker = toolInvoker;
        _modelCatalog = modelCatalog;
        _modelSelector = modelSelector;
        _llmModelResolver = llmModelResolver;
        _continuationPolicy = continuationPolicy;
        _operationIds = operationIds;
        _turnIds = turnIds;
        _modelRequestIds = modelRequestIds;
        _messageIds = messageIds;
        _entryIds = entryIds;
        _timeProvider = timeProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultAgentLoop>.Instance;
        _historyReadPageSize = loopOptions.HistoryReadPageSize;
        _appendConflictRetryLimit = loopOptions.AppendConflictRetryLimit;
        _settlementTimeout = loopOptions.SettlementTimeout;
        _observerDeliveryTimeout = loopOptions.ObserverDeliveryTimeout;
        _disableToolsOnFinalTurn = loopOptions.DisableToolsOnFinalTurn;
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
            var result = await RunCoreAsync(request, operationId, activity, cancellationToken).ConfigureAwait(false);
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

    /// <summary>Drives the run after its activity and start diagnostics are in place.</summary>
    /// <param name="request">The validated run request.</param>
    /// <param name="operationId">The run's causal operation identity.</param>
    /// <param name="runActivity">
    /// The loop's own run activity, or null when no listener sampled it. Model tags are set on this activity
    /// only; they are never written to whatever <see cref="Activity.Current"/> happens to be, which may be a
    /// host-owned parent when the loop's activity was not sampled.
    /// </param>
    /// <param name="cancellationToken">The caller's cancellation.</param>
    /// <returns>The complete result of the run.</returns>
    private async Task<AgentLoopResult> RunCoreAsync(
        AgentRunRequest request,
        OperationId operationId,
        Activity? runActivity,
        CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "A validated run request is required by the loop core.");
        var runCorrelation = new InRunOperationCorrelation(operationId, request.RunId, turnId: null);
        var (runAuthorization, runCaptureFailure) = await CaptureAuthorizationAsync(
            request, runCorrelation, cancellationToken)
            .ConfigureAwait(false);
        if (runAuthorization is null)
        {
            // Nothing has been observed or committed: no branch version exists to report truthfully.
            return BuildResult(request, runCaptureFailure!, [], finalVersion: null);
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
                finalVersion: null);
        }

        var currentVersion = initialCursor.Version;
        var committedMessages = ImmutableArray.CreateBuilder<AgentMessage>();

        var (history, recoveryFailure) = await SettleDanglingToolCallsAsync(
            request, sessionContext, runCorrelation, initialEntries, initialCursor, committedMessages, cancellationToken)
            .ConfigureAwait(false);
        if (recoveryFailure is not null)
        {
            return BuildResult(request, recoveryFailure, committedMessages.ToImmutable(), currentVersion);
        }

        currentVersion = history.SourceCursor.Version;

        var modelResolution = await ResolveModelAsync(request, operationId, cancellationToken)
            .ConfigureAwait(false);

        if (modelResolution.Outcome is { } selectionFailure)
        {
            return BuildResult(request, selectionFailure, committedMessages.ToImmutable(), currentVersion);
        }

        var model = modelResolution.Model!;
        var llmModel = modelResolution.Adapter!;
        _ = runActivity?.SetTag(AgentKitTagNames.RequestModel, model.ModelId.ToString());
        _ = runActivity?.SetTag(AgentKitTagNames.ProviderName, model.ProviderId.ToString());

        for (var turn = 1; turn <= request.MaxTurns; turn++)
        {
            TurnOutcome result;
            try
            {
                result = await RunTurnAsync(
                    request,
                    model,
                    llmModel,
                    operationId,
                    turn,
                    turn == 1 ? modelResolution.FirstModelRequestId : null,
                    history,
                    committedMessages,
                    currentVersion,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested && committedMessages.Count > 0)
            {
                // Cancellation may propagate as an exception only while this run has produced no durable effect.
                // Once an earlier turn committed a message, the caller must still receive it: the run settles
                // with a typed cancelled outcome carrying every committed message and the exact branch version.
                return BuildResult(
                    request,
                    new AgentRunCancelled("The run was cancelled after at least one message had been committed."),
                    committedMessages.ToImmutable(),
                    currentVersion);
            }

            if (result.Outcome is not null)
            {
                return BuildResult(request, result.Outcome, committedMessages.ToImmutable(), result.Version);
            }

            currentVersion = result.Version;
            Debug.Assert(result.Cursor is not null, "A continuing turn retains an exact updated history cursor.");
            Debug.Assert(turn < request.MaxTurns, "The final permitted turn always settles; it never continues.");
            history = new HistoryView(result.Cursor, history.Messages.AddRange(result.NewMessages), []);
        }

        // Every turn-limit exit is produced inside the final turn itself: pending tool calls are settled as
        // rejected and a continuation proposal on the final turn settles as the typed turn limit. Reaching this
        // point would mean a turn continued past the limit, which is an invariant violation rather than a limit.
        throw new UnreachableException("The final permitted turn continued instead of settling the run.");
    }

    /// <summary>
    /// Chooses this run's model and resolves it to an executable adapter.
    /// </summary>
    /// <remarks>
    /// Selection happens once per run rather than once per turn, so every
    /// turn of a run talks to the same model and the same catalog version. A
    /// mid-run catalog reload therefore cannot silently move a conversation
    /// to a different provider. The selection's <see cref="ModelSelectionRequest.ModelRequestId"/>
    /// is the identity of the first turn's attempt; later turns allocate fresh
    /// identities, so selection diagnostics always correlate to one real attempt.
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

        // The selection is correlated to a real attempt: its identity becomes the first turn's model request
        // identity rather than a throwaway value that never matches any attempt.
        var firstModelRequestId = _modelRequestIds.Create();
        var selectionRequest = new ModelSelectionRequest(
            scope,
            firstModelRequestId,
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

                return ModelResolution.Resolved(descriptor, adapter, firstModelRequestId);

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
        ModelRequestId? reservedModelRequestId,
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
            return TurnOutcome.Settled(turnCaptureFailure!, currentVersion);
        }

        var turnSessionContext = new SessionOperationContext(
            request.AgentId,
            request.SessionId,
            executionLaneId: null,
            turnCorrelation,
            request.Identity,
            turnAuthorization);
        var modelRequestId = reservedModelRequestId ?? _modelRequestIds.Create();
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

        // On the final permitted turn the loop can no longer invoke anything the model requests, so when
        // configured it asks for the final response outright: no tools and an explicit ToolChoice.None. A model
        // that requests calls anyway is still settled truthfully by SettleRejectedAtTurnLimitAsync.
        var finalTurnWithoutTools = _disableToolsOnFinalTurn && turn == request.MaxTurns;
        if (finalTurnWithoutTools)
        {
            LoopLog.FinalTurnToolsDisabled(_logger, request.RunId, turnId, request.MaxTurns);
        }

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
                finalTurnWithoutTools ? [] : agent.Tools,
                finalTurnWithoutTools ? LlmToolChoice.None : agent.ToolChoice,
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
                finalTurnWithoutTools ? [] : request.Tools,
                finalTurnWithoutTools ? LlmToolChoice.None : request.ToolChoice,
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
        // (Completed) or by requesting tools (ToolUse). Every other terminal is truthful partial output: it is
        // preserved as an Interrupted message and the run settles with the typed outcome matching the stop reason
        // rather than pretending the agent chose to finish or collapsing every cause into one unknown failure.
        if (response.StopReason is not (NormalizedStopReason.Completed or NormalizedStopReason.ToolUse))
        {
            LoopLog.ModelResponseNotAccepted(_logger, request.RunId, turnId, $"stop reason {response.StopReason}");
            return await SettleInterruptedAsync(
                request, model, sourceCursor, turnSessionContext, turnCorrelation, turnId, response.RequestId,
                response.Parts, response.Usage, response.StopReason, response.Identity.RequestId,
                OutcomeForUnacceptedStop(response),
                committedMessages, currentVersion).ConfigureAwait(false);
        }

        // A ToolUse stop that requests no tool call is a protocol violation: the provider claims the model stopped
        // to call tools, yet there is nothing to invoke. Settling it as a completed turn would present an
        // unfinished response as the agent's chosen final answer.
        var requestedCalls = response.Parts.OfType<ToolCallPart>().ToImmutableArray();
        if (response.StopReason is NormalizedStopReason.ToolUse && requestedCalls.IsEmpty)
        {
            LoopLog.ModelResponseNotAccepted(_logger, request.RunId, turnId, "tool-use stop without any tool call");
            return await SettleInterruptedAsync(
                request, model, sourceCursor, turnSessionContext, turnCorrelation, turnId, response.RequestId,
                response.Parts, response.Usage, NormalizedStopReason.Error, response.Identity.RequestId,
                new AgentRunFailed(new ProviderFailure(
                    ProviderFailureKind.ProtocolViolation, response.Identity.ProviderId, response.Identity.RequestId,
                    statusCode: null, providerCode: null, retryAfter: null,
                    "The model response reported a tool-use stop but requested no tool call.",
                    diagnosticCause: null, ExtensionData.Empty)),
                committedMessages, currentVersion).ConfigureAwait(false);
        }

        // Duplicate call identities cannot be honoured: one identity must never produce two effects, and the
        // history contract requires exactly one terminal result per call. The response is a protocol violation.
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

        // The response was generated against the history this turn saw. A concurrent message landing ahead of it
        // makes it stale, so the append fails closed rather than rebasing (allowInterleavedMessages: false).
        var appendAttempt = await AppendWithDiagnosticsAsync(
            new SessionAppendRequest(
                turnSessionContext,
                request.BranchId,
                currentVersion,
                new IdempotencyKey($"run:{request.RunId}:turn:{turnId}:assistant"),
                [assistantEntry]),
            request.SessionProfile,
            allowInterleavedMessages: false,
            cancellationToken).ConfigureAwait(false);

        if (appendAttempt.StaleAfterInterleavedMessage)
        {
            LoopLog.ModelResponseNotAccepted(_logger, request.RunId, turnId, "a concurrent message was committed before the response");
            return TurnOutcome.Settled(
                new AgentRunSessionOperationFailed(
                    "A concurrent writer committed a message to the branch while the model response was pending; " +
                    "the response is stale and was not committed."),
                currentVersion);
        }

        if (appendAttempt.Result is not SessionAppended appended)
        {
            return TurnOutcome.Settled(
                new AgentRunSessionOperationFailed(DescribeAppendFailure(appendAttempt.Result)), currentVersion);
        }

        currentVersion = appended.NewVersion;
        committedMessages.Add(assistantMessage);

        var toolCalls = requestedCalls;
        var committedSequence = appended.CommittedEntries[^1].Sequence;

        return toolCalls switch
        {
            { IsEmpty: true } => await DecideContinuationAsync(
                request, turnCorrelation, turn, assistantMessage, [], assistantEntryId,
                NextCursor(sourceCursor, currentVersion, committedSequence), [assistantMessage], currentVersion, cancellationToken)
                .ConfigureAwait(false),
            _ when turn == request.MaxTurns => await SettleRejectedAtTurnLimitAsync(
                request, sourceCursor, turnSessionContext, turnCorrelation, turnId, assistantEntryId, toolCalls,
                committedMessages, currentVersion, committedSequence)
                .ConfigureAwait(false),
            _ => await InvokeToolsAsync(
                request, sourceCursor, turnSessionContext, turnCorrelation, turnId, turn, assistantEntryId, assistantMessage, toolCalls,
                committedMessages, currentVersion, committedSequence, cancellationToken)
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
            await ObserveDetachedAsync(request, new AgentRunToolCallCompleted(turnId, rejected)).ConfigureAwait(false);
        }

        var appendAttempt = await CommitToolMessageAsync(
            request, sourceCursor, turnSessionContext, turnCorrelation, turnId, assistantEntryId,
            resultParts.ToImmutable(), currentVersion, currentSequence, committedMessages).ConfigureAwait(false);

        return appendAttempt.Result is SessionAppended appended
            ? TurnOutcome.Settled(new AgentRunTurnLimitReached(request.MaxTurns), appended.NewVersion)
            : TurnOutcome.Settled(new AgentRunSessionOperationFailed(DescribeAppendFailure(appendAttempt.Result)), currentVersion);
    }

    /// <summary>
    /// Builds and durably commits the tool message carrying one terminal result per requested call. The commit
    /// runs under the loop's own bounded settlement token rather than the caller's: the assistant message that
    /// requested these calls is already committed, so its results must land no matter how the run itself settles.
    /// For the same reason a concurrent message landing ahead of the tool message never refuses the commit; the
    /// interleaved entries are returned so the next turn's history reflects them.
    /// </summary>
    private async ValueTask<AppendAttempt> CommitToolMessageAsync(
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

        var appendAttempt = await AppendWithSettlementBoundAsync(
            request.RunId,
            new SessionAppendRequest(
                turnSessionContext,
                request.BranchId,
                currentVersion,
                new IdempotencyKey($"run:{request.RunId}:turn:{turnId}:tools"),
                [toolEntry]),
            request.SessionProfile,
            allowInterleavedMessages: true).ConfigureAwait(false);

        if (appendAttempt.Result is SessionAppended)
        {
            committedMessages.Add(toolMessage);
        }

        return appendAttempt;
    }

    private async Task<TurnOutcome> InvokeToolsAsync(
        AgentRunRequest request,
        MessageCursor sourceCursor,
        SessionOperationContext turnSessionContext,
        InRunOperationCorrelation turnCorrelation,
        TurnId turnId,
        int turn,
        SessionEntryId assistantEntryId,
        AssistantMessage assistantMessage,
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
                await ObserveDetachedAsync(request, new AgentRunToolCallCompleted(turnId, skippedResult)).ConfigureAwait(false);
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
            await ObserveDetachedAsync(request, new AgentRunToolCallCompleted(turnId, resultPart)).ConfigureAwait(false);
        }

        // The commit deliberately ignores the caller's cancellation (see CommitToolMessageAsync). A tool invoker
        // may also absorb cancellation into an ordinary ToolCallOutcomeKind.Cancelled result instead of throwing
        // (for example, a process runner that kills its child process and returns a settled "cancelled" outcome) —
        // cancellationToken.IsCancellationRequested is checked explicitly below, after this commit, so that case
        // still propagates cancellation to the caller instead of silently continuing to the next turn.
        var appendAttempt = await CommitToolMessageAsync(
            request, sourceCursor, turnSessionContext, turnCorrelation, turnId, assistantEntryId,
            resultParts.ToImmutable(), currentVersion, currentSequence, committedMessages).ConfigureAwait(false);

        if (appendAttempt.Result is not SessionAppended appended)
        {
            activity.SetFailed("session_append_failed", appendAttempt.Result.GetType().Name);
            LoopLog.ToolBatchFailed(_logger, request.RunId, turnId, appendAttempt.Result.GetType().Name);
            return TurnOutcome.Settled(
                new AgentRunSessionOperationFailed(DescribeAppendFailure(appendAttempt.Result)), currentVersion);
        }

        var toolMessage = committedMessages[^1];

        if (interrupted || cancellationToken.IsCancellationRequested)
        {
            // The tool message is now durably committed. Cancellation observed at this point settles the run
            // with a typed outcome rather than throwing: an exception here would discard a result whose
            // messages already landed, and IAgentLoop promises exactly one terminal outcome for every effect.
            activity.SetFailed("cancelled", "cancellation");
            LoopLog.ToolBatchInterrupted(_logger, request.RunId, turnId);
            return TurnOutcome.Settled(
                new AgentRunCancelled("The run was cancelled while its tool calls were being invoked; every requested call was settled with a terminal result before the run stopped."),
                appended.NewVersion);
        }

        activity.SetSuccessful("completed");
        LoopLog.ToolBatchCompleted(_logger, request.RunId, turnId, toolCalls.Length);
        // The next cursor covers everything through the committed tool message, so any message a concurrent
        // writer interleaved between the assistant request and its results must become visible to the next turn
        // in sequence order; otherwise the cursor would claim history the next request never saw.
        var toolEntry = appended.CommittedEntries[^1];
        var nextCursor = NextCursor(sourceCursor, appended.NewVersion, toolEntry.Sequence);
        var toolResultReferences = toolCalls
            .Select(call => new CommittedToolResultReference(toolEntry.Id, call.CallId, turnId))
            .ToImmutableArray();
        return await DecideContinuationAsync(
            request, turnCorrelation, turn, assistantMessage, toolResultReferences, toolEntry.Id, nextCursor,
            [assistantMessage, .. appendAttempt.InterleavedMessages, toolMessage], appended.NewVersion, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Asks the selected <see cref="IRunContinuationPolicy"/> what happens after one committed turn and maps its
    /// proposal onto the loop's own transition.
    /// </summary>
    /// <param name="request">The run being driven.</param>
    /// <param name="turnCorrelation">The committed turn's in-run correlation.</param>
    /// <param name="turn">The one-based number of the committed turn.</param>
    /// <param name="assistantMessage">The complete, committed assistant response of the turn.</param>
    /// <param name="toolResults">One reference per requested call to its committed terminal result, or empty when the turn requested no tool.</param>
    /// <param name="lastEntryId">The identity of the last entry this turn committed.</param>
    /// <param name="nextCursor">The exact history cursor after this turn's commits.</param>
    /// <param name="newMessages">The messages newly visible to the next turn, in sequence order.</param>
    /// <param name="version">The branch version after this turn's commits.</param>
    /// <param name="cancellationToken">Cancels the policy's evaluation.</param>
    /// <returns>A continuation to the next turn, or the settled outcome proposed by the policy.</returns>
    /// <remarks>
    /// <para>
    /// <see cref="ContinueRun"/> continues while a turn remains; on the final turn it settles with
    /// <see cref="AgentRunTurnLimitReached"/> because the policy cannot widen the hard limit.
    /// <see cref="CompleteRun"/> and <see cref="HaltRun"/> settle with the proposed outcome. Cancellation while the
    /// policy evaluates settles with <see cref="AgentRunCancelled"/>: the turn's messages are already committed,
    /// so the caller must receive them. A context the abstractions reject fails closed as
    /// <see cref="AgentRunInvalidState"/>.
    /// </para>
    /// <para>
    /// This reduced loop drives one implicit execution lane per branch, so the lane identity is the branch
    /// identity; its operation-state revision is the turn number, which advances with every committed turn; and
    /// its policy version is <see cref="_policyVersion"/>, the single immutable snapshot of the loop's fixed
    /// behaviour. The reduced loop projects every result of a batch into one tool message entry, so a batch of
    /// more than one call cannot supply the distinct per-call terminal-record identities the
    /// <see cref="CommittedTurnContinuationBoundary"/> contract requires. Such a batch continues under the
    /// canonical committed-tool-results rule without a policy call, and the bypass is logged.
    /// </para>
    /// </remarks>
    private async ValueTask<TurnOutcome> DecideContinuationAsync(
        AgentRunRequest request,
        InRunOperationCorrelation turnCorrelation,
        int turn,
        AssistantMessage assistantMessage,
        ImmutableArray<CommittedToolResultReference> toolResults,
        SessionEntryId lastEntryId,
        MessageCursor nextCursor,
        ImmutableArray<AgentMessage> newMessages,
        SessionVersion version,
        CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "A validated run request is required to decide continuation.");
        Debug.Assert(turnCorrelation.TurnId is not null, "Continuation is decided for one committed turn.");
        Debug.Assert(assistantMessage.State == MessageState.Complete, "Only a committed complete response reaches continuation.");
        var turnId = turnCorrelation.TurnId.Value;

        if (toolResults.Length > 1)
        {
            LoopLog.ContinuationPolicyBypassedForBatchProjection(_logger, request.RunId, turnId, toolResults.Length);
            return turn < request.MaxTurns
                ? TurnOutcome.Continue(nextCursor, newMessages)
                : TurnOutcome.Settled(new AgentRunTurnLimitReached(request.MaxTurns), version);
        }

        RunContinuationContext context;
        try
        {
            context = new RunContinuationContext(
                request.AgentId,
                request.SessionId,
                new ExecutionLaneId(request.BranchId.Value),
                turnCorrelation.OperationId,
                request.RunId,
                AgentRunState.Driving,
                new OperationStateRevision(turn),
                new SessionBranchCursor(request.BranchId, lastEntryId),
                nextCursor.Sequence,
                request.Authorization.ConfigurationVersion,
                _policyVersion,
                new CommittedTurnContinuationBoundary(assistantMessage, toolResults, outputDecision: null, requiresOutputValidation: false),
                requiredStopOutcome: null,
                toolResults.IsEmpty ? [] : [new CommittedToolResultsContinuationCause(toolResults)]);
        }
        catch (ArgumentException exception)
        {
            LoopLog.ContinuationFailed(_logger, request.RunId, exception.GetType().FullName ?? exception.GetType().Name);
            return TurnOutcome.Settled(
                new AgentRunInvalidState("The committed turn could not be described as continuation evidence."), version);
        }

        RunContinuationDecision decision;
        try
        {
            decision = await _continuationPolicy.DecideAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LoopLog.ContinuationCancelled(_logger, request.RunId);
            return TurnOutcome.Settled(
                new AgentRunCancelled("The run was cancelled while its continuation was being decided; the turn's messages are committed."),
                version);
        }

        LoopLog.ContinuationDecisionApplied(_logger, request.RunId, turnId, decision.GetType().Name);
        return decision switch
        {
            ContinueRun when turn < request.MaxTurns => TurnOutcome.Continue(nextCursor, newMessages),
            ContinueRun => TurnOutcome.Settled(new AgentRunTurnLimitReached(request.MaxTurns), version),
            CompleteRun complete => TurnOutcome.Settled(complete.Outcome, version),
            HaltRun halt => TurnOutcome.Settled(halt.Outcome, version),
            _ => throw new InvalidOperationException(
                $"Unrecognized {nameof(RunContinuationDecision)} kind '{decision.GetType()}'."),
        };
    }

    /// <summary>Builds the exact history cursor after a commit on the same branch as <paramref name="sourceCursor"/>.</summary>
    /// <param name="sourceCursor">The cursor the turn started from.</param>
    /// <param name="version">The branch version after the commit.</param>
    /// <param name="sequence">The sequence of the last committed entry.</param>
    /// <returns>A cursor covering the branch through <paramref name="sequence"/>.</returns>
    private static MessageCursor NextCursor(MessageCursor sourceCursor, SessionVersion version, SessionSequence sequence)
    {
        Debug.Assert(sourceCursor is not null, "A turn always starts from an exact history cursor.");
        return new MessageCursor(
            sourceCursor.AgentId,
            sourceCursor.SessionId,
            sourceCursor.ConversationId,
            sourceCursor.BranchId,
            version,
            sequence);
    }

    /// <summary>
    /// Maps a completed attempt whose terminal stop reason is neither <see cref="NormalizedStopReason.Completed"/>
    /// nor <see cref="NormalizedStopReason.ToolUse"/> to the typed run outcome that truthfully names its cause.
    /// </summary>
    /// <param name="response">The completed response carrying the unaccepted stop reason.</param>
    /// <returns>
    /// <see cref="AgentRunCancelled"/> for <see cref="NormalizedStopReason.Cancelled"/>;
    /// <see cref="AgentRunOutputLengthLimitReached"/> for <see cref="NormalizedStopReason.Length"/>;
    /// <see cref="AgentRunInvalidState"/> for <see cref="NormalizedStopReason.Deferred"/>, which this loop has no
    /// deferred-operation handoff to honour; and <see cref="AgentRunFailed"/> with
    /// <see cref="ProviderFailureKind.ProtocolViolation"/> for <see cref="NormalizedStopReason.Pending"/>,
    /// <see cref="NormalizedStopReason.Error"/>, or an undefined value, none of which a completed attempt may report.
    /// </returns>
    private static AgentRunOutcome OutcomeForUnacceptedStop(ModelResponse response)
    {
        Debug.Assert(response is not null, "A completed attempt always carries a response.");
        Debug.Assert(
            response.StopReason is not (NormalizedStopReason.Completed or NormalizedStopReason.ToolUse),
            "Accepted stop reasons never reach the unaccepted-stop mapping.");

        return response.StopReason switch
        {
            NormalizedStopReason.Cancelled => new AgentRunCancelled(
                "The model reported that its response was cancelled before it completed."),
            NormalizedStopReason.Length => new AgentRunOutputLengthLimitReached(
                response.RequestId,
                hasPartialOutput: !response.Parts.IsEmpty,
                "The model reached its output length limit before it finished its response."),
            NormalizedStopReason.Deferred => new AgentRunInvalidState(
                "The model reported a deferred response, but this loop has no deferred-operation handoff to resume it."),
            NormalizedStopReason.Pending or NormalizedStopReason.Error => ProtocolViolation(
                response, $"The provider reported a completed attempt with a non-terminal stop reason ({response.StopReason})."),
            NormalizedStopReason.Completed or NormalizedStopReason.ToolUse => throw new InvalidOperationException(
                "Accepted stop reasons never reach the unaccepted-stop mapping."),
            _ => ProtocolViolation(
                response, $"The provider reported a completed attempt with an undefined stop reason ({response.StopReason})."),
        };

        static AgentRunFailed ProtocolViolation(ModelResponse response, string safeMessage) => new(new ProviderFailure(
            ProviderFailureKind.ProtocolViolation, response.Identity.ProviderId, response.Identity.RequestId,
            statusCode: null, providerCode: null, retryAfter: null, safeMessage, diagnosticCause: null, ExtensionData.Empty));
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

    /// <summary>
    /// Performs a required terminal commit under the loop's own bounded settlement token, independent of the
    /// caller's cancellation, so evidence of how the run stopped can land without the loop ever hanging on it.
    /// </summary>
    /// <param name="runId">The run whose settlement is bounded, for diagnostics.</param>
    /// <param name="request">The append to commit.</param>
    /// <param name="sessionProfile">The run's immutable session profile.</param>
    /// <param name="allowInterleavedMessages">Whether the append may still commit after a concurrent message landed ahead of it.</param>
    /// <returns>
    /// The append attempt; when <see cref="AgentLoopOptions.SettlementTimeout"/> elapses first, a
    /// <see cref="SessionAppendFailed"/> whose message states that the commit outcome is unknown.
    /// </returns>
    private async ValueTask<AppendAttempt> AppendWithSettlementBoundAsync(
        RunId runId,
        SessionAppendRequest request,
        SessionProfileSnapshot sessionProfile,
        bool allowInterleavedMessages)
    {
        Debug.Assert(request is not null, "A validated append request is required for a bounded settlement commit.");
        using var settlement = new CancellationTokenSource(_settlementTimeout, _timeProvider);
        var delay = _settlementRetryBaseDelay;
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                var appendAttempt = await AppendWithDiagnosticsAsync(request, sessionProfile, allowInterleavedMessages, settlement.Token)
                    .ConfigureAwait(false);
                if (appendAttempt.Result is not SessionAppendFailed)
                {
                    return appendAttempt;
                }

                // The store reported the commit as failed. The request keeps its idempotency key, so a retry cannot
                // duplicate an append that did land; the settlement token bounds how long the loop keeps trying.
                LoopLog.SettlementCommitRetryScheduled(_logger, runId, request.Context.SessionId, attempt, delay);
                await Task.Delay(delay, _timeProvider, settlement.Token).ConfigureAwait(false);
                delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, _settlementRetryMaxDelay.Ticks));
            }
        }
        catch (OperationCanceledException) when (settlement.IsCancellationRequested)
        {
            LoopLog.SettlementTimedOut(_logger, runId, request.Context.SessionId, _settlementTimeout);
            return new AppendAttempt(
                new SessionAppendFailed(
                    "The required terminal commit did not complete within the configured settlement timeout; " +
                    "whether it landed is unknown."),
                [],
                staleAfterInterleavedMessage: false);
        }
    }

    /// <summary>
    /// Delivers a run event that must not use the caller's (possibly cancelled) token, bounded by
    /// <see cref="AgentLoopOptions.ObserverDeliveryTimeout"/> so a stalled observer cannot hold up settlement.
    /// </summary>
    /// <param name="request">The run whose observer receives the event.</param>
    /// <param name="runEvent">The immutable event to deliver.</param>
    /// <returns>An operation completing after delivery succeeds, times out, or is safely dropped.</returns>
    private async ValueTask ObserveDetachedAsync(AgentRunRequest request, AgentRunEvent runEvent)
    {
        Debug.Assert(request is not null, "A validated run request is required for observer delivery.");
        if (request.Observer is null)
        {
            return;
        }

        using var delivery = new CancellationTokenSource(_observerDeliveryTimeout, _timeProvider);
        await ObserveAsync(request, runEvent, delivery.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Appends with commit diagnostics, rebasing onto the actual branch tip after a concurrent writer advanced it.
    /// </summary>
    /// <param name="request">The append built against the loop's last-observed tip.</param>
    /// <param name="sessionProfile">The run's immutable session profile.</param>
    /// <param name="allowInterleavedMessages">
    /// Whether the append may still commit after a concurrent <see cref="MessageSessionEntry"/> landed ahead of it.
    /// An assistant response is generated against the history it saw, so an interleaved message makes it stale and
    /// the append fails closed. Tool results and interrupted partial output settle calls or evidence already
    /// committed, so they land regardless and the interleaved messages are returned for the next turn's history.
    /// </param>
    /// <param name="cancellationToken">Cancels the append and any rebase read.</param>
    /// <returns>
    /// The terminal append result together with every concurrently committed entry that now precedes the
    /// appended entries, in sequence order, and whether the append was refused because of an interleaved message.
    /// </returns>
    /// <remarks>
    /// Every rebase re-reads the interleaved range under one pinned <see cref="SessionReadSnapshot"/> rather than
    /// deriving the tip from the conflict's version: version and sequence advance independently (one version per
    /// append, one sequence per entry), and each entry's own <see cref="SessionEntry.Sequence"/> was assigned from
    /// the stale tip when it was built.
    /// </remarks>
    private async ValueTask<AppendAttempt> AppendWithDiagnosticsAsync(
        SessionAppendRequest request,
        SessionProfileSnapshot sessionProfile,
        bool allowInterleavedMessages,
        CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "A validated append request is required for session diagnostics.");
        Debug.Assert(!request.Entries.IsEmpty, "The loop never appends an empty entry batch.");
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
        var interleaved = ImmutableArray.CreateBuilder<SessionEntry>();
        var staleResponse = false;
        SessionAppendResult result;
        for (var attempt = 1; ; attempt++)
        {
            result = await _sessionCoordinator.AppendAsync(
                attemptRequest, sessionProfile, cancellationToken).ConfigureAwait(false);
            if (result is not SessionAppendConflict conflict || attempt > _appendConflictRetryLimit)
            {
                break;
            }

            LoopLog.SessionAppendConflictRetried(
                _logger, request.Context.SessionId, conflict.ExpectedVersion, conflict.ActualVersion, attempt);

            var interleavedRead = await ReadInterleavedEntriesAsync(
                attemptRequest, sessionProfile, cancellationToken).ConfigureAwait(false);
            if (interleavedRead is not var (interleavedEntries, tip))
            {
                break;
            }

            interleaved.AddRange(interleavedEntries);
            if (!allowInterleavedMessages && interleavedEntries.Any(static entry => entry is MessageSessionEntry))
            {
                LoopLog.SessionAppendStaleAfterInterleavedMessage(
                    _logger, request.Context.SessionId, conflict.ExpectedVersion, tip.Version);
                staleResponse = true;
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
            activity.SetFailed(staleResponse ? "stale_after_interleaved_message" : "failed", result.GetType().Name);
            LoopLog.SessionCommitFailed(_logger, request.Context.SessionId, result.GetType().Name);
        }

        return new AppendAttempt(result, interleaved.ToImmutable(), staleResponse);
    }

    /// <summary>
    /// Reads, under one pinned snapshot, every entry a concurrent writer committed at or after the sequence the
    /// conflicting append had claimed, through the actual branch tip.
    /// </summary>
    /// <param name="attemptRequest">The conflicting append whose first entry names the sequence the loop believed was free.</param>
    /// <param name="sessionProfile">The run's immutable session profile.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The interleaved entries in sequence order with the pinned tip snapshot, or null when the range could not be read consistently.</returns>
    private async ValueTask<(ImmutableArray<SessionEntry> Entries, SessionReadSnapshot Tip)?> ReadInterleavedEntriesAsync(
        SessionAppendRequest attemptRequest,
        SessionProfileSnapshot sessionProfile,
        CancellationToken cancellationToken)
    {
        Debug.Assert(attemptRequest is not null, "A conflicting append request is required to read the interleaved range.");
        Debug.Assert(!attemptRequest.Entries.IsEmpty, "The loop never appends an empty entry batch.");

        // FromSequenceExclusive is exclusive, so reading from (claimed - 1) returns the entry now occupying the
        // claimed sequence and everything after it up to the pinned tip.
        var cursor = new SessionSequence(attemptRequest.Entries[0].Sequence.Value - 1);
        var entries = ImmutableArray.CreateBuilder<SessionEntry>();
        SessionReadSnapshot? snapshot = null;
        while (true)
        {
            var pageResult = await _sessionCoordinator.ReadAsync(
                snapshot is null
                    ? new SessionReadRequest(attemptRequest.Context, attemptRequest.BranchId, cursor, _historyReadPageSize)
                    : new SessionReadRequest(attemptRequest.Context, attemptRequest.BranchId, cursor, _historyReadPageSize, snapshot),
                sessionProfile,
                cancellationToken).ConfigureAwait(false);
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
        return (entries.ToImmutable(), snapshot);
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

        // Partial output is committed under the loop's bounded settlement token rather than the caller's: the
        // caller's token is usually the very reason the attempt was interrupted, and a cancelled token would
        // otherwise discard output the class promises to keep. Interrupted output is audit evidence rather than a
        // live reply, so a concurrently interleaved message never refuses it (allowInterleavedMessages: true).
        var appendAttempt = await AppendWithSettlementBoundAsync(
            request.RunId,
            new SessionAppendRequest(
                turnSessionContext,
                request.BranchId,
                currentVersion,
                new IdempotencyKey($"run:{request.RunId}:turn:{turnId}:interrupted"),
                [entry]),
            request.SessionProfile,
            allowInterleavedMessages: true).ConfigureAwait(false);

        if (appendAttempt.Result is not SessionAppended appended)
        {
            return TurnOutcome.Settled(
                new AgentRunSessionOperationFailed(DescribeAppendFailure(appendAttempt.Result)), currentVersion);
        }

        committedMessages.Add(interruptedMessage);
        return TurnOutcome.Settled(outcome, appended.NewVersion);
    }

    /// <summary>
    /// Settles, before the first turn, every tool call a previous run left without a terminal result, so the
    /// branch becomes causally valid again instead of rejecting every later run.
    /// </summary>
    /// <param name="request">The run performing the recovery.</param>
    /// <param name="sessionContext">The run-scoped session context that writes the settlement.</param>
    /// <param name="runCorrelation">The run's in-run correlation recorded as the entry's writer.</param>
    /// <param name="entries">The loaded eligible history entries in sequence order.</param>
    /// <param name="cursor">The exact cursor of the loaded history.</param>
    /// <param name="committedMessages">Receives the synthesized tool message when one is committed.</param>
    /// <param name="cancellationToken">The caller's cancellation; the recovery is pre-turn work of this run.</param>
    /// <returns>
    /// The history view the first turn starts from (unchanged when nothing was dangling), or the typed failure
    /// that settles the run when the settlement could not be committed.
    /// </returns>
    /// <remarks>
    /// A previous run whose tool-message commit failed or crashed leaves a complete assistant message whose
    /// <see cref="ToolCallPart"/>s have no <see cref="ToolResultPart"/>; the context assembler then rejects every
    /// later request as broken causality. This loop is the recovery owner for that state: it appends one tool
    /// message carrying an interrupted terminal result (<see cref="SideEffectCertainty.Unknown"/>, not retryable)
    /// per dangling call. The append's idempotency key derives from the dangling assistant message identity, so
    /// concurrent or repeated recoveries settle each call exactly once, and its causal parent is that message's
    /// entry. The settlement never invokes a tool.
    /// </remarks>
    private async ValueTask<(HistoryView History, AgentRunOutcome? Failure)> SettleDanglingToolCallsAsync(
        AgentRunRequest request,
        SessionOperationContext sessionContext,
        InRunOperationCorrelation runCorrelation,
        ImmutableArray<SessionEntry> entries,
        MessageCursor cursor,
        ImmutableArray<AgentMessage>.Builder committedMessages,
        CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "A validated run request is required for run-start recovery.");
        Debug.Assert(!entries.IsDefault, "Loaded history is an initialized array.");
        var messages = ToMessages(entries);
        var pendingCalls = new Dictionary<ToolCallId, ToolCallPart>();
        MessageSessionEntry? danglingEntry = null;
        foreach (var entry in entries)
        {
            if (entry is not MessageSessionEntry { Message.State: MessageState.Complete } messageEntry)
            {
                continue;
            }

            foreach (var part in messageEntry.Message.Parts)
            {
                switch (part)
                {
                    case ToolCallPart call when messageEntry.Message is AssistantMessage:
                        _ = pendingCalls.TryAdd(call.CallId, call);
                        danglingEntry = messageEntry;
                        break;
                    case ToolResultPart result when messageEntry.Message is ToolMessage:
                        _ = pendingCalls.Remove(result.CallId);
                        break;
                    default:
                        break;
                }
            }
        }

        if (pendingCalls.Count == 0 || danglingEntry is null)
        {
            return (new HistoryView(cursor, messages, []), null);
        }

        LoopLog.DanglingToolCallsSettled(_logger, request.RunId, pendingCalls.Count);
        var now = _timeProvider.GetUtcNow();
        var resultParts = pendingCalls.Values
            .Select(static call => (ContentPart) new ToolResultPart(
                call.CallId,
                call.Tool,
                new ToolCallOutcome(
                    ToolCallOutcomeKind.Cancelled,
                    ToolTerminalStatus.Interrupted,
                    SideEffectCertainty.Unknown,
                    retryable: false,
                    "The run that requested this tool call ended before its result was committed; whether the tool ran is unknown.",
                    ExtensionData.Empty),
                [],
                ExtensionData.Empty))
            .ToImmutableArray();
        var toolMessage = new ToolMessage(
            _messageIds.Create(),
            request.AgentId,
            request.SessionId,
            cursor.ConversationId,
            request.BranchId,
            request.RunId,
            turnId: null,
            now,
            MessageState.Complete,
            resultParts,
            ExtensionData.Empty);
        var entryToAppend = new MessageSessionEntry(
            _entryIds.Create(),
            sessionContext.ToAddress(),
            runCorrelation,
            request.BranchId,
            new SessionSequence(cursor.Sequence.Value + 1),
            danglingEntry.Id,
            now,
            new SchemaVersion("1"),
            toolMessage);

        var appendAttempt = await AppendWithDiagnosticsAsync(
            new SessionAppendRequest(
                sessionContext,
                request.BranchId,
                cursor.Version,
                new IdempotencyKey($"recovery:dangling-tools:{danglingEntry.Message.Id}"),
                [entryToAppend]),
            request.SessionProfile,
            allowInterleavedMessages: true,
            cancellationToken).ConfigureAwait(false);

        if (appendAttempt.Result is not SessionAppended appended)
        {
            return (new HistoryView(cursor, messages, []), new AgentRunSessionOperationFailed(
                "A previous run left tool calls without terminal results and the recovery settlement could not be committed: " +
                DescribeAppendFailure(appendAttempt.Result)));
        }

        committedMessages.Add(toolMessage);
        var nextCursor = NextCursor(cursor, appended.NewVersion, appended.CommittedEntries[^1].Sequence);
        return (new HistoryView(nextCursor, [.. messages, .. appendAttempt.InterleavedMessages, toolMessage], []), null);
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

    /// <summary>
    /// Captures fresh authorization for one run operation and verifies it against the run-start evidence.
    /// </summary>
    /// <param name="request">The run whose baseline evidence the capture must match.</param>
    /// <param name="correlation">The operation (run or turn) the authorization is captured for.</param>
    /// <param name="cancellationToken">Cancels the capture.</param>
    /// <returns>
    /// The captured authorization, or the typed outcome that settles the run:
    /// <see cref="AgentRunAuthorizationUnavailable"/> when the authority could not capture authorization, and
    /// <see cref="AgentRunInvalidState"/> when the captured evidence contradicts the run-start evidence.
    /// </returns>
    private async ValueTask<(SecurityAuthorizationContext? Authorization, AgentRunOutcome? Failure)>
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
            LoopLog.AuthorizationCaptureUnavailable(_logger, request.RunId, correlation.TurnId);
            return (null, new AgentRunAuthorizationUnavailable(result is SecurityAuthorizationCaptureUnavailable unavailable
                ? unavailable.SafeReason
                : "The security profile could not be captured for this operation."));
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
            LoopLog.AuthorizationEvidenceMismatch(_logger, request.RunId, correlation.TurnId);
            return (null, new AgentRunInvalidState("The captured security profile differs from the run-start evidence."));
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
        AgentRunRequest request, AgentRunOutcome outcome, ImmutableArray<AgentMessage> newMessages, SessionVersion? finalVersion) =>
        new(request.AgentId, request.SessionId, request.BranchId, request.RunId, outcome, newMessages, finalVersion);

    /// <summary>
    /// The result of choosing this run's model: either a terminal outcome
    /// that settles the run before any turn starts, or the chosen descriptor
    /// paired with the adapter that executes it.
    /// </summary>
    private readonly struct ModelResolution
    {
        private ModelResolution(AgentRunOutcome? outcome, ModelDescriptor? model, ILlmModel? adapter, ModelRequestId? firstModelRequestId)
        {
            Outcome = outcome;
            Model = model;
            Adapter = adapter;
            FirstModelRequestId = firstModelRequestId;
        }

        /// <summary>Gets the terminal outcome, when no model could be used.</summary>
        public AgentRunOutcome? Outcome { get; }

        /// <summary>Gets the chosen descriptor, when resolution succeeded.</summary>
        public ModelDescriptor? Model { get; }

        /// <summary>Gets the adapter that executes the chosen model, when resolution succeeded.</summary>
        public ILlmModel? Adapter { get; }

        /// <summary>Gets the selection's model request identity, which the first turn's attempt reuses, when resolution succeeded.</summary>
        public ModelRequestId? FirstModelRequestId { get; }

        /// <summary>Creates a successful resolution.</summary>
        public static ModelResolution Resolved(ModelDescriptor model, ILlmModel adapter, ModelRequestId firstModelRequestId) =>
            new(null, model, adapter, firstModelRequestId);

        /// <summary>Creates a resolution that settles the run before it starts.</summary>
        public static ModelResolution Failed(AgentRunOutcome outcome) => new(outcome, null, null, null);
    }

    /// <summary>
    /// The result of one guarded append: the coordinator's terminal result, every entry a concurrent writer
    /// committed ahead of the appended entries during rebase, and whether the append was refused as stale.
    /// </summary>
    private readonly struct AppendAttempt
    {
        /// <summary>Initializes one append attempt result.</summary>
        /// <param name="result">The coordinator's terminal append result.</param>
        /// <param name="interleaved">Every concurrently committed entry that now precedes the appended entries, in sequence order.</param>
        /// <param name="staleAfterInterleavedMessage">Whether the append was refused because a concurrent message made its content stale.</param>
        public AppendAttempt(SessionAppendResult result, ImmutableArray<SessionEntry> interleaved, bool staleAfterInterleavedMessage)
        {
            Debug.Assert(result is not null, "An append attempt always records a terminal coordinator result.");
            Debug.Assert(!interleaved.IsDefault, "Interleaved entries are an initialized, possibly empty, array.");
            Result = result;
            Interleaved = interleaved;
            StaleAfterInterleavedMessage = staleAfterInterleavedMessage;
        }

        /// <summary>Gets the coordinator's terminal append result.</summary>
        public SessionAppendResult Result { get; }

        /// <summary>Gets every concurrently committed entry that now precedes the appended entries, in sequence order.</summary>
        public ImmutableArray<SessionEntry> Interleaved { get; }

        /// <summary>Gets whether the append was refused because a concurrent message made the pending content stale.</summary>
        public bool StaleAfterInterleavedMessage { get; }

        /// <summary>Gets the messages among <see cref="Interleaved"/>, in sequence order.</summary>
        public ImmutableArray<AgentMessage> InterleavedMessages => ToMessages(Interleaved);
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
