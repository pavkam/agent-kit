// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

using System.Buffers.Binary;
using System.Security.Cryptography;

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
/// naming the cause: <see cref="RunFailed"/> or <see cref="RunCancelled"/>, both preserving the normalized cause
/// through <see cref="RunFailure"/>/<see cref="CancellationReason"/>, or <see cref="RunFailed"/> for a deferral
/// this loop cannot resume.
/// </para>
/// <para>
/// History is loaded under one pinned snapshot and reconstructed from the
/// newest active <see cref="CompactionSessionEntry"/> on the branch when one
/// exists: the model-facing view is that checkpoint's summary, projected as a
/// <see cref="RuntimeMessage"/> in the shape documented by
/// <see cref="CompactionCheckpointProjection"/>, followed by exactly the
/// entries from the checkpoint's retained suffix onward. Covered entries are
/// neither replayed nor scanned for dangling tool calls. The history cursor
/// still names the real branch tip, so every append remains guarded by the
/// actual branch version. The projection is in-memory only and is never
/// appended to the session.
/// </para>
/// <para>
/// Caller cancellation propagates as <see cref="OperationCanceledException"/>
/// only while this run has committed nothing. After the first durable commit,
/// cancellation observed anywhere (a cancelled model attempt, a cancelled or
/// skipped tool call, or a cancelled wait between turns) settles the run with a
/// typed <see cref="RunCancelled"/> outcome. The result then carries every
/// committed message and the exact branch version, and every requested tool
/// call in an interrupted batch has already received its terminal result.
/// </para>
/// </remarks>
public sealed class DefaultAgentLoop: IAgentLoop
{
    private readonly IHookDispatcher? _hookDispatcher;
    private readonly IHookCatalog? _hookCatalog;
    private readonly IHookInstanceFactory? _hookInstanceFactory;
    private readonly IIdentifierGenerator<HookDispatchId> _hookDispatchIds;
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
    private readonly double _contextPressureThreshold;
    private readonly double _estimatedCharactersPerToken;
    private readonly int _maximumPromotionsPerBoundary;
    private readonly IIdentifierGenerator<CompactionId> _compactionIds;
    private readonly IIdentifierGenerator<UsageEntryId> _usageEntryIds;

    /// <summary>
    /// The first backoff before a required terminal commit that the store reported as failed is retried under its
    /// unchanged idempotency key. It doubles per retry up to <see cref="_settlementRetryMaxDelay"/>; the overall
    /// bound is <see cref="AgentLoopOptions.SettlementTimeout"/>.
    /// </summary>
    private static readonly TimeSpan _settlementRetryBaseDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>The longest single backoff between settlement retries.</summary>
    private static readonly TimeSpan _settlementRetryMaxDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The interim per-dispatch hook deadline window until <c>AgentHookOptions.DefaultHookTimeout</c> lands and the
    /// kernel enforces it directly (WS2-C12); <see cref="HookDispatchMetadata"/> requires a deadline after its
    /// timestamp, so the loop supplies this generous bound rather than fabricating an unenforced one.
    /// </summary>
    private static readonly TimeSpan _hookDispatchTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Initializes a new instance of the <see cref="DefaultAgentLoop"/> class.</summary>
    /// <param name="operationIds">Generates the run's causal operation identity.</param>
    /// <param name="turnIds">Generates each turn's identity.</param>
    /// <param name="modelRequestIds">Generates each model request's identity.</param>
    /// <param name="messageIds">Generates each committed message's identity.</param>
    /// <param name="entryIds">Generates each appended session entry's identity.</param>
    /// <param name="timeProvider">The clock used to timestamp committed messages and attempt deadlines.</param>
    /// <param name="optionsMonitor">
    /// The named options source. This instance's options are the value named by <paramref name="loopKey"/>, so
    /// a host that registered several keyed loops through <c>AddAgentLoop</c> with different
    /// <c>configure</c> callbacks gets independently bounded behavior per key.
    /// </param>
    /// <param name="loopKey">
    /// The exact key text this instance was resolved under, supplied automatically by dependency injection for
    /// a keyed service constructor parameter attributed <c>[ServiceKey]</c>. It selects this instance's named
    /// <see cref="AgentLoopOptions"/> from <paramref name="optionsMonitor"/> and is otherwise never used: every
    /// per-agent collaborator this instance drives a run with arrives through <see cref="RunAsync"/>'s
    /// <see cref="AgentRunServices"/> parameter instead, so this constructor stays genuinely key-independent
    /// beyond selecting its own bounded options.
    /// </param>
    /// <param name="logger">
    /// The optional logger that receives safe run-lifecycle diagnostics; a
    /// Microsoft null logger is used when omitted.
    /// </param>
    /// <param name="hookDispatcher">
    /// The typed dispatch kernel the first-party hook points run through, or <see langword="null"/> when the
    /// composition registers no hooks.
    /// </param>
    /// <param name="hookCatalog">
    /// Captures the hook catalog for each run, or <see langword="null"/> when hooks are not composed.
    /// </param>
    /// <param name="hookInstanceFactory">
    /// Creates the per-run activation lease, or <see langword="null"/> when hooks are not composed.
    /// </param>
    /// <param name="hookDispatchIds">The generator for hook dispatch identities, or <see langword="null"/> for a GUID generator.</param>
    /// <param name="compactionIds">The generator for compaction identities the pressure trigger allocates, or <see langword="null"/> for a GUID generator.</param>
    /// <param name="usageEntryIds">The generator for usage-accounting entry identities, or <see langword="null"/> for a GUID generator.</param>
    /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The named <see cref="AgentLoopOptions"/> selected by <paramref name="loopKey"/> carries a non-positive
    /// <see cref="AgentLoopOptions.HistoryReadPageSize"/>, a negative
    /// <see cref="AgentLoopOptions.AppendConflictRetryLimit"/>, or a non-positive
    /// <see cref="AgentLoopOptions.SettlementTimeout"/> or <see cref="AgentLoopOptions.ObserverDeliveryTimeout"/>, a
    /// <see cref="AgentLoopOptions.ContextPressureThreshold"/> outside (0, 1], or a non-positive
    /// <see cref="AgentLoopOptions.EstimatedCharactersPerToken"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="hookDispatcher"/>, <paramref name="hookCatalog"/>, and <paramref name="hookInstanceFactory"/>
    /// are not all composed together or all omitted.
    /// </exception>
    public DefaultAgentLoop(
        IIdentifierGenerator<OperationId> operationIds,
        IIdentifierGenerator<TurnId> turnIds,
        IIdentifierGenerator<ModelRequestId> modelRequestIds,
        IIdentifierGenerator<MessageId> messageIds,
        IIdentifierGenerator<SessionEntryId> entryIds,
        TimeProvider timeProvider,
        IOptionsMonitor<AgentLoopOptions> optionsMonitor,
        [ServiceKey] string loopKey,
        ILogger<DefaultAgentLoop>? logger = null,
        IHookDispatcher? hookDispatcher = null,
        IHookCatalog? hookCatalog = null,
        IHookInstanceFactory? hookInstanceFactory = null,
        IIdentifierGenerator<HookDispatchId>? hookDispatchIds = null,
        IIdentifierGenerator<CompactionId>? compactionIds = null,
        IIdentifierGenerator<UsageEntryId>? usageEntryIds = null)
    {
        ArgumentNullException.ThrowIfNull(operationIds);
        ArgumentNullException.ThrowIfNull(turnIds);
        ArgumentNullException.ThrowIfNull(modelRequestIds);
        ArgumentNullException.ThrowIfNull(messageIds);
        ArgumentNullException.ThrowIfNull(entryIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(optionsMonitor);
        ArgumentNullException.ThrowIfNull(loopKey);
        var loopOptions = optionsMonitor.Get(loopKey);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(loopOptions.HistoryReadPageSize, 0, nameof(optionsMonitor));
        ArgumentOutOfRangeException.ThrowIfNegative(loopOptions.AppendConflictRetryLimit, nameof(optionsMonitor));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(loopOptions.SettlementTimeout, TimeSpan.Zero, nameof(optionsMonitor));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(loopOptions.ObserverDeliveryTimeout, TimeSpan.Zero, nameof(optionsMonitor));

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
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(loopOptions.ContextPressureThreshold, nameof(optionsMonitor));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(loopOptions.ContextPressureThreshold, 1, nameof(optionsMonitor));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(loopOptions.EstimatedCharactersPerToken, nameof(optionsMonitor));
        _contextPressureThreshold = loopOptions.ContextPressureThreshold;
        _estimatedCharactersPerToken = loopOptions.EstimatedCharactersPerToken;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(loopOptions.MaximumPromotionsPerBoundary, nameof(optionsMonitor));
        _maximumPromotionsPerBoundary = loopOptions.MaximumPromotionsPerBoundary;
        _compactionIds = compactionIds ?? new GuidIdentifierGenerator<CompactionId>(static value => new CompactionId(value));
        _usageEntryIds = usageEntryIds ?? new GuidIdentifierGenerator<UsageEntryId>(static value => new UsageEntryId(value));
        _hookDispatcher = hookDispatcher;
        _hookCatalog = hookCatalog;
        _hookInstanceFactory = hookInstanceFactory;
        var hooksComposed = hookDispatcher is not null;
        if (hooksComposed != (hookCatalog is not null) || hooksComposed != (hookInstanceFactory is not null))
        {
            throw new InvalidOperationException(
                "Hook dispatch requires IHookDispatcher, IHookCatalog, and IHookInstanceFactory to be composed together; register AddAgentHooks or omit all three.");
        }

        _hookDispatchIds = hookDispatchIds ?? new GuidIdentifierGenerator<HookDispatchId>(static value => new HookDispatchId(value));
    }

    /// <summary>Builds the shared point, dispatch, causality, and timing facts for one hook dispatch.</summary>
    /// <param name="point">The hook point about to be dispatched.</param>
    /// <param name="correlation">The causal operation this dispatch occurs within.</param>
    /// <returns>The immutable metadata every event-argument type and the dispatcher itself observe for this dispatch.</returns>
    private HookDispatchMetadata CreateHookDispatch(HookPointId point, OperationCorrelation correlation)
    {
        var timestamp = _timeProvider.GetUtcNow();
        return new HookDispatchMetadata(point, _hookDispatchIds.Create(), correlation, timestamp, timestamp + _hookDispatchTimeout);
    }

    private bool HooksComposed => _hookDispatcher is not null;

    private async ValueTask<HookActivationScope?> OpenHookScopeAsync(CancellationToken cancellationToken)
    {
        if (!HooksComposed)
        {
            return null;
        }

        var snapshot = await _hookCatalog!
            .CaptureAsync(new HookCatalogRequest(new HookProfileKey("default")), cancellationToken)
            .ConfigureAwait(false);
        var lease = await _hookInstanceFactory!.CreateAsync(snapshot, cancellationToken).ConfigureAwait(false);
        return new HookActivationScope(snapshot, lease);
    }

    private static bool CatalogIncludesPoint(HookCatalogSnapshot catalog, HookPointId point) =>
        catalog.Registrations.Any(registration => registration.Point.Equals(point));

    /// <inheritdoc/>
    public async Task<AgentLoopResult> RunAsync(
        AgentLoopRunRequest request,
        AgentRunServices services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(services);

        // A durably admitted run's driving operation is the exact operation admission installed on the lane
        // (SessionAcceptedRunState.Correlation.OperationId): every store-facing call for this run — promotion,
        // release — must present that identity, so the loop reuses it here rather than minting an unrelated
        // second operation identity for the same accepted work.
        var operationId = request.LaneAdmission?.AcceptedCorrelation.OperationId ?? _operationIds.Create();
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

        var laneState = new LoopLaneState(
            request.LaneAdmission?.ExecutionLaneId ?? new ExecutionLaneId(request.SessionId.Value),
            request.LaneAdmission?.OperationStateRevision ?? new OperationStateRevision(1),
            request.RunId);

        try
        {
            var result = await RunCoreAsync(request, services, operationId, activity, laneState, cancellationToken).ConfigureAwait(false);
            if (request.LaneAdmission is { } admission && result.FinalVersion is { } finalVersion)
            {
                await ReleaseLaneAsync(request, services, admission, laneState, finalVersion).ConfigureAwait(false);
            }

            var outcome = result.Outcome.GetType().Name;
            if (result.Outcome is RunSucceeded)
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

    /// <summary>Releases a durably admitted run's lane once the run has settled.</summary>
    /// <param name="request">The run request, carrying the exact <see cref="LoopLaneAdmission"/> to release.</param>
    /// <param name="services">The compiled per-run collaborator bundle.</param>
    /// <param name="admission">The accepted correlation the release presents as its evidence.</param>
    /// <param name="laneState">The run's tracked lane identity and current total-state revision.</param>
    /// <param name="finalVersion">The branch version this run's settlement observed, presented as the release's expected version.</param>
    /// <remarks>
    /// This is best-effort: release failure never changes the run's already-determined outcome. A lane the run
    /// could not release stays durably accepted; nothing today reconciles it, matching the narrow scope of this
    /// increment (see <c>docs/implementation-plan.md</c>, workstream 1). Uses <see cref="CancellationToken.None"/>
    /// throughout so a caller's cancellation, already reflected in the settled outcome, cannot also prevent this
    /// cleanup. The presented revision is <paramref name="laneState"/>'s current value rather than the value
    /// admission first installed, so a run that advanced the lane's state mid-run (for example by promoting
    /// admitted input) releases against the revision it actually last observed.
    /// </remarks>
    private async Task ReleaseLaneAsync(
        AgentLoopRunRequest request, AgentRunServices services, LoopLaneAdmission admission, LoopLaneState laneState, SessionVersion finalVersion)
    {
        Debug.Assert(request is not null, "A validated run request is required to release its lane.");
        Debug.Assert(admission is not null, "Lane-release evidence is required.");
        Debug.Assert(laneState is not null, "The run's tracked lane state is required to release it.");
        if (services.RunCoordinator is not { } runCoordinator)
        {
            return;
        }

        try
        {
            var (authorization, failure) = await CaptureAuthorizationAsync(
                request, services, admission.AcceptedCorrelation, CancellationToken.None).ConfigureAwait(false);
            if (authorization is null)
            {
                Debug.Assert(failure is not null, "A failed capture always names a typed failure.");
                LoopLog.LaneReleaseAuthorizationUnavailable(_logger, request.RunId);
                return;
            }

            var releaseContext = new SessionOperationContext(
                request.AgentId, request.SessionId, laneState.ExecutionLaneId, admission.AcceptedCorrelation,
                request.Identity, authorization);
            var release = new SessionRunReleaseRequest(
                releaseContext, laneState.OperationStateRevision, finalVersion,
                new IdempotencyKey($"agentkit.loop:{request.RunId}:release"));
            var capability = new SessionExecutionCapability(request.SessionProfile, services.Session, runCoordinator);
            var result = await services.Session.ReleaseRunAsync(release, capability, CancellationToken.None).ConfigureAwait(false);
            if (result is not SessionRunReleased)
            {
                LoopLog.LaneReleaseRejected(_logger, request.RunId, result.GetType().Name);
            }
        }
        catch (Exception exception)
        {
            LoopLog.LaneReleaseFaulted(_logger, request.RunId, exception.GetType().FullName ?? exception.GetType().Name);
        }
    }

    /// <summary>Drives the run after its activity and start diagnostics are in place.</summary>
    /// <param name="request">The validated run request.</param>
    /// <param name="services">The compiled per-run collaborator bundle.</param>
    /// <param name="operationId">The run's causal operation identity.</param>
    /// <param name="runActivity">
    /// The loop's own run activity, or null when no listener sampled it. Model tags are set on this activity
    /// only; they are never written to whatever <see cref="Activity.Current"/> happens to be, which may be a
    /// host-owned parent when the loop's activity was not sampled.
    /// </param>
    /// <param name="laneState">The run's tracked lane identity and current total-state revision.</param>
    /// <param name="cancellationToken">The caller's cancellation.</param>
    /// <returns>The complete result of the run.</returns>
    private async Task<AgentLoopResult> RunCoreAsync(
        AgentLoopRunRequest request,
        AgentRunServices services,
        OperationId operationId,
        Activity? runActivity,
        LoopLaneState laneState,
        CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "A validated run request is required by the loop core.");
        var runCorrelation = new InRunOperationCorrelation(operationId, request.RunId, turnId: null);
        var (runAuthorization, runCaptureFailure) = await CaptureAuthorizationAsync(
            request, services, runCorrelation, cancellationToken)
            .ConfigureAwait(false);
        if (runAuthorization is null)
        {
            // Nothing has been observed or committed: no branch version exists to report truthfully.
            return BuildResult(request, runCaptureFailure!, [], finalVersion: null, laneState.Usage);
        }

        var sessionContext = new SessionOperationContext(
            request.AgentId,
            request.SessionId,
            executionLaneId: laneState.ExecutionLaneId,
            runCorrelation,
            request.Identity,
            runAuthorization);

        var historyLoad = await LoadHistoryAsync(
            request.RunId, services, sessionContext, request.SessionProfile, request.BranchId, cancellationToken).ConfigureAwait(false);
        if (historyLoad is not { } loaded)
        {
            return BuildResult(
                request,
                RunOutcomes.SessionOperationFailed("The run's eligible session history could not be loaded."),
                [],
                finalVersion: null,
                laneState.Usage);
        }

        var initialCursor = loaded.Cursor;
        var currentVersion = initialCursor.Version;
        var committedMessages = ImmutableArray.CreateBuilder<AgentMessage>();

        // The model-facing history starts at the newest active checkpoint when one exists: its summary, projected as
        // synthetic runtime evidence, followed by exactly the retained suffix and everything after the checkpoint.
        // The cursor still names the real branch tip, so every append below is guarded by the actual version.
        var retainedMessages = ToMessages(loaded.Entries);
        var initialMessages = retainedMessages;
        if (loaded.Checkpoint is { } checkpoint)
        {
            initialMessages = retainedMessages.Insert(0, CompactionCheckpointProjector.Project(checkpoint, initialCursor));
            _ = runActivity?.SetTag(AgentKitTagNames.CompactionId, checkpoint.Record.Context.CompactionId.ToString());
            LoopLog.HistoryReconstructedFromCompactionCheckpoint(
                _logger,
                request.RunId,
                checkpoint.Record.Context.CompactionId,
                checkpoint.Sequence,
                loaded.CoveredEntryCount,
                retainedMessages.Length);
        }

        var (history, recoveryFailure) = await SettleDanglingToolCallsAsync(
            request, services, sessionContext, runCorrelation, loaded.Entries, initialMessages, initialCursor, committedMessages, cancellationToken)
            .ConfigureAwait(false);
        if (recoveryFailure is not null)
        {
            return BuildResult(request, recoveryFailure, committedMessages.ToImmutable(), currentVersion, laneState.Usage);
        }

        currentVersion = history.SourceCursor.Version;

        if (request.LaneAdmission is { } admissionBeforeFirstRequest
            && await IsDurableAbortRequestedAsync(
                request, services, admissionBeforeFirstRequest, laneState, cancellationToken).ConfigureAwait(false))
        {
            return BuildResult(
                request,
                RunOutcomes.Cancelled("The run was durably aborted before its first model request."),
                committedMessages.ToImmutable(),
                currentVersion,
                laneState.Usage);
        }

        var beforeFirstModelRequest = request.LaneAdmission is { } admissionForPromotion
            ? await TryPromoteInputAsync(
                request, services, laneState, PromotionBoundary.BeforeFirstModelRequest, previousTurnId: null,
                admissionForPromotion.AcceptedCorrelation.TurnId!.Value, history.SourceCursor, lastEntryId: null,
                cancellationToken)
                .ConfigureAwait(false)
            : null;
        if (beforeFirstModelRequest is { } promotedBeforeFirstRequest)
        {
            laneState.OperationStateRevision = promotedBeforeFirstRequest.NewOperationStateRevision;
            committedMessages.AddRange(promotedBeforeFirstRequest.NewMessages);
            history = new HistoryView(
                promotedBeforeFirstRequest.Cursor,
                history.Messages.AddRange(promotedBeforeFirstRequest.NewMessages),
                history.Repairs);
            currentVersion = promotedBeforeFirstRequest.Cursor.Version;
        }

        var modelResolution = await ResolveModelAsync(request, services, operationId, cancellationToken)
            .ConfigureAwait(false);

        if (modelResolution.Outcome is { } selectionFailure)
        {
            return BuildResult(request, selectionFailure, committedMessages.ToImmutable(), currentVersion, laneState.Usage);
        }

        var model = modelResolution.Model!;
        var llmModel = modelResolution.Adapter!;
        _ = runActivity?.SetTag(AgentKitTagNames.RequestModel, model.ModelId.ToString());
        _ = runActivity?.SetTag(AgentKitTagNames.ProviderName, model.ProviderId.ToString());

        var tracking = new RunTracking();
        await using var hookScope = await OpenHookScopeAsync(cancellationToken).ConfigureAwait(false);
        if (!request.BudgetLimits.IsEmpty)
        {
            if (services.Budgets is not { } budgets)
            {
                LoopLog.BudgetAuthorityMissing(_logger, request.RunId);
                return BuildResult(
                    request,
                    RunOutcomes.InvalidState("The run declares budget limits but the composition provides no budget authority."),
                    committedMessages.ToImmutable(),
                    currentVersion,
                    laneState.Usage);
            }

            var scopeResult = await budgets.CreateChildScopeAsync(
                new BudgetScopeRequest(
                    parentScopeId: null,
                    new BudgetScopeAddress(
                        request.Identity.TenantId, request.Identity.PrincipalId, request.AgentId, request.SessionId, request.RunId, null),
                    request.BudgetLimits,
                    new IdempotencyKey($"run:{request.RunId}:budget")),
                cancellationToken).ConfigureAwait(false);
            if (scopeResult is not BudgetScopeCreated created)
            {
                LoopLog.BudgetScopeNotCreated(_logger, request.RunId, scopeResult.GetType().Name);
                return BuildResult(
                    request,
                    RunOutcomes.InvalidState("The run's budget scope could not be created."),
                    committedMessages.ToImmutable(),
                    currentVersion,
                    laneState.Usage);
            }

            tracking.Budget = new RunBudget(created.Scope, request.RunId, _timeProvider);
            _ = runActivity?.SetTag(AgentKitTagNames.BudgetScopeId, created.Scope.Id.ToString());
        }

        if (hookScope is not null && CatalogIncludesPoint(hookScope.Catalog, AgentHookPoints.RunStarted))
        {
            var runStartedDispatch = CreateHookDispatch(AgentHookPoints.RunStarted, runCorrelation);
            var runStartedArgs = new RunStartedEventArgs(
                runStartedDispatch,
                request.AgentId,
                request.SessionId,
                request.BranchId,
                model,
                request.MaxTurns,
                request.AttemptTimeout);
            var runStartedContext = hookScope.CreateDispatch(runStartedDispatch);
            await _hookDispatcher!.DispatchAsync(
                AgentHookPointDefinitions.RunStarted,
                runStartedContext,
                runStartedArgs,
                HookFailureMode.IsolateAndDiagnose,
                cancellationToken).ConfigureAwait(false);
        }

        var compactionAttempted = false;
        for (var turn = 1; turn <= request.MaxTurns; turn++)
        {
            if (tracking.Budget is { } turnBudget
                && await turnBudget.CountAsync(BudgetDimensions.Turns, operationId, $"turn:{turn}", cancellationToken).ConfigureAwait(false) is { } turnExhausted)
            {
                LoopLog.BudgetExhausted(_logger, request.RunId, turnExhausted.Dimension);
                return BuildResult(
                    request, RunOutcomes.BudgetExhausted(turnExhausted, hasPartialOutput: committedMessages.Count > 0),
                    committedMessages.ToImmutable(), currentVersion, laneState.Usage);
            }

            if (!compactionAttempted && services.Compactor is { } compactor && model.Limits.MaxContextTokens is { } contextWindow)
            {
                var estimatedTokens = EstimateTokens(history.Messages);
                var threshold = contextWindow * _contextPressureThreshold;
                if (estimatedTokens > threshold)
                {
                    compactionAttempted = true;
                    history = await CompactUnderPressureAsync(
                        request, services, compactor, sessionContext, runCorrelation, runAuthorization, history,
                        estimatedTokens, contextWindow, threshold, cancellationToken).ConfigureAwait(false);
                    currentVersion = history.SourceCursor.Version;
                }
            }

            TurnOutcome result;
            try
            {
                result = await RunTurnAsync(
                    request,
                    services,
                    model,
                    llmModel,
                    operationId,
                    turn,
                    turn == 1 ? modelResolution.FirstModelRequestId : null,
                    modelResolution.Adjustments,
                    history,
                    tracking,
                    hookScope,
                    committedMessages,
                    currentVersion,
                    laneState,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested && committedMessages.Count > 0)
            {
                // Cancellation may propagate as an exception only while this run has produced no durable effect.
                // Once an earlier turn committed a message, the caller must still receive it: the run settles
                // with a typed cancelled outcome carrying every committed message and the exact branch version.
                return BuildResult(
                    request,
                    RunOutcomes.Cancelled("The run was cancelled after at least one message had been committed."),
                    committedMessages.ToImmutable(),
                    currentVersion,
                    laneState.Usage);
            }

            if (result.Outcome is not null)
            {
                return BuildResult(request, result.Outcome, committedMessages.ToImmutable(), result.Version, laneState.Usage, result.Output);
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
        AgentLoopRunRequest request,
        AgentRunServices services,
        OperationId operationId,
        CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "A validated run request is required to resolve a model.");

        var catalog = await services.Models.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
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

        var selection = await services.ModelSelector
            .SelectAsync(selectionRequest, cancellationToken)
            .ConfigureAwait(false);

        switch (selection)
        {
            case InvalidModelPolicy invalid:
                LoopLog.ModelSelectionFailed(_logger, request.RunId, invalid.Reason);
                return ModelResolution.Failed(
                    RunOutcomes.ModelSelectionFailed(invalid.Reason, []));

            case NoCompatibleModel none:
                LoopLog.ModelSelectionFailed(
                    _logger,
                    request.RunId,
                    "no compatible model");
                return ModelResolution.Failed(RunOutcomes.ModelSelectionFailed(
                    "No configured model satisfies this run's requirements.",
                    none.Diagnostics));

            case ModelSelected selected:
                var descriptor = selected.Decision.Model;
                var adapter = services.ModelResolver.Resolve(descriptor);
                if (adapter is null)
                {
                    LoopLog.ModelSelectionFailed(
                        _logger,
                        request.RunId,
                        "no adapter registered for the selected model");
                    return ModelResolution.Failed(RunOutcomes.ModelSelectionFailed(
                        $"Model alias '{descriptor.Alias}' is configured in the catalog but no "
                        + "LLM model adapter is registered to execute it.",
                        selected.Decision.Diagnostics));
                }

                return ModelResolution.Resolved(descriptor, adapter, firstModelRequestId, selected.Decision.Adjustments);

            default:
                throw new InvalidOperationException(
                    $"Unrecognized {nameof(ModelSelectionResult)} kind '{selection.GetType()}'.");
        }
    }

    /// <summary>
    /// Applies every declared capability adjustment the selection made to the
    /// settings that build this turn's request.
    /// </summary>
    /// <remarks>
    /// The selector's <see cref="CapabilitiesDowngraded"/> outcome only
    /// records that an adjustment happened; nothing applies it to the actual
    /// request settings by itself. Without this step, a run selected under
    /// <see cref="CapabilityDowngradePolicy.AllowDeclaredAdjustments"/> would
    /// still ask for <see cref="LlmRequestSettings.ParallelToolCalls"/> that
    /// the chosen model just declared it cannot honor, and the adapter's
    /// request preflight would reject the request at attempt time instead of
    /// the selection having already accounted for it.
    /// </remarks>
    private static LlmRequestSettings ApplySelectionAdjustments(
        LlmRequestSettings settings, ImmutableArray<CapabilityAdjustment> adjustments)
    {
        if (adjustments.IsDefaultOrEmpty || settings.ParallelToolCalls is not true)
        {
            return settings;
        }

        foreach (var adjustment in adjustments)
        {
            if (adjustment.Capability is ModelCapabilityKind.ParallelToolCalls)
            {
                return settings with { ParallelToolCalls = false };
            }
        }

        return settings;
    }

    private async Task<TurnOutcome> RunTurnAsync(
        AgentLoopRunRequest request,
        AgentRunServices services,
        ModelDescriptor model,
        ILlmModel llmModel,
        OperationId operationId,
        int turn,
        ModelRequestId? reservedModelRequestId,
        ImmutableArray<CapabilityAdjustment> selectionAdjustments,
        HistoryView history,
        RunTracking tracking,
        HookActivationScope? hookScope,
        ImmutableArray<AgentMessage>.Builder committedMessages,
        SessionVersion currentVersion,
        LoopLaneState laneState,
        CancellationToken cancellationToken)
    {
        var turnId = turn == 1 && request.LaneAdmission is { } firstTurnAdmission
            ? firstTurnAdmission.AcceptedCorrelation.TurnId!.Value
            : _turnIds.Create();
        var turnCorrelation = new InRunOperationCorrelation(operationId, request.RunId, turnId);
        var (turnAuthorization, turnCaptureFailure) = await CaptureAuthorizationAsync(
            request, services, turnCorrelation, cancellationToken)
            .ConfigureAwait(false);
        if (turnAuthorization is null)
        {
            return TurnOutcome.Settled(turnCaptureFailure!, currentVersion);
        }

        var turnSessionContext = new SessionOperationContext(
            request.AgentId,
            request.SessionId,
            executionLaneId: laneState.ExecutionLaneId,
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
                ApplySelectionAdjustments(agent.Settings, selectionAdjustments),
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
                ApplySelectionAdjustments(request.Settings, selectionAdjustments),
                ExtensionData.Empty);

        var assembleResult = await services.Context.AssembleAsync(assembleRequest, cancellationToken).ConfigureAwait(false);

        if (assembleResult is ContextPreparationFailed prepFailed)
        {
            turnActivity.SetFailed("context_preparation_failed", prepFailed.Failure.Kind.ToString());
            LoopLog.TurnFailed(_logger, request.RunId, turnId, "context_preparation_failed");
            return TurnOutcome.Settled(RunOutcomes.ContextPreparationFailed(prepFailed.Failure), currentVersion);
        }

        var context = ((ContextReady) assembleResult).Context;
        if (hookScope is not null && CatalogIncludesPoint(hookScope.Catalog, AgentHookPoints.BeforeModelRequest))
        {
            var beforeModelDispatch = CreateHookDispatch(AgentHookPoints.BeforeModelRequest, turnCorrelation);
            var hookArgs = new BeforeModelRequestEventArgs(
                beforeModelDispatch, request.AgentId, request.SessionId, turn, context);
            try
            {
                var hookContext = hookScope.CreateDispatch(beforeModelDispatch);
                await _hookDispatcher!.DispatchAsync(
                    AgentHookPointDefinitions.BeforeModelRequest,
                    hookContext,
                    hookArgs,
                    HookFailureMode.FailOperation,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                LoopLog.HookFailedTurn(_logger, request.RunId, turnId, AgentHookPoints.BeforeModelRequest, exception.GetType().FullName ?? exception.GetType().Name);
                turnActivity.SetFailed("hook_failed", exception.GetType().Name);
                return TurnOutcome.Settled(
                    RunOutcomes.InvalidState("A before-model-request hook failed; the request was not sent."), currentVersion);
            }

            if (hookArgs.SettingsChanged)
            {
                context = context with { Settings = hookArgs.Settings };
            }
        }

        if (tracking.Budget is { } requestBudget
            && await requestBudget.CountAsync(BudgetDimensions.ModelRequests, turnCorrelation.OperationId, $"turn:{turnId}:request", cancellationToken).ConfigureAwait(false) is { } requestExhausted)
        {
            LoopLog.BudgetExhausted(_logger, request.RunId, requestExhausted.Dimension);
            turnActivity.SetFailed("budget_exhausted", requestExhausted.Dimension.Value);
            return TurnOutcome.Settled(
                RunOutcomes.BudgetExhausted(requestExhausted, hasPartialOutput: committedMessages.Count > 0), currentVersion);
        }

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
                    request.Observer is null && services.Publisher is null
                        ? NoOpModelResponseObserver.Instance
                        : new RunModelResponseObserver(
                            turnId,
                            (runEvent, token) => ObserveAsync(request, services, laneState, history.SourceCursor.ConversationId, runEvent, token)),
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
                request, services, model, history.SourceCursor, turnSessionContext, turnCorrelation, turnId, modelRequestId,
                failed.PartialParts, failed.Usage, NormalizedStopReason.Error, failed.Failure.RequestId,
                RunOutcomes.ModelAttemptFailed(failed.Failure), committedMessages, currentVersion)
                .ConfigureAwait(false),

            // A cancelled attempt with no partial parts and no earlier turn commit is a zero-effect
            // cancellation: the documented contract (see the class remarks) is that caller cancellation
            // propagates as OperationCanceledException while the run has committed nothing, exactly like the
            // top-level catch around RunTurnAsync already enforces for an adapter that throws OCE directly
            // instead of returning ModelAttemptCancelled. Settling with a typed RunCancelled outcome here
            // instead would make the same user cancellation throw for one adapter and return for another.
            ModelAttemptCancelled { PartialParts.IsEmpty: true } cancelled
                when cancellationToken.IsCancellationRequested && committedMessages.Count == 0 =>
                throw new OperationCanceledException(cancellationToken),

            ModelAttemptCancelled cancelled => await SettleInterruptedAsync(
                request, services, model, history.SourceCursor, turnSessionContext, turnCorrelation, turnId, modelRequestId,
                cancelled.PartialParts, cancelled.Usage, NormalizedStopReason.Cancelled,
                cancelled.Cancellation.RequestId, RunOutcomes.Cancelled(cancelled.Cancellation.SafeMessage),
                committedMessages, currentVersion)
                .ConfigureAwait(false),

            ModelAttemptCompleted completed => await SettleCompletedAsync(
                request, services, model, history.SourceCursor, turnSessionContext, turnCorrelation, turnId, turn, completed.Response,
                tracking, hookScope, committedMessages, currentVersion, laneState, cancellationToken)
                .ConfigureAwait(false),

            _ => throw new InvalidOperationException(
                $"Unrecognized {nameof(ModelAttemptResult)} kind '{attemptResult.GetType()}'."),
        };

        if (turnOutcome.Outcome is null or RunSucceeded)
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
        AgentLoopRunRequest request,
        AgentRunServices services,
        ModelDescriptor model,
        MessageCursor sourceCursor,
        SessionOperationContext turnSessionContext,
        InRunOperationCorrelation turnCorrelation,
        TurnId turnId,
        int turn,
        ModelResponse response,
        RunTracking tracking,
        HookActivationScope? hookScope,
        ImmutableArray<AgentMessage>.Builder committedMessages,
        SessionVersion currentVersion,
        LoopLaneState laneState,
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
                request, services, model, sourceCursor, turnSessionContext, turnCorrelation, turnId, response.RequestId,
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
                request, services, model, sourceCursor, turnSessionContext, turnCorrelation, turnId, response.RequestId,
                response.Parts, response.Usage, NormalizedStopReason.Error, response.Identity.RequestId,
                RunOutcomes.ModelAttemptFailed(new ProviderFailure(
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
                request, services, model, sourceCursor, turnSessionContext, turnCorrelation, turnId, response.RequestId,
                response.Parts, response.Usage, NormalizedStopReason.Error, response.Identity.RequestId,
                RunOutcomes.ModelAttemptFailed(new ProviderFailure(
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
            services,
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
                RunOutcomes.SessionOperationFailed(
                    "A concurrent writer committed a message to the branch while the model response was pending; " +
                    "the response is stale and was not committed."),
                currentVersion);
        }

        if (appendAttempt.Result is not SessionAppended appended)
        {
            return TurnOutcome.Settled(
                RunOutcomes.SessionOperationFailed(DescribeAppendFailure(appendAttempt.Result)), currentVersion);
        }

        currentVersion = appended.NewVersion;
        committedMessages.Add(assistantMessage);

        if (services.Publisher is { } assistantPublisher)
        {
            // A required sink's failure must propagate rather than be swallowed here: the message is already
            // durably committed, and silently continuing past a required sink's failure would misrepresent the
            // run as having delivered evidence it did not.
            await assistantPublisher.PublishAsync(
                new MessageCommittedEvent(
                    request.AgentId,
                    request.SessionId,
                    sourceCursor.ConversationId,
                    request.RunId,
                    turnId,
                    laneState.AllocateSequence(),
                    _timeProvider.GetUtcNow(),
                    assistantMessage.Id,
                    currentVersion),
                cancellationToken).ConfigureAwait(false);
        }

        // Usage is accounted for the run's own frozen projection regardless of whether budget limits are
        // configured; budget reservation below is a separate, optional enforcement concern over the same report.
        if (UsageAccounting.BuildEntry(
                _usageEntryIds.Create(), request.RunId, turnCorrelation.OperationId, model, response.RequestId, response.Usage)
            is { } usageEntry)
        {
            laneState.Usage = laneState.Usage.Apply(usageEntry);
        }

        if (tracking.Budget is { } usageBudget
            && await usageBudget.AccountUsageAsync(response.Usage, turnCorrelation.OperationId, $"turn:{turnId}:usage", cancellationToken).ConfigureAwait(false) is { } usageExhausted)
        {
            // The response is committed; the run stops here rather than spending past the limit on another request.
            LoopLog.BudgetExhausted(_logger, request.RunId, usageExhausted.Dimension);
            return TurnOutcome.Settled(RunOutcomes.BudgetExhausted(usageExhausted, hasPartialOutput: true), currentVersion);
        }

        var toolCalls = requestedCalls;
        var committedSequence = appended.CommittedEntries[^1].Sequence;

        return toolCalls switch
        {
            { IsEmpty: true } when request.Output is { } outputDefinition => await ValidateOutputAsync(
                request, services, sourceCursor, turnSessionContext, turnCorrelation, turnId, turn, assistantEntryId,
                assistantMessage, response, outputDefinition, tracking, committedMessages, currentVersion, committedSequence,
                laneState, cancellationToken)
                .ConfigureAwait(false),
            { IsEmpty: true } => await DecideContinuationAsync(
                request, services, turnCorrelation, turn, assistantMessage, [], assistantEntryId,
                NextCursor(sourceCursor, currentVersion, committedSequence), [assistantMessage], currentVersion, laneState,
                committedMessages, cancellationToken)
                .ConfigureAwait(false),
            _ when turn == request.MaxTurns => await SettleRejectedAtTurnLimitAsync(
                request, services, sourceCursor, turnSessionContext, turnCorrelation, turnId, assistantEntryId, toolCalls,
                committedMessages, currentVersion, committedSequence)
                .ConfigureAwait(false),
            _ => await InvokeToolsAsync(
                request, services, sourceCursor, turnSessionContext, turnCorrelation, turnId, turn, assistantEntryId, assistantMessage, toolCalls,
                tracking, hookScope, committedMessages, currentVersion, committedSequence, laneState, cancellationToken)
                .ConfigureAwait(false),
        };
    }

    /// <summary>
    /// Validates the committed terminal response against the run's selected <see cref="OutputDefinition"/> and
    /// hands the processor's typed decision to the continuation policy.
    /// </summary>
    /// <param name="request">The run being driven.</param>
    /// <param name="services">The compiled per-run collaborator bundle, whose <see cref="AgentRunServices.OutputProcessor"/> performs the validation.</param>
    /// <param name="sourceCursor">The history cursor the turn started from.</param>
    /// <param name="turnSessionContext">The turn's session operation context.</param>
    /// <param name="turnCorrelation">The turn's in-run correlation.</param>
    /// <param name="turnId">The committed turn's identity.</param>
    /// <param name="turn">The one-based number of the committed turn.</param>
    /// <param name="assistantEntryId">The committed assistant entry's identity.</param>
    /// <param name="assistantMessage">The complete, committed assistant response.</param>
    /// <param name="response">The provider response the assistant message was built from.</param>
    /// <param name="definition">The output contract the run must satisfy.</param>
    /// <param name="tracking">The run-scoped attempt counter the processor's retry policy is evaluated against.</param>
    /// <param name="committedMessages">Every message this run has committed so far.</param>
    /// <param name="currentVersion">The branch version after the assistant commit.</param>
    /// <param name="committedSequence">The sequence of the committed assistant entry.</param>
    /// <param name="laneState">The run's tracked lane identity and current total-state revision.</param>
    /// <param name="cancellationToken">Cancels validation; the assistant message is already committed.</param>
    /// <returns>The turn's continuation or settlement.</returns>
    /// <remarks>
    /// <para>
    /// An <see cref="OutputAccepted"/> decision completes the run with the validated value attached. An
    /// <see cref="OutputRetryRequired"/> decision commits the processor's bounded repair instruction as a
    /// <see cref="RuntimeMessage"/>, which the provider translation carries with user-level trust and never with
    /// system authority, and continues to the next turn so the model can correct its answer; the turn limit still
    /// applies. An <see cref="OutputRejected"/> or <see cref="OutputConfigurationRejected"/> decision halts the run
    /// as <see cref="RunPolicyHalted"/>. The processor never sees a turn that requested tools: output is
    /// validated only on a terminal response.
    /// </para>
    /// <para>
    /// The attempt counter is per run, not per turn, so repairs consumed on earlier turns count against the
    /// definition's retry policy. A composition that selects an output definition without an output processor fails
    /// closed as <see cref="RunFailed"/> rather than completing with unvalidated text.
    /// </para>
    /// </remarks>
    private async Task<TurnOutcome> ValidateOutputAsync(
        AgentLoopRunRequest request,
        AgentRunServices services,
        MessageCursor sourceCursor,
        SessionOperationContext turnSessionContext,
        InRunOperationCorrelation turnCorrelation,
        TurnId turnId,
        int turn,
        SessionEntryId assistantEntryId,
        AssistantMessage assistantMessage,
        ModelResponse response,
        OutputDefinition definition,
        RunTracking tracking,
        ImmutableArray<AgentMessage>.Builder committedMessages,
        SessionVersion currentVersion,
        SessionSequence committedSequence,
        LoopLaneState laneState,
        CancellationToken cancellationToken)
    {
        Debug.Assert(request.Output is not null, "Output validation runs only when the request selects a definition.");

        if (services.OutputProcessor is not { } processor)
        {
            LoopLog.OutputProcessorMissing(_logger, request.RunId, turnId, definition.Id);
            return TurnOutcome.Settled(
                RunOutcomes.InvalidState("The run selects an output definition but the composition provides no output processor."),
                currentVersion);
        }

        var attempt = tracking.NextAttempt();
        OutputProcessingResult decision;
        try
        {
            decision = await processor.ProcessAsync(
                new OutputProcessingRequest(definition, response, attempt), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LoopLog.OutputValidationCancelled(_logger, request.RunId, turnId, definition.Id, attempt);
            return TurnOutcome.Settled(
                RunOutcomes.Cancelled("The run was cancelled while its output was being validated; the response is committed."),
                currentVersion);
        }
        catch (Exception exception)
        {
            LoopLog.OutputValidationFaulted(_logger, request.RunId, turnId, definition.Id, attempt, exception.GetType().FullName ?? exception.GetType().Name);
            return TurnOutcome.Settled(
                RunOutcomes.InvalidState("The output processor faulted while validating the response."),
                currentVersion);
        }

        LoopLog.OutputDecided(_logger, request.RunId, turnId, definition.Id, attempt, decision.GetType().Name);

        if (decision is not OutputRetryRequired retry)
        {
            return await DecideContinuationAsync(
                request, services, turnCorrelation, turn, assistantMessage, [], assistantEntryId,
                NextCursor(sourceCursor, currentVersion, committedSequence), [assistantMessage], currentVersion, laneState,
                committedMessages, cancellationToken, decision).ConfigureAwait(false);
        }

        var now = _timeProvider.GetUtcNow();
        var repairMessage = new RuntimeMessage(
            _messageIds.Create(),
            request.AgentId,
            request.SessionId,
            sourceCursor.ConversationId,
            request.BranchId,
            request.RunId,
            turnId,
            now,
            MessageState.Complete,
            [new TextPart(retry.Repair.SafeMessage, TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);
        var repairEntry = new MessageSessionEntry(
            _entryIds.Create(),
            turnSessionContext.ToAddress(),
            turnCorrelation,
            request.BranchId,
            new SessionSequence(committedSequence.Value + 1),
            assistantEntryId,
            now,
            new SchemaVersion("1"),
            repairMessage);

        var appendAttempt = await AppendWithDiagnosticsAsync(
            services,
            new SessionAppendRequest(
                turnSessionContext,
                request.BranchId,
                currentVersion,
                new IdempotencyKey($"run:{request.RunId}:turn:{turnId}:output-repair"),
                [repairEntry]),
            request.SessionProfile,
            allowInterleavedMessages: true,
            cancellationToken).ConfigureAwait(false);
        if (appendAttempt.Result is not SessionAppended appended)
        {
            return TurnOutcome.Settled(
                RunOutcomes.SessionOperationFailed(DescribeAppendFailure(appendAttempt.Result)), currentVersion);
        }

        committedMessages.Add(repairMessage);
        var repairSequence = appended.CommittedEntries[^1].Sequence;
        return await DecideContinuationAsync(
            request, services, turnCorrelation, turn, assistantMessage, [], repairEntry.Id,
            NextCursor(sourceCursor, appended.NewVersion, repairSequence),
            [assistantMessage, .. appendAttempt.InterleavedMessages, repairMessage], appended.NewVersion, laneState,
            committedMessages, cancellationToken, retry).ConfigureAwait(false);
    }

    /// <summary>Reads whether a durable abort marker is committed for this run before the next promotion attempt.</summary>
    /// <param name="request">The run being driven.</param>
    /// <param name="services">The compiled per-run collaborator bundle.</param>
    /// <param name="admission">The accepted lane admission installed for this run.</param>
    /// <param name="laneState">The run's tracked lane identity and current total-state revision.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns><see langword="true"/> when <see cref="SessionRunStateLoaded.AbortRequested"/> is set; otherwise <see langword="false"/>.</returns>
    private async ValueTask<bool> IsDurableAbortRequestedAsync(
        AgentLoopRunRequest request,
        AgentRunServices services,
        LoopLaneAdmission admission,
        LoopLaneState laneState,
        CancellationToken cancellationToken)
    {
        if (services.RunCoordinator is not { } runCoordinator)
        {
            return false;
        }

        var (authorization, _) = await CaptureAuthorizationAsync(
            request, services, admission.AcceptedCorrelation, cancellationToken).ConfigureAwait(false);
        if (authorization is null)
        {
            return false;
        }

        var context = new SessionOperationContext(
            request.AgentId, request.SessionId, laneState.ExecutionLaneId, admission.AcceptedCorrelation,
            request.Identity, authorization);
        var capability = new SessionExecutionCapability(request.SessionProfile, services.Session, runCoordinator);
        var loaded = await services.Session.LoadRunStateAsync(new SessionRunStateRequest(context), capability, cancellationToken)
            .ConfigureAwait(false);
        return loaded is SessionRunStateLoaded { AbortRequested: true };
    }

    /// <summary>
    /// Attempts one atomic input-promotion transition at a safe loop boundary, best-effort: a rejection,
    /// stale-evidence conflict, or fault is logged and treated as nothing to promote rather than failing the run.
    /// </summary>
    /// <param name="request">The run being driven; promotion is skipped entirely when it carries no <see cref="AgentLoopRunRequest.LaneAdmission"/>.</param>
    /// <param name="services">The compiled per-run collaborator bundle; promotion is skipped entirely when <see cref="AgentRunServices.Input"/> is <see langword="null"/>.</param>
    /// <param name="laneState">
    /// The run's tracked lane identity and current total-state revision. Not mutated here on a successful
    /// commit: the caller applies the returned <see cref="PromotionAttemptOutcome.NewOperationStateRevision"/>
    /// once it no longer needs <see cref="LoopLaneState.OperationStateRevision"/>'s pre-commit value for
    /// continuation-evidence consistency. Mutated directly here only when the commit succeeded but its content
    /// could not be reloaded, since no outcome is returned to carry the advance in that case.
    /// </param>
    /// <param name="boundary">The exact safe boundary this attempt occurs at.</param>
    /// <param name="previousTurnId">The committed turn preceding this boundary, or <see langword="null"/> before the run's first turn.</param>
    /// <param name="targetTurnId">The turn that would receive the promoted input in the atomic history transition.</param>
    /// <param name="fromCursor">The history cursor already observed by the loop, whose sequence becomes the promotion's admission cutoff.</param>
    /// <param name="lastEntryId">The identity of the last entry the loop has actually observed on the branch, or <see langword="null"/> when none is known.</param>
    /// <param name="cancellationToken">Cancels the attempt.</param>
    /// <returns>The committed promotion's evidence and newly visible messages, or <see langword="null"/> when nothing was promoted.</returns>
    /// <remarks>
    /// A committed promotion's messages are not reconstructed from the coordinator's evidence: the session store
    /// alone materializes the exact <see cref="UserMessage"/> records (identity, timestamp, and causal placement),
    /// so this method reloads them by reading forward from <paramref name="fromCursor"/> rather than re-appending
    /// anything the store already committed.
    /// </remarks>
    private async Task<PromotionAttemptOutcome?> TryPromoteInputAsync(
        AgentLoopRunRequest request,
        AgentRunServices services,
        LoopLaneState laneState,
        PromotionBoundary boundary,
        TurnId? previousTurnId,
        TurnId targetTurnId,
        MessageCursor fromCursor,
        SessionEntryId? lastEntryId,
        CancellationToken cancellationToken)
    {
        if (services.Input is not { } coordinator || request.LaneAdmission is not { } admission)
        {
            return null;
        }

        LoopLog.InputPromotionAttempted(_logger, request.RunId, boundary);
        var (authorization, _) = await CaptureAuthorizationAsync(
            request, services, admission.AcceptedCorrelation, cancellationToken).ConfigureAwait(false);
        if (authorization is null)
        {
            LoopLog.InputPromotionFaulted(_logger, request.RunId, boundary, "authorization_unavailable");
            return null;
        }

        InputPromotionResult result;
        try
        {
            var promotionRequest = new InputPromotionRequest(
                request.AgentId,
                request.SessionId,
                laneState.ExecutionLaneId,
                admission.AcceptedCorrelation,
                laneState.OperationStateRevision,
                new SessionBranchCursor(request.BranchId, lastEntryId),
                fromCursor.Sequence,
                expectedVersion: null,
                expectedFencingToken: null,
                request.Identity,
                authorization,
                boundary,
                previousTurnId,
                targetTurnId,
                _maximumPromotionsPerBoundary);
            result = await coordinator.PromoteAsync(promotionRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LoopLog.InputPromotionFaulted(_logger, request.RunId, boundary, exception.GetType().FullName ?? exception.GetType().Name);
            return null;
        }

        switch (result)
        {
            case InputPromoted promoted:
                var sessionContext = new SessionOperationContext(
                    request.AgentId, request.SessionId, laneState.ExecutionLaneId, admission.AcceptedCorrelation,
                    request.Identity, authorization);
                var reloaded = await LoadEntriesAfterAsync(
                    request, services, sessionContext, fromCursor, promoted.SessionVersion, cancellationToken)
                    .ConfigureAwait(false);
                if (reloaded is not { } page)
                {
                    // The store committed this promotion durably even though the loop could not reload its
                    // content; the caller still must observe the lane's now-current revision so a later
                    // operation on this lane (another promotion, or the eventual release) is not fenced.
                    laneState.OperationStateRevision = promoted.OperationStateRevision;
                    LoopLog.InputPromotionFaulted(_logger, request.RunId, boundary, "history_reload_failed");
                    return null;
                }

                LoopLog.InputPromotionCommitted(_logger, request.RunId, boundary, promoted.Promoted.Length);
                return new PromotionAttemptOutcome(
                    promoted.Snapshot, page.Cursor, page.Messages, page.LastEntryId, promoted.OperationStateRevision);

            case InputPromotionRejected { Rejection.Kind: InputRejectionKind.NoEligibleInput } none:
                LoopLog.InputPromotionSkipped(_logger, request.RunId, boundary, none.Rejection.SafeReason);
                return null;

            case InputPromotionRejected rejected:
                LoopLog.InputPromotionRejected(_logger, request.RunId, boundary, rejected.Rejection.SafeReason);
                return null;

            case InputPromotionConflict conflict:
                LoopLog.InputPromotionConflict(_logger, request.RunId, boundary, conflict.Kind.ToString());
                return null;

            default:
                LoopLog.InputPromotionFaulted(_logger, request.RunId, boundary, "unrecognized_result");
                return null;
        }
    }

    /// <summary>Reads every entry committed after <paramref name="fromCursor"/> and projects the messages among them.</summary>
    /// <param name="request">The run whose branch is read.</param>
    /// <param name="services">The compiled per-run collaborator bundle.</param>
    /// <param name="sessionContext">The session operation context the read is authorized under.</param>
    /// <param name="fromCursor">The exact previously observed cursor; only entries after its sequence are returned.</param>
    /// <param name="newVersion">The branch version to stamp on the returned cursor.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The advanced cursor, newly visible messages in sequence order, and the last entry's identity; <see langword="null"/> when the read failed.</returns>
    private async Task<(MessageCursor Cursor, ImmutableArray<AgentMessage> Messages, SessionEntryId? LastEntryId)?> LoadEntriesAfterAsync(
        AgentLoopRunRequest request,
        AgentRunServices services,
        SessionOperationContext sessionContext,
        MessageCursor fromCursor,
        SessionVersion newVersion,
        CancellationToken cancellationToken)
    {
        var entries = ImmutableArray.CreateBuilder<SessionEntry>();
        var cursor = fromCursor.Sequence;
        SessionReadSnapshot? snapshot = null;
        while (true)
        {
            var pageResult = await services.Session.ReadAsync(
                snapshot is null
                    ? new SessionReadRequest(sessionContext, request.BranchId, cursor, _historyReadPageSize)
                    : new SessionReadRequest(sessionContext, request.BranchId, cursor, _historyReadPageSize, snapshot),
                request.SessionProfile,
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

        var built = entries.ToImmutable();
        var messages = ToMessages(built);
        var lastEntryId = built.IsEmpty ? (SessionEntryId?) null : built[^1].Id;
        var nextCursor = new MessageCursor(
            fromCursor.AgentId, fromCursor.SessionId, fromCursor.ConversationId, fromCursor.BranchId, newVersion, cursor);
        return (nextCursor, messages, lastEntryId);
    }

    /// <summary>Carries one committed promotion's evidence back to its caller.</summary>
    /// <param name="Snapshot">
    /// The exact pre-commit selection evidence the session owner revalidated, unchanged by the commit: this is
    /// what a <see cref="PromotedInputContinuationCause"/> built from this outcome must carry so it matches the
    /// continuation context built from the same pre-promotion values.
    /// </param>
    /// <param name="Cursor">The advanced history cursor after the promoted messages.</param>
    /// <param name="NewMessages">The promoted messages, in commit order.</param>
    /// <param name="LastEntryId">The identity of the last committed entry, or <see langword="null"/> when none was read back.</param>
    /// <param name="NewOperationStateRevision">
    /// The lane's total-state revision as installed by this commit; the caller applies this to
    /// <see cref="LoopLaneState.OperationStateRevision"/> only once it no longer needs the pre-commit value for
    /// evidence consistency.
    /// </param>
    private readonly record struct PromotionAttemptOutcome(
        InputPromotionSnapshot Snapshot,
        MessageCursor Cursor,
        ImmutableArray<AgentMessage> NewMessages,
        SessionEntryId? LastEntryId,
        OperationStateRevision NewOperationStateRevision);

    /// <summary>Builds the continuation causes that describe one committed turn to the policy.</summary>
    /// <param name="toolResults">The committed tool results of the turn, or empty.</param>
    /// <param name="outputDecision">The output processor's decision for the turn, or <see langword="null"/>.</param>
    /// <param name="promoted">The promotion evidence to weigh alongside the turn's other causes, or <see langword="null"/> when nothing was promoted.</param>
    /// <returns>Promotion evidence first when present, tool-result evidence when tools ran, repair evidence when a repair turn follows, otherwise empty.</returns>
    private static ImmutableArray<RunContinuationCause> ContinuationCauses(
        ImmutableArray<CommittedToolResultReference> toolResults,
        OutputProcessingResult? outputDecision,
        InputPromotionSnapshot? promoted = null)
    {
        Debug.Assert(toolResults.IsEmpty || outputDecision is null, "Output is validated only on a turn without tool calls.");
        var causes = ImmutableArray.CreateBuilder<RunContinuationCause>();
        if (promoted is { } snapshot)
        {
            causes.Add(new PromotedInputContinuationCause(snapshot));
        }

        if (!toolResults.IsEmpty)
        {
            causes.Add(new CommittedToolResultsContinuationCause(toolResults));
        }

        if (outputDecision is OutputRetryRequired retry)
        {
            causes.Add(new OutputRepairContinuationCause(retry));
        }

        return causes.ToImmutable();
    }

    /// <summary>
    /// Settles every tool call requested on the final permitted turn with a rejected terminal result, so the
    /// already-committed assistant message never leaves a call without its exactly-one result.
    /// </summary>
    private async Task<TurnOutcome> SettleRejectedAtTurnLimitAsync(
        AgentLoopRunRequest request,
        AgentRunServices services,
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
                DefaultProjection(ToolTerminalStatus.Denied),
                ExtensionData.Empty);
            resultParts.Add(rejected);
            await ObserveDetachedAsync(request, new AgentRunToolCallCompleted(turnId, rejected)).ConfigureAwait(false);
        }

        var appendAttempt = await CommitToolMessageAsync(
            request, services, sourceCursor, turnSessionContext, turnCorrelation, turnId, assistantEntryId,
            resultParts.ToImmutable(), currentVersion, currentSequence, committedMessages).ConfigureAwait(false);

        return appendAttempt.Result is SessionAppended appended
            ? TurnOutcome.Settled(RunOutcomes.TurnLimitReached(request.MaxTurns), appended.NewVersion)
            : TurnOutcome.Settled(RunOutcomes.SessionOperationFailed(DescribeAppendFailure(appendAttempt.Result)), currentVersion);
    }

    /// <summary>
    /// Builds and durably commits the tool message carrying one terminal result per requested call. The commit
    /// runs under the loop's own bounded settlement token rather than the caller's: the assistant message that
    /// requested these calls is already committed, so its results must land no matter how the run itself settles.
    /// For the same reason a concurrent message landing ahead of the tool message never refuses the commit; the
    /// interleaved entries are returned so the next turn's history reflects them.
    /// </summary>
    private async ValueTask<AppendAttempt> CommitToolMessageAsync(
        AgentLoopRunRequest request,
        AgentRunServices services,
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
            services,
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
        AgentLoopRunRequest request,
        AgentRunServices services,
        MessageCursor sourceCursor,
        SessionOperationContext turnSessionContext,
        InRunOperationCorrelation turnCorrelation,
        TurnId turnId,
        int turn,
        SessionEntryId assistantEntryId,
        AssistantMessage assistantMessage,
        ImmutableArray<ToolCallPart> toolCalls,
        RunTracking tracking,
        HookActivationScope? hookScope,
        ImmutableArray<AgentMessage>.Builder committedMessages,
        SessionVersion currentVersion,
        SessionSequence currentSequence,
        LoopLaneState laneState,
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

                if (tracking.Budget is { } callBudget
                    && await callBudget.CountAsync(BudgetDimensions.AttemptedToolCalls, turnCorrelation.OperationId, $"call:{toolCall.CallId}", cancellationToken).ConfigureAwait(false) is { } callExhausted)
                {
                    LoopLog.BudgetExhausted(_logger, request.RunId, callExhausted.Dimension);
                    resultPart = BudgetRejectedResultPart(toolCall, callExhausted);
                    resultParts.Add(resultPart);
                    await ObserveDetachedAsync(request, new AgentRunToolCallCompleted(turnId, resultPart)).ConfigureAwait(false);
                    continue;
                }

                var arguments = toolCall.Arguments;
                if (hookScope is not null && CatalogIncludesPoint(hookScope.Catalog, AgentHookPoints.BeforeToolInvocation))
                {
                    var beforeToolDispatch = CreateHookDispatch(AgentHookPoints.BeforeToolInvocation, turnCorrelation);
                    var hookArgs = new BeforeToolInvocationEventArgs(
                        beforeToolDispatch, request.AgentId, request.SessionId, toolCall);
                    var hookContext = hookScope.CreateDispatch(beforeToolDispatch);
                    await _hookDispatcher!.DispatchAsync(
                        AgentHookPointDefinitions.BeforeToolInvocation,
                        hookContext,
                        hookArgs,
                        HookFailureMode.FailOperation,
                        cancellationToken).ConfigureAwait(false);
                    if (hookArgs.Veto is { } veto)
                    {
                        LoopLog.ToolCallVetoed(_logger, request.RunId, toolCall.CallId);
                        resultPart = VetoedResultPart(toolCall, veto);
                        resultParts.Add(resultPart);
                        await ObserveDetachedAsync(request, new AgentRunToolCallCompleted(turnId, resultPart)).ConfigureAwait(false);
                        continue;
                    }

                    arguments = hookArgs.Arguments;
                }

                var resolved = await services.Tools.InvokeAsync(
                    new LegacyToolCallRequest(toolCall.Tool, toolContext, arguments, _timeProvider.GetUtcNow()),
                    cancellationToken).ConfigureAwait(false);

                resultPart = new ToolResultPart(
                    toolCall.CallId,
                    resolved.Tool,
                    resolved.Invocation.Outcome,
                    resolved.Invocation.Content,
                    new ToolResultProjectionInfo(
                        resolved.ProjectionPolicy,
                        Enum.IsDefined(resolved.Invocation.Outcome.SourceStatus)
                            ? []
                            : [ToolResultProjectionLoss.StatusCoarsened],
                        0,
                        0),
                    ExtensionData.Empty);
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
                    DefaultProjection(ToolTerminalStatus.InvocationFailed),
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
            request, services, sourceCursor, turnSessionContext, turnCorrelation, turnId, assistantEntryId,
            resultParts.ToImmutable(), currentVersion, currentSequence, committedMessages).ConfigureAwait(false);

        if (appendAttempt.Result is not SessionAppended appended)
        {
            activity.SetFailed("session_append_failed", appendAttempt.Result.GetType().Name);
            LoopLog.ToolBatchFailed(_logger, request.RunId, turnId, appendAttempt.Result.GetType().Name);
            return TurnOutcome.Settled(
                RunOutcomes.SessionOperationFailed(DescribeAppendFailure(appendAttempt.Result)), currentVersion);
        }

        var toolMessage = committedMessages[^1];

        if (services.Publisher is { } toolPublisher)
        {
            // Published regardless of the interruption/cancellation check below: the tool message is already
            // durably committed at this point, and a required sink's failure must propagate rather than be
            // swallowed, exactly as for the assistant message commit.
            await toolPublisher.PublishAsync(
                new MessageCommittedEvent(
                    request.AgentId,
                    request.SessionId,
                    sourceCursor.ConversationId,
                    request.RunId,
                    turnId,
                    laneState.AllocateSequence(),
                    _timeProvider.GetUtcNow(),
                    toolMessage.Id,
                    appended.NewVersion),
                cancellationToken).ConfigureAwait(false);
        }

        if (interrupted || cancellationToken.IsCancellationRequested)
        {
            // The tool message is now durably committed. Cancellation observed at this point settles the run
            // with a typed outcome rather than throwing: an exception here would discard a result whose
            // messages already landed, and IAgentLoop promises exactly one terminal outcome for every effect.
            activity.SetFailed("cancelled", "cancellation");
            LoopLog.ToolBatchInterrupted(_logger, request.RunId, turnId);
            return TurnOutcome.Settled(
                RunOutcomes.Cancelled("The run was cancelled while its tool calls were being invoked; every requested call was settled with a terminal result before the run stopped."),
                appended.NewVersion);
        }

        activity.SetSuccessful("completed");
        LoopLog.ToolBatchCompleted(_logger, request.RunId, turnId, toolCalls.Length);
        // The next cursor covers everything through the committed tool message, so any message a concurrent
        // writer interleaved between the assistant request and its results must become visible to the next turn
        // in sequence order; otherwise the cursor would claim history the next request never saw.
        var toolEntry = appended.CommittedEntries[^1];
        var nextCursor = NextCursor(sourceCursor, appended.NewVersion, toolEntry.Sequence);
        // Every call's terminal result was committed as one ContentPart of the single batched ToolMessage entry,
        // so there is exactly one real SessionEntryId for the whole batch. CommittedToolResultReference requires
        // a distinct SessionEntryId per call so the continuation policy sees one syntactically distinct
        // identity for each; DerivePerCallEntryId supplies that without changing what was actually committed.
        // See its own remarks for exactly what these derived identities do and do not mean.
        var toolResultReferences = toolCalls
            .Select((call, index) => new CommittedToolResultReference(
                DerivePerCallEntryId(toolEntry.Id, index), call.CallId, turnId))
            .ToImmutableArray();
        return await DecideContinuationAsync(
            request, services, turnCorrelation, turn, assistantMessage, toolResultReferences, toolEntry.Id, nextCursor,
            [assistantMessage, .. appendAttempt.InterleavedMessages, toolMessage], appended.NewVersion, laneState,
            committedMessages, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Rebuilds the <see cref="AgentLoopOptions"/> this instance resolved at construction, for policy-version computation.</summary>
    /// <returns>A fresh options instance carrying exactly this instance's captured resolved values.</returns>
    private AgentLoopOptions EffectiveLoopOptions() => new()
    {
        HistoryReadPageSize = _historyReadPageSize,
        AppendConflictRetryLimit = _appendConflictRetryLimit,
        DisableToolsOnFinalTurn = _disableToolsOnFinalTurn,
        SettlementTimeout = _settlementTimeout,
        ObserverDeliveryTimeout = _observerDeliveryTimeout,
        ContextPressureThreshold = _contextPressureThreshold,
        EstimatedCharactersPerToken = _estimatedCharactersPerToken,
        MaximumPromotionsPerBoundary = _maximumPromotionsPerBoundary,
    };

    /// <summary>
    /// Asks the selected <see cref="IRunContinuationPolicy"/> what happens after one committed turn and maps its
    /// proposal onto the loop's own transition.
    /// </summary>
    /// <param name="request">The run being driven.</param>
    /// <param name="services">The compiled per-run collaborator bundle.</param>
    /// <param name="turnCorrelation">The committed turn's in-run correlation.</param>
    /// <param name="turn">The one-based number of the committed turn.</param>
    /// <param name="assistantMessage">The complete, committed assistant response of the turn.</param>
    /// <param name="toolResults">One reference per requested call to its committed terminal result, or empty when the turn requested no tool.</param>
    /// <param name="lastEntryId">The identity of the last entry this turn committed.</param>
    /// <param name="nextCursor">The exact history cursor after this turn's commits.</param>
    /// <param name="newMessages">The messages newly visible to the next turn, in sequence order.</param>
    /// <param name="version">The branch version after this turn's commits.</param>
    /// <param name="laneState">The run's tracked lane identity and current total-state revision.</param>
    /// <param name="committedMessages">Every message this run has committed so far; a committed promotion's messages are added here.</param>
    /// <param name="cancellationToken">Cancels the policy's evaluation.</param>
    /// <param name="outputDecision">
    /// The output processor's decision for this terminal turn, or <see langword="null"/> when the run selects no
    /// output definition or the turn requested tools. When present the boundary requires output validation, so the
    /// policy completes only on acceptance, continues on a repair decision, and halts on rejection.
    /// </param>
    /// <returns>A continuation to the next turn, or the settled outcome proposed by the policy.</returns>
    /// <remarks>
    /// <para>
    /// <see cref="ContinueRun"/> continues while a turn remains; on the final turn it settles with
    /// <see cref="RunPolicyHalted"/> because the policy cannot widen the hard limit.
    /// <see cref="CompleteRun"/> and <see cref="HaltRun"/> settle with the proposed outcome. Cancellation while the
    /// policy evaluates settles with <see cref="RunCancelled"/>: the turn's messages are already committed,
    /// so the caller must receive them. A context the abstractions reject fails closed as
    /// <see cref="RunFailed"/>.
    /// </para>
    /// <para>
    /// The lane identity and operation-state revision presented to the policy are <paramref name="laneState"/>'s
    /// live values: the exact lane the run's admission installed (or the session-derived lane when the run carries
    /// no admission), and the revision as last observed by this run rather than a value fabricated from the turn
    /// number. Its policy version is computed by <see cref="RunPolicyVersioning.Compute"/> from the run's effective
    /// turn limit and attempt timeout, the selected continuation policy key, and this instance's resolved
    /// <see cref="AgentLoopOptions"/>, so a change to any of those — including a live options reload that never
    /// advances the owning <see cref="AgentDefinition"/>'s revision — names a different version. The reduced loop
    /// projects every result of a batch into one tool message entry, so there is
    /// exactly one real <see cref="SessionEntryId"/> for a batch of any size. Because
    /// <see cref="CommittedTurnContinuationBoundary"/> requires a distinct <see cref="SessionEntryId"/> per
    /// reference, every entry in <paramref name="toolResults"/> for a batch of more than one call already
    /// carries a <em>derived</em> per-call identity built by <see cref="DerivePerCallEntryId"/> — a stable
    /// value computed from the real batch entry identity and the call's position, documented there as
    /// policy-facing only and never a real session entry. This lets the policy be consulted uniformly for
    /// every committed-tool-results turn, whether it requested one call or many, instead of a single-call
    /// special case that silently skipped every parallel tool-use turn.
    /// </para>
    /// </remarks>
    private async ValueTask<TurnOutcome> DecideContinuationAsync(
        AgentLoopRunRequest request,
        AgentRunServices services,
        InRunOperationCorrelation turnCorrelation,
        int turn,
        AssistantMessage assistantMessage,
        ImmutableArray<CommittedToolResultReference> toolResults,
        SessionEntryId lastEntryId,
        MessageCursor nextCursor,
        ImmutableArray<AgentMessage> newMessages,
        SessionVersion version,
        LoopLaneState laneState,
        ImmutableArray<AgentMessage>.Builder committedMessages,
        CancellationToken cancellationToken,
        OutputProcessingResult? outputDecision = null)
    {
        Debug.Assert(request is not null, "A validated run request is required to decide continuation.");
        Debug.Assert(turnCorrelation.TurnId is not null, "Continuation is decided for one committed turn.");
        Debug.Assert(assistantMessage.State == MessageState.Complete, "Only a committed complete response reaches continuation.");
        var turnId = turnCorrelation.TurnId.Value;
        var policyVersion = RunPolicyVersioning.Compute(
            request.MaxTurns, request.AttemptTimeout, AgentLoopComponentDefaults.ContinuationPolicyKey, EffectiveLoopOptions());

        // The continuation context and the PromotedInputContinuationCause it may carry must describe the exact
        // same pre-promotion evidence (see ArgumentExceptionExtensions.ThrowIfInconsistentContinuationEvidence):
        // the revision, branch cursor, and cutoff below are deliberately the values observed before this
        // attempt, not the store's new state after it commits. The merged cursor/messages a Continue decision
        // actually resumes from are tracked separately below.
        if (request.LaneAdmission is { } admissionAfterTurn
            && await IsDurableAbortRequestedAsync(
                request, services, admissionAfterTurn, laneState, cancellationToken).ConfigureAwait(false))
        {
            return TurnOutcome.Settled(
                RunOutcomes.Cancelled("The run was durably aborted at a safe input boundary."), version);
        }

        var afterTurnCommitted = await TryPromoteInputAsync(
            request, services, laneState, PromotionBoundary.AfterTurnCommitted, turnId, _turnIds.Create(), nextCursor,
            lastEntryId, cancellationToken).ConfigureAwait(false);

        RunContinuationContext context;
        try
        {
            context = new RunContinuationContext(
                request.AgentId,
                request.SessionId,
                laneState.ExecutionLaneId,
                turnCorrelation.OperationId,
                request.RunId,
                AgentRunState.Driving,
                laneState.OperationStateRevision,
                new SessionBranchCursor(request.BranchId, lastEntryId),
                nextCursor.Sequence,
                request.Authorization.ConfigurationVersion,
                policyVersion,
                new CommittedTurnContinuationBoundary(
                    assistantMessage, toolResults, outputDecision, requiresOutputValidation: outputDecision is not null),
                requiredStopOutcome: null,
                ContinuationCauses(toolResults, outputDecision, afterTurnCommitted?.Snapshot));
        }
        catch (ArgumentException exception)
        {
            LoopLog.ContinuationFailed(_logger, request.RunId, exception.GetType().FullName ?? exception.GetType().Name);
            return TurnOutcome.Settled(
                RunOutcomes.InvalidState("The committed turn could not be described as continuation evidence."), version);
        }

        // The promotion, if any, is durably committed regardless of what the policy decides below: the lane's
        // real revision and version have already advanced in the store, and every promoted message belongs in
        // this run's result whether the run continues or settles here.
        if (afterTurnCommitted is { } committed)
        {
            laneState.OperationStateRevision = committed.NewOperationStateRevision;
            committedMessages.AddRange(committed.NewMessages);
            nextCursor = committed.Cursor;
            newMessages = [.. newMessages, .. committed.NewMessages];
            version = committed.Cursor.Version;
        }

        RunContinuationDecision decision;
        try
        {
            decision = await services.ContinuationPolicy.DecideAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LoopLog.ContinuationCancelled(_logger, request.RunId);
            return TurnOutcome.Settled(
                RunOutcomes.Cancelled("The run was cancelled while its continuation was being decided; the turn's messages are committed."),
                version);
        }

        // A policy that would otherwise complete the run gets one last chance to observe input admitted in the
        // brief window since the AfterTurnCommitted attempt above, so it is not stranded until an entirely new
        // run picks it up. Only attempted when a further turn is actually possible and nothing already promoted
        // above (which would already have produced a highest-priority PromotedInputContinuationCause).
        if (decision is CompleteRun && afterTurnCommitted is null && turn < request.MaxTurns)
        {
            if (request.LaneAdmission is { } admissionOtherwiseIdle
                && await IsDurableAbortRequestedAsync(
                    request, services, admissionOtherwiseIdle, laneState, cancellationToken).ConfigureAwait(false))
            {
                return TurnOutcome.Settled(
                    RunOutcomes.Cancelled("The run was durably aborted at a safe input boundary."), version);
            }

            var otherwiseIdle = await TryPromoteInputAsync(
                request, services, laneState, PromotionBoundary.OtherwiseIdle, turnId, _turnIds.Create(), nextCursor,
                lastEntryId, cancellationToken).ConfigureAwait(false);
            if (otherwiseIdle is { } idlePromotion)
            {
                laneState.OperationStateRevision = idlePromotion.NewOperationStateRevision;
                committedMessages.AddRange(idlePromotion.NewMessages);
                LoopLog.ContinuationDecisionApplied(_logger, request.RunId, turnId, nameof(ContinueRun));
                return TurnOutcome.Continue(idlePromotion.Cursor, [.. newMessages, .. idlePromotion.NewMessages]);
            }
        }

        LoopLog.ContinuationDecisionApplied(_logger, request.RunId, turnId, decision.GetType().Name);
        return decision switch
        {
            ContinueRun when turn < request.MaxTurns => TurnOutcome.Continue(nextCursor, newMessages),
            ContinueRun => TurnOutcome.Settled(RunOutcomes.TurnLimitReached(request.MaxTurns), version),
            CompleteRun complete => TurnOutcome.Settled(complete.Outcome, version, outputDecision is OutputAccepted accepted ? accepted.Output : null),
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
    /// Derives a deterministic, policy-facing-only per-call <see cref="SessionEntryId"/> for
    /// <see cref="CommittedToolResultReference"/> when a batch of tool results was committed as one real
    /// <see cref="MessageSessionEntry"/> covering every call.
    /// </summary>
    /// <param name="batchEntryId">The real <see cref="SessionEntryId"/> of the committed batched tool message.</param>
    /// <param name="callIndex">The zero-based position of this call within the batch, in the order the model requested it.</param>
    /// <returns>A stable, non-empty identity distinct for every <paramref name="callIndex"/> of the same batch.</returns>
    /// <remarks>
    /// <para>
    /// The reduced loop commits an entire tool-call batch as one session entry: one real
    /// <see cref="SessionEntryId"/> covers every <see cref="ToolResultPart"/> the batch produced. The
    /// committed-turn continuation boundary, however, requires <see cref="CommittedToolResultReference"/> to
    /// carry a distinct <see cref="SessionEntryId"/> per call (see
    /// <c>ArgumentExceptionExtensions.ThrowIfInvalidCommittedToolReferences</c>), so that every reference can be
    /// told apart. Rather than skip the continuation policy for any batch of more than one call — which silently
    /// exempted parallel tool use, the case most likely to need the policy — this method derives one distinct
    /// value per call from the real batch entry identity and the call's position.
    /// </para>
    /// <para>
    /// The derived value is <em>never</em> a real session entry. It is not appended, read, or otherwise treated
    /// as addressing session storage; it exists solely so <see cref="CommittedToolResultReference"/> and the
    /// continuation policy that consumes it can distinguish one call's reference from another's within the same
    /// turn. If a future revision commits one real session entry per tool result instead of one per batch, this
    /// derivation is removed and every reference's <see cref="CommittedToolResultReference.SessionEntryId"/>
    /// becomes the call's own real entry identity.
    /// </para>
    /// <para>
    /// The derivation is deterministic: the same batch entry identity and call index always produce the same
    /// derived value, so repeated evaluation of an unchanged committed turn (for example, after a stale
    /// continuation proposal is discarded and recomputed) yields identical evidence. It combines the batch
    /// entry's raw <see cref="Guid"/> bytes with the call's index through SHA-256 and forces the RFC 4122
    /// version/variant bits so the result is always a well-formed, non-<see cref="Guid.Empty"/> value regardless
    /// of index.
    /// </para>
    /// </remarks>
    private static SessionEntryId DerivePerCallEntryId(SessionEntryId batchEntryId, int callIndex)
    {
        Debug.Assert(callIndex >= 0, "A call's position within its batch is never negative.");

        Span<byte> seed = stackalloc byte[20];
        var wrote = batchEntryId.Value.TryWriteBytes(seed);
        Debug.Assert(wrote, "A GUID always writes its canonical 16 bytes.");
        BinaryPrimitives.WriteInt32LittleEndian(seed[16..], callIndex);

        Span<byte> hash = stackalloc byte[32];
        var hashed = SHA256.TryHashData(seed, hash, out var bytesWritten);
        Debug.Assert(hashed && bytesWritten == hash.Length, "SHA-256 always fills its fixed-size destination.");

        // Force the RFC 4122 version (4) and variant bits so the derived value is always a well-formed,
        // guaranteed non-empty GUID, independent of the hash's raw bit pattern.
        hash[7] = (byte) ((hash[7] & 0x0F) | 0x40);
        hash[8] = (byte) ((hash[8] & 0x3F) | 0x80);
        return new SessionEntryId(new Guid(hash[..16]));
    }

    /// <summary>
    /// Maps a completed attempt whose terminal stop reason is neither <see cref="NormalizedStopReason.Completed"/>
    /// nor <see cref="NormalizedStopReason.ToolUse"/> to the typed run outcome that truthfully names its cause.
    /// </summary>
    /// <param name="response">The completed response carrying the unaccepted stop reason.</param>
    /// <returns>
    /// <see cref="RunCancelled"/> for <see cref="NormalizedStopReason.Cancelled"/>;
    /// a <see cref="RunFailed"/> with a <see cref="AgentErrorCodes.TokenLimit"/> error for
    /// <see cref="NormalizedStopReason.Length"/>; <see cref="RunFailed"/> with
    /// <see cref="AgentErrorCodes.InvalidState"/> for <see cref="NormalizedStopReason.Deferred"/>, which this loop
    /// has no deferred-operation handoff to honour; and <see cref="RunFailed"/> with
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
            NormalizedStopReason.Cancelled => RunOutcomes.Cancelled(
                "The model reported that its response was cancelled before it completed."),
            NormalizedStopReason.Length => RunOutcomes.OutputLengthLimitReached(
                response.RequestId,
                hasPartialOutput: !response.Parts.IsEmpty,
                "The model reached its output length limit before it finished its response."),
            NormalizedStopReason.Deferred => RunOutcomes.InvalidState(
                "The model reported a deferred response, but this loop has no deferred-operation handoff to resume it."),
            NormalizedStopReason.Pending or NormalizedStopReason.Error => ProtocolViolation(
                response, $"The provider reported a completed attempt with a non-terminal stop reason ({response.StopReason})."),
            NormalizedStopReason.Completed or NormalizedStopReason.ToolUse => throw new InvalidOperationException(
                "Accepted stop reasons never reach the unaccepted-stop mapping."),
            _ => ProtocolViolation(
                response, $"The provider reported a completed attempt with an undefined stop reason ({response.StopReason})."),
        };

        static AgentRunOutcome ProtocolViolation(ModelResponse response, string safeMessage) => RunOutcomes.ModelAttemptFailed(new ProviderFailure(
            ProviderFailureKind.ProtocolViolation, response.Identity.ProviderId, response.Identity.RequestId,
            statusCode: null, providerCode: null, retryAfter: null, safeMessage, diagnosticCause: null, ExtensionData.Empty));
    }

    /// <summary>Builds the rejected terminal result a refused tool-call budget reservation produces.</summary>
    /// <param name="toolCall">The call that was not attempted.</param>
    /// <param name="exhausted">The exhaustion that refused it.</param>
    /// <returns>A rejected, not-performed result recording <see cref="ToolTerminalStatus.ResourceLimitExceeded"/>.</returns>
    private static ToolResultPart BudgetRejectedResultPart(ToolCallPart toolCall, BudgetExhaustion exhausted) => new(
        toolCall.CallId,
        toolCall.Tool,
        new ToolCallOutcome(
            ToolCallOutcomeKind.Rejected,
            ToolTerminalStatus.ResourceLimitExceeded,
            SideEffectCertainty.DefinitelyNotPerformed,
            retryable: false,
            $"The call was not attempted: {exhausted.SafeMessage}",
            ExtensionData.Empty),
        [new TextPart($"The call was not attempted: {exhausted.SafeMessage}", TextSemantics.Plain, ExtensionData.Empty)],
        DefaultProjection(ToolTerminalStatus.ResourceLimitExceeded),
        ExtensionData.Empty);

    /// <summary>Builds the rejected terminal result a hook veto produces; the veto's safe reason is what the model sees.</summary>
    /// <param name="toolCall">The vetoed call.</param>
    /// <param name="veto">The hook's typed refusal.</param>
    /// <returns>A rejected, not-performed result correlated to the call.</returns>
    private static ToolResultPart VetoedResultPart(ToolCallPart toolCall, ToolInvocationVeto veto) => new(
        toolCall.CallId,
        toolCall.Tool,
        new ToolCallOutcome(
            ToolCallOutcomeKind.Rejected,
            ToolTerminalStatus.Unsupported,
            SideEffectCertainty.DefinitelyNotPerformed,
            retryable: false,
            $"The call was vetoed before invocation: {veto.SafeReason}",
            ExtensionData.Empty),
        [new TextPart($"The call was vetoed before invocation: {veto.SafeReason}", TextSemantics.Plain, ExtensionData.Empty)],
        DefaultProjection(ToolTerminalStatus.Unsupported),
        ExtensionData.Empty);

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
        DefaultProjection(ToolTerminalStatus.Interrupted),
        ExtensionData.Empty);

    /// <summary>
    /// Builds projection provenance for a terminal result this loop settles directly (turn-limit rejection,
    /// interruption, a dangling call, or an invoker fault) rather than one produced by an actual tool invocation.
    /// </summary>
    /// <param name="sourceStatus">The exact terminal status this loop is recording.</param>
    /// <returns>
    /// Projection provenance under the well-known default policy, recording
    /// <see cref="ToolResultProjectionLoss.StatusCoarsened"/> only if a future loop change ever passes an
    /// undefined status; every status this loop assigns directly is a defined, exact value.
    /// </returns>
    private static ToolResultProjectionInfo DefaultProjection(ToolTerminalStatus sourceStatus) => new(
        ToolResultProjectionPolicyReference.Default,
        Enum.IsDefined(sourceStatus) ? [] : [ToolResultProjectionLoss.StatusCoarsened],
        0,
        0);

    /// <summary>Delivers optional run progress without allowing presentation failure to alter run semantics.</summary>
    /// <param name="request">The run whose observer receives the event.</param>
    /// <param name="runEvent">The immutable event to deliver.</param>
    /// <param name="cancellationToken">Bounds delivery; cancellation is isolated like every observer failure.</param>
    /// <returns>An operation completing after delivery succeeds or is safely dropped.</returns>
    private async ValueTask ObserveAsync(
        AgentLoopRunRequest request,
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
    /// Delivers optional run progress to the legacy <see cref="AgentLoopRunRequest.Observer"/>, exactly as
    /// <see cref="ObserveAsync(AgentLoopRunRequest, AgentRunEvent, CancellationToken)"/> does, and additionally
    /// translates a streamed model content fragment into a <see cref="ContentDeltaEvent"/> published through
    /// <see cref="AgentRunServices.Publisher"/> when one is composed.
    /// </summary>
    /// <param name="request">The run whose observer and publisher receive the event.</param>
    /// <param name="services">The compiled per-run collaborator bundle, whose optional <see cref="AgentRunServices.Publisher"/> receives the translated event.</param>
    /// <param name="laneState">The run's tracked lane state, whose <see cref="LoopLaneState.AllocateSequence"/> stamps the published event.</param>
    /// <param name="conversationId">The conversation correlated with the run's history, or <see langword="null"/>.</param>
    /// <param name="runEvent">The immutable legacy event to deliver.</param>
    /// <param name="cancellationToken">Cancels publisher delivery; legacy observer delivery is isolated like every observer failure.</param>
    /// <returns>An operation completing after both deliveries finish.</returns>
    /// <remarks>
    /// Unlike legacy observer delivery, a publisher fault is not isolated here: <see cref="IOutputPublisher.PublishAsync"/>
    /// already isolates a best-effort sink's own failure internally, so a fault that reaches this call means a
    /// required sink failed, which the run must observe rather than silently continue past.
    /// </remarks>
    private async ValueTask ObserveAsync(
        AgentLoopRunRequest request,
        AgentRunServices services,
        LoopLaneState laneState,
        ConversationId? conversationId,
        AgentRunEvent runEvent,
        CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "A validated run request is required for observer delivery.");
        Debug.Assert(services is not null, "The compiled per-run collaborator bundle is required for observer delivery.");
        Debug.Assert(laneState is not null, "The run's tracked lane state is required to stamp a published event's sequence.");
        Debug.Assert(runEvent is not null, "A run event is required for observer delivery.");

        await ObserveAsync(request, runEvent, cancellationToken).ConfigureAwait(false);

        if (services.Publisher is { } publisher
            && runEvent is AgentRunModelResponseEvent { ResponseEvent: ModelPartDelta delta } modelEvent)
        {
            var contentEvent = new ContentDeltaEvent(
                request.AgentId,
                request.SessionId,
                conversationId,
                request.RunId,
                modelEvent.TurnId,
                laneState.AllocateSequence(),
                _timeProvider.GetUtcNow(),
                delta.RequestId,
                delta.PartIndex,
                delta.Delta);
            await publisher.PublishAsync(contentEvent, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Performs a required terminal commit under the loop's own bounded settlement token, independent of the
    /// caller's cancellation, so evidence of how the run stopped can land without the loop ever hanging on it.
    /// </summary>
    /// <param name="runId">The run whose settlement is bounded, for diagnostics.</param>
    /// <param name="services">The compiled per-run collaborator bundle.</param>
    /// <param name="request">The append to commit.</param>
    /// <param name="sessionProfile">The run's immutable session profile.</param>
    /// <param name="allowInterleavedMessages">Whether the append may still commit after a concurrent message landed ahead of it.</param>
    /// <returns>
    /// The append attempt; when <see cref="AgentLoopOptions.SettlementTimeout"/> elapses first, a
    /// <see cref="SessionAppendFailed"/> whose message states that the commit outcome is unknown.
    /// </returns>
    private async ValueTask<AppendAttempt> AppendWithSettlementBoundAsync(
        RunId runId,
        AgentRunServices services,
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
                var appendAttempt = await AppendWithDiagnosticsAsync(services, request, sessionProfile, allowInterleavedMessages, settlement.Token)
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
    private async ValueTask ObserveDetachedAsync(AgentLoopRunRequest request, AgentRunEvent runEvent)
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
    /// <param name="services">The compiled per-run collaborator bundle.</param>
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
        AgentRunServices services,
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
            result = await services.Session.AppendAsync(
                attemptRequest, sessionProfile, cancellationToken).ConfigureAwait(false);
            if (result is not SessionAppendConflict conflict || attempt > _appendConflictRetryLimit)
            {
                break;
            }

            LoopLog.SessionAppendConflictRetried(
                _logger, request.Context.SessionId, conflict.ExpectedVersion, conflict.ActualVersion, attempt);

            var interleavedRead = await ReadInterleavedEntriesAsync(
                services, attemptRequest, sessionProfile, cancellationToken).ConfigureAwait(false);
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
    /// <param name="services">The compiled per-run collaborator bundle.</param>
    /// <param name="attemptRequest">The conflicting append whose first entry names the sequence the loop believed was free.</param>
    /// <param name="sessionProfile">The run's immutable session profile.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The interleaved entries in sequence order with the pinned tip snapshot, or null when the range could not be read consistently.</returns>
    private async ValueTask<(ImmutableArray<SessionEntry> Entries, SessionReadSnapshot Tip)?> ReadInterleavedEntriesAsync(
        AgentRunServices services,
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
            var pageResult = await services.Session.ReadAsync(
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
        AgentLoopRunRequest request,
        AgentRunServices services,
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
            services,
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
                RunOutcomes.SessionOperationFailed(DescribeAppendFailure(appendAttempt.Result)), currentVersion);
        }

        committedMessages.Add(interruptedMessage);
        return TurnOutcome.Settled(outcome, appended.NewVersion);
    }

    /// <summary>
    /// Settles, before the first turn, every tool call a previous run left without a terminal result, so the
    /// branch becomes causally valid again instead of rejecting every later run.
    /// </summary>
    /// <param name="request">The run performing the recovery.</param>
    /// <param name="services">The compiled per-run collaborator bundle.</param>
    /// <param name="sessionContext">The run-scoped session context that writes the settlement.</param>
    /// <param name="runCorrelation">The run's in-run correlation recorded as the entry's writer.</param>
    /// <param name="entries">
    /// The loaded eligible history entries in sequence order. When the branch carries an active compaction
    /// checkpoint these are only the entries from its retained suffix onward, so a call left dangling inside the
    /// covered range is neither seen nor settled: the checkpoint already stands in for that history.
    /// </param>
    /// <param name="messages">The model-facing projection of <paramref name="entries"/>, led by the checkpoint summary when one applies.</param>
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
        AgentLoopRunRequest request,
        AgentRunServices services,
        SessionOperationContext sessionContext,
        InRunOperationCorrelation runCorrelation,
        ImmutableArray<SessionEntry> entries,
        ImmutableArray<AgentMessage> messages,
        MessageCursor cursor,
        ImmutableArray<AgentMessage>.Builder committedMessages,
        CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "A validated run request is required for run-start recovery.");
        Debug.Assert(!entries.IsDefault, "Loaded history is an initialized array.");
        Debug.Assert(!messages.IsDefault, "The projected history is an initialized array.");
        // Each pending call retains its own owning entry rather than a single shared "last assistant message
        // seen" variable: a later assistant message whose own calls were all resolved (possible via
        // imported/branched history or a different writer) must never overwrite the owner of an earlier,
        // still-unresolved call.
        var pendingCalls = new Dictionary<ToolCallId, (ToolCallPart Call, MessageSessionEntry Owner)>();
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
                        _ = pendingCalls.TryAdd(call.CallId, (call, messageEntry));
                        break;
                    case ToolResultPart result when messageEntry.Message is ToolMessage:
                        _ = pendingCalls.Remove(result.CallId);
                        break;
                    default:
                        break;
                }
            }
        }

        if (pendingCalls.Count == 0)
        {
            return (new HistoryView(cursor, messages, []), null);
        }

        // The idempotency key and causal parent are attributed to the earliest still-pending call's owning
        // message, not whichever assistant message the scan happened to visit last.
        var danglingEntry = pendingCalls.Values
            .Select(static pending => pending.Owner)
            .DistinctBy(static owner => owner.Id)
            .OrderBy(static owner => owner.Sequence.Value)
            .First();

        LoopLog.DanglingToolCallsSettled(_logger, request.RunId, pendingCalls.Count);
        var now = _timeProvider.GetUtcNow();
        var resultParts = pendingCalls.Values
            .Select(static pending => pending.Call)
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
                DefaultProjection(ToolTerminalStatus.Interrupted),
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
            services,
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
            return (new HistoryView(cursor, messages, []), RunOutcomes.SessionOperationFailed(
                "A previous run left tool calls without terminal results and the recovery settlement could not be committed: " +
                DescribeAppendFailure(appendAttempt.Result)));
        }

        committedMessages.Add(toolMessage);
        var nextCursor = NextCursor(cursor, appended.NewVersion, appended.CommittedEntries[^1].Sequence);
        return (new HistoryView(nextCursor, [.. messages, .. appendAttempt.InterleavedMessages, toolMessage], []), null);
    }

    /// <summary>
    /// Loads the branch under one pinned snapshot and keeps only the entries a run's model-facing history needs:
    /// everything when the branch carries no active compaction checkpoint, otherwise the newest active
    /// checkpoint's retained suffix and every entry after the checkpoint.
    /// </summary>
    /// <param name="runId">The run loading its history, for diagnostics.</param>
    /// <param name="services">The compiled per-run collaborator bundle.</param>
    /// <param name="sessionContext">The run-scoped session context authorizing the read.</param>
    /// <param name="sessionProfile">The run's immutable session profile.</param>
    /// <param name="branchId">The branch to load.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>
    /// The retained entries in sequence order, the cursor naming the real branch tip, the selected checkpoint when
    /// one applies, and the number of covered entries omitted; or null when the branch could not be read
    /// consistently under one snapshot.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The branch is still read forward from its origin because the session read contract pages forward only.
    /// Entries are buffered as they arrive; when an active <see cref="CompactionSessionEntry"/> is encountered, every
    /// buffered entry whose sequence precedes its <see cref="CompactionManifest.RetainedSuffixStart"/> is discarded.
    /// Peak retention is therefore bounded by the covered range plus the retained suffix, not by the number of
    /// checkpoints in the branch, and a checkpoint appended after the loop's last observation is never applied
    /// retroactively to a turn that already assembled its request.
    /// </para>
    /// <para>
    /// When more than one active checkpoint exists the newest wins. The first-party compactor covers a contiguous
    /// prefix of the branch, so a newer checkpoint's covered range normally includes every older checkpoint; a
    /// newer record that does not cover an older one is logged as a warning and still used, because the loop does
    /// not adjudicate between records the compactor committed.
    /// </para>
    /// </remarks>
    private async Task<LoadedHistory?> LoadHistoryAsync(
        RunId runId,
        AgentRunServices services,
        SessionOperationContext sessionContext,
        SessionProfileSnapshot sessionProfile,
        BranchId branchId,
        CancellationToken cancellationToken)
    {
        Debug.Assert(sessionContext is not null, "A run-scoped session context is required to load history.");
        var loadResult = await services.Session.LoadAsync(sessionContext, sessionProfile, cancellationToken)
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
        CompactionSessionEntry? checkpoint = null;
        var coveredEntryCount = 0;
        var loadedEntryCount = 0;

        while (true)
        {
            var pageResult = await services.Session.ReadAsync(
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

            foreach (var entry in page.Entries)
            {
                loadedEntryCount++;
                if (entry is CompactionSessionEntry { Record: { Status: CompactionRecordStatus.Active, Checkpoint: not null } } activeCheckpoint)
                {
                    if (checkpoint is not null
                        && checkpoint.Sequence.Value > activeCheckpoint.Record.Manifest.CoveredRange.EndInclusive.Value)
                    {
                        LoopLog.OlderCompactionCheckpointNotCovered(
                            _logger,
                            runId,
                            activeCheckpoint.Record.Context.CompactionId,
                            activeCheckpoint.Sequence,
                            activeCheckpoint.Record.Manifest.CoveredRange.EndInclusive,
                            checkpoint.Record.Context.CompactionId,
                            checkpoint.Sequence);
                    }

                    checkpoint = activeCheckpoint;
                    coveredEntryCount += DiscardCoveredEntries(entries, activeCheckpoint.Record.Manifest.RetainedSuffixStart);
                }

                entries.Add(entry);
            }

            cursor = page.ThroughSequence;

            if (!page.HasMore || page.Entries.IsEmpty)
            {
                break;
            }
        }

        Debug.Assert(snapshot is not null, "A successful first page supplies exact snapshot evidence.");
        var reachedBoundary = cursor == snapshot.UpperSequence
            || (loadedEntryCount == 0 && cursor.Value > snapshot.UpperSequence.Value);
        return reachedBoundary
            ? new LoadedHistory(
                entries.ToImmutable(),
                new MessageCursor(
                    sessionContext.AgentId,
                    sessionContext.SessionId,
                    loaded.Descriptor.ConversationId,
                    branchId,
                    snapshot.Version,
                    snapshot.UpperSequence),
                checkpoint,
                coveredEntryCount)
            : null;
    }

    /// <summary>
    /// Drops every buffered entry that precedes a checkpoint's retained suffix, keeping the buffer bounded to the
    /// suffix the checkpoint stands before.
    /// </summary>
    /// <param name="entries">The buffered entries in sequence order; mutated in place.</param>
    /// <param name="retainedSuffixStart">The first sequence the checkpoint retains verbatim.</param>
    /// <returns>The number of entries discarded.</returns>
    private static int DiscardCoveredEntries(ImmutableArray<SessionEntry>.Builder entries, SessionSequence retainedSuffixStart)
    {
        Debug.Assert(entries is not null, "The load owns an initialized entry buffer.");
        var discard = 0;
        while (discard < entries.Count && entries[discard].Sequence.Value < retainedSuffixStart.Value)
        {
            discard++;
        }

        entries.RemoveRange(0, discard);
        return discard;
    }

    /// <summary>
    /// Captures fresh authorization for one run operation and verifies it against the run-start evidence.
    /// </summary>
    /// <param name="request">The run whose baseline evidence the capture must match.</param>
    /// <param name="services">The compiled per-run collaborator bundle.</param>
    /// <param name="correlation">The operation (run or turn) the authorization is captured for.</param>
    /// <param name="cancellationToken">Cancels the capture.</param>
    /// <returns>
    /// The captured authorization, or the typed outcome that settles the run: a <see cref="RunFailed"/> with an
    /// <see cref="AgentErrorCodes.AuthorizationDenied"/> error when the authority could not capture authorization,
    /// and a <see cref="RunFailed"/> with an <see cref="AgentErrorCodes.InvalidState"/> error when the captured
    /// evidence contradicts the run-start evidence.
    /// </returns>
    private async ValueTask<(SecurityAuthorizationContext? Authorization, AgentRunOutcome? Failure)>
        CaptureAuthorizationAsync(
            AgentLoopRunRequest request,
            AgentRunServices services,
            InRunOperationCorrelation correlation,
            CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "A validated run request is required for authorization capture.");
        Debug.Assert(correlation is not null, "A concrete in-run correlation is required for authorization capture.");

        var baseline = request.Authorization;
        var scope = new SecurityAuthorizationScope(request.AgentId, request.SessionId, correlation);
        var result = await services.SecurityProfileSelector.SelectAsync(
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
            return (null, RunOutcomes.AuthorizationUnavailable(result is SecurityAuthorizationCaptureUnavailable unavailable
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
            return (null, RunOutcomes.InvalidState("The captured security profile differs from the run-start evidence."));
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

    /// <summary>
    /// Estimates the tokens the model-facing history occupies from the UTF-16 length of its text-bearing parts.
    /// </summary>
    /// <param name="messages">The history about to be sent.</param>
    /// <returns>An advisory estimate; never used to block a request on its own.</returns>
    private long EstimateTokens(ImmutableArray<AgentMessage> messages)
    {
        long characters = 0;
        foreach (var message in messages)
        {
            foreach (var part in message.Parts)
            {
                characters += part switch
                {
                    TextPart text => text.Text.Length,
                    ToolCallPart call => call.Arguments.ValueKind == System.Text.Json.JsonValueKind.Undefined ? 0 : call.Arguments.GetRawText().Length,
                    ToolResultPart result => result.Content.OfType<TextPart>().Sum(static inner => (long) inner.Text.Length),
                    ReasoningPart { Content.Text: { } reasoning } => reasoning.Length,
                    _ => 0,
                };
            }
        }

        return (long) Math.Ceiling(characters / _estimatedCharactersPerToken);
    }

    /// <summary>
    /// Asks the composed compactor to checkpoint older history and, when it succeeds, reloads the model-facing history
    /// from the newest checkpoint; on any other outcome the run continues with the history it had.
    /// </summary>
    /// <param name="request">The run being driven.</param>
    /// <param name="services">The compiled per-run collaborator bundle.</param>
    /// <param name="compactor">The composed compactor.</param>
    /// <param name="sessionContext">The run-scoped session context used to reload history.</param>
    /// <param name="runCorrelation">The run's correlation, recorded as the compaction's cause.</param>
    /// <param name="authorization">The run's captured authorization.</param>
    /// <param name="history">The history under pressure.</param>
    /// <param name="estimatedTokens">The estimate that crossed the threshold.</param>
    /// <param name="contextWindow">The model's declared context window.</param>
    /// <param name="threshold">The token count at which pressure was declared.</param>
    /// <param name="cancellationToken">Cancels the compaction; cancellation propagates.</param>
    /// <returns>The reloaded history after a successful checkpoint, otherwise <paramref name="history"/>.</returns>
    /// <remarks>
    /// Compaction runs at most once per run and is advisory: a compactor that rejects, fails, conflicts, or finds
    /// nothing to reduce leaves the request as it was, and the provider remains the authority on whether the
    /// request fits. The checkpoint is a durable session entry, so later runs benefit even when this one does not.
    /// </remarks>
    private async Task<HistoryView> CompactUnderPressureAsync(
        AgentLoopRunRequest request,
        AgentRunServices services,
        ICompactor compactor,
        SessionOperationContext sessionContext,
        InRunOperationCorrelation runCorrelation,
        SecurityAuthorizationContext authorization,
        HistoryView history,
        long estimatedTokens,
        long contextWindow,
        double threshold,
        CancellationToken cancellationToken)
    {
        Debug.Assert(history is not null, "Pressure is evaluated over a loaded history.");
        var compactionId = _compactionIds.Create();
        LoopLog.CompactionTriggered(_logger, request.RunId, compactionId, estimatedTokens, contextWindow);
        var now = _timeProvider.GetUtcNow();
        CompactionResult outcome;
        try
        {
            outcome = await compactor.CompactAsync(
                new CompactionRequest(
                    new CompactionOperationContext(
                        compactionId, request.AgentId, request.SessionId, runCorrelation, request.Identity, authorization, request.SessionProfile),
                    request.BranchId,
                    history.SourceCursor.Version,
                    history.SourceCursor.Sequence,
                    new ContextEpoch(0),
                    new CompactionTrigger(
                        CompactionTriggerKind.ContextPressure,
                        $"Estimated {estimatedTokens} tokens exceed {threshold:F0} of a {contextWindow}-token window.",
                        runCorrelation.OperationId),
                    targetInputTokens: (int) Math.Min(int.MaxValue, Math.Max(1, threshold / 2)),
                    minimumReductionRatio: 0.1,
                    minimumRetainedEntries: 1,
                    now,
                    now + request.AttemptTimeout,
                    ExtensionData.Empty),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LoopLog.CompactionFaulted(_logger, request.RunId, compactionId, exception.GetType().FullName ?? exception.GetType().Name);
            return history;
        }

        if (outcome is not CompactionSucceeded)
        {
            LoopLog.CompactionNotApplied(_logger, request.RunId, compactionId, outcome.GetType().Name);
            return history;
        }

        var reloaded = await LoadHistoryAsync(
            request.RunId, services, sessionContext, request.SessionProfile, request.BranchId, cancellationToken).ConfigureAwait(false);
        if (reloaded is not { } loaded)
        {
            LoopLog.CompactionNotApplied(_logger, request.RunId, compactionId, "history_reload_failed");
            return history;
        }

        var messages = ToMessages(loaded.Entries);
        if (loaded.Checkpoint is { } checkpoint)
        {
            messages = messages.Insert(0, CompactionCheckpointProjector.Project(checkpoint, loaded.Cursor));
        }

        LoopLog.CompactionApplied(_logger, request.RunId, compactionId, history.Messages.Length, messages.Length);
        return new HistoryView(loaded.Cursor, messages, []);
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
        AgentLoopRunRequest request, AgentRunOutcome outcome, ImmutableArray<AgentMessage> newMessages, SessionVersion? finalVersion,
        RunUsage usage, ValidatedOutput? output = null) =>
        new(request.AgentId, request.SessionId, request.BranchId, request.RunId, outcome, newMessages, finalVersion, output,
            usage, new RunSettlementCompleted());

    /// <summary>
    /// The result of choosing this run's model: either a terminal outcome
    /// that settles the run before any turn starts, or the chosen descriptor
    /// paired with the adapter that executes it.
    /// </summary>
    private readonly struct ModelResolution
    {
        private ModelResolution(
            AgentRunOutcome? outcome,
            ModelDescriptor? model,
            ILlmModel? adapter,
            ModelRequestId? firstModelRequestId,
            ImmutableArray<CapabilityAdjustment> adjustments)
        {
            Outcome = outcome;
            Model = model;
            Adapter = adapter;
            FirstModelRequestId = firstModelRequestId;
            Adjustments = adjustments;
        }

        /// <summary>Gets the terminal outcome, when no model could be used.</summary>
        public AgentRunOutcome? Outcome { get; }

        /// <summary>Gets the chosen descriptor, when resolution succeeded.</summary>
        public ModelDescriptor? Model { get; }

        /// <summary>Gets the adapter that executes the chosen model, when resolution succeeded.</summary>
        public ILlmModel? Adapter { get; }

        /// <summary>Gets the selection's model request identity, which the first turn's attempt reuses, when resolution succeeded.</summary>
        public ModelRequestId? FirstModelRequestId { get; }

        /// <summary>
        /// Gets every declared capability adjustment the selection made to
        /// reach this model, when resolution succeeded. Empty when the model
        /// supported the request without adjustment.
        /// </summary>
        public ImmutableArray<CapabilityAdjustment> Adjustments { get; }

        /// <summary>Creates a successful resolution.</summary>
        public static ModelResolution Resolved(
            ModelDescriptor model,
            ILlmModel adapter,
            ModelRequestId firstModelRequestId,
            ImmutableArray<CapabilityAdjustment> adjustments) =>
            new(null, model, adapter, firstModelRequestId, adjustments);

        /// <summary>Creates a resolution that settles the run before it starts.</summary>
        public static ModelResolution Failed(AgentRunOutcome outcome) => new(outcome, null, null, null, []);
    }

    /// <summary>
    /// The result of loading a run's history: the entries the model-facing view is built from, the cursor naming
    /// the real branch tip, and the active compaction checkpoint those entries follow, when one applies.
    /// </summary>
    private readonly struct LoadedHistory
    {
        /// <summary>Initializes one loaded history.</summary>
        /// <param name="entries">The retained entries in sequence order.</param>
        /// <param name="cursor">The cursor naming the real branch tip under the pinned read snapshot.</param>
        /// <param name="checkpoint">The newest active checkpoint the entries follow, or null when the branch has none.</param>
        /// <param name="coveredEntryCount">The number of loaded entries omitted because the checkpoint covers them.</param>
        public LoadedHistory(
            ImmutableArray<SessionEntry> entries,
            MessageCursor cursor,
            CompactionSessionEntry? checkpoint,
            int coveredEntryCount)
        {
            Debug.Assert(!entries.IsDefault, "Loaded entries are an initialized, possibly empty, array.");
            Debug.Assert(cursor is not null, "A loaded history always names its exact cursor.");
            Debug.Assert(coveredEntryCount >= 0, "A covered count is never negative.");
            Debug.Assert(checkpoint is not null || coveredEntryCount == 0, "Entries are covered only by a checkpoint.");
            Entries = entries;
            Cursor = cursor;
            Checkpoint = checkpoint;
            CoveredEntryCount = coveredEntryCount;
        }

        /// <summary>Gets the retained entries in sequence order: the whole branch, or the checkpoint's retained suffix and everything after it.</summary>
        public ImmutableArray<SessionEntry> Entries { get; }

        /// <summary>Gets the cursor naming the real branch tip, regardless of how many entries the checkpoint covers.</summary>
        public MessageCursor Cursor { get; }

        /// <summary>Gets the newest active compaction checkpoint whose summary leads the projected history, or null.</summary>
        public CompactionSessionEntry? Checkpoint { get; }

        /// <summary>Gets the number of loaded entries omitted because <see cref="Checkpoint"/> covers them.</summary>
        public int CoveredEntryCount { get; }
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
    /// <summary>Per-run state the turns share: the output validation attempt counter and the run's budget.</summary>
    /// <remarks>
    /// The processor's retry policy is evaluated against the attempt count, so repairs consumed on earlier turns are
    /// not forgotten when a later turn produces another invalid candidate. One instance exists per run and is touched
    /// only by that run's sequential turns.
    /// </remarks>
    private sealed class RunTracking
    {
        private int _attempts;

        /// <summary>Gets or sets the run's budget, or <see langword="null"/> for an unbudgeted run.</summary>
        public RunBudget? Budget { get; set; }

        /// <summary>Allocates the next one-based output validation attempt number.</summary>
        /// <returns>1 for the first validation of the run, then 2, 3, and so on.</returns>
        public int NextAttempt() => ++_attempts;
    }

    private readonly struct TurnOutcome
    {
        private TurnOutcome(AgentRunOutcome? outcome, SessionVersion version, MessageCursor? cursor, ImmutableArray<AgentMessage> newMessages, ValidatedOutput? output)
        {
            Outcome = outcome;
            Version = version;
            Cursor = cursor;
            NewMessages = newMessages;
            Output = output;
        }

        /// <summary>Gets the terminal outcome, when the run settled during this turn.</summary>
        public AgentRunOutcome? Outcome { get; }

        /// <summary>Gets the session version after this turn's commits.</summary>
        public SessionVersion Version { get; }

        /// <summary>Gets the exact history cursor after a continuing turn.</summary>
        public MessageCursor? Cursor { get; }

        /// <summary>Gets the messages newly visible to the next turn's history, when continuing.</summary>
        public ImmutableArray<AgentMessage> NewMessages { get; }

        /// <summary>Gets the validated structured output accepted for a <see cref="RunSucceeded"/> settlement.</summary>
        public ValidatedOutput? Output { get; }

        /// <summary>Creates a settled outcome.</summary>
        public static TurnOutcome Settled(AgentRunOutcome outcome, SessionVersion version, ValidatedOutput? output = null) => new(outcome, version, null, [], output);

        /// <summary>Creates a continuation outcome.</summary>
        public static TurnOutcome Continue(MessageCursor cursor, ImmutableArray<AgentMessage> newMessages)
        {
            ArgumentNullException.ThrowIfNull(cursor);
            return new(null, cursor.Version, cursor, newMessages, null);
        }
    }
}
