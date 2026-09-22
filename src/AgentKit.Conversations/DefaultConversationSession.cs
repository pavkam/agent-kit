// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>
/// The default <see cref="IConversationSession"/>: composes <c>ISessionCoordinator</c>,
/// <c>ISecurityProfileSelector</c>, and the <see cref="AgentEngine"/> facade into one durable, authorized conversational turn.
/// </summary>
/// <remarks>
/// This class lazily creates its underlying session on the first <see cref="SendAsync(string, CancellationToken)"/> call and reuses that
/// same session and active branch for every later call. The binding is announced once to a live observer, as the
/// first event of the first observed turn, and is readable at any time through <see cref="SessionId"/> and
/// <see cref="BranchId"/>. Calls are serialized with an internal lock, so this
/// type is safe to share as a singleton for one conversation; a host that needs several independent
/// conversations against the same agent composes one instance per conversation instead.
/// </remarks>
public sealed class DefaultConversationSession: IConversationSession, IDisposable
{
    private readonly ISessionCoordinator _sessionCoordinator;
    private readonly ISecurityProfileSelector _securityProfileSelector;
    private readonly IConversationTurnExecutor _turnExecutor;
    private readonly IIdentifierGenerator<OperationId> _operationIds;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DefaultConversationSession> _logger;
    private readonly SemaphoreSlim _turnLock = new(1, 1);

    private readonly AgentId _agentId;
    private readonly ExecutionIdentity _identity;
    private readonly SecurityProfileKey _securityProfileKey;
    private readonly AgentDefinitionRevision _agentDefinitionRevision;
    private readonly ConfigurationVersion _configurationVersion;
    private readonly SessionProfileSnapshot _sessionProfile;
    private readonly ImmutableDictionary<ToolId, ConversationToolPresentationBinding> _toolPresentationBindings;
    private readonly IToolPresenter? _toolPresenter;
    private readonly OutputDefinition? _output;
    private readonly int _maxTurns;
    private readonly TimeSpan _attemptTimeout;

    private SessionId? _sessionId;
    private BranchId _branchId;
    private volatile ConversationSessionBoundEvent? _binding;
    private bool _bindingAnnounced;
    private bool _disposed;

    /// <inheritdoc/>
    /// <remarks>
    /// Backed by an immutable snapshot published after the binding fields are written under the turn lock, so a
    /// reader on another thread sees either no binding or a complete one.
    /// </remarks>
    public SessionId? SessionId => _binding?.SessionId;

    /// <inheritdoc/>
    public BranchId? BranchId => _binding?.BranchId;

    /// <inheritdoc/>
    public async ValueTask<ConversationHistoryReadResult> ReadHistoryAsync(
        SessionSequence afterSequence,
        int maximumEntries,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEntries);
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _turnLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_sessionId is not { } sessionId)
            {
                return new ConversationHistoryUnavailable(
                    "Open an existing session or send a message before reading conversation history.");
            }

            try
            {
                var correlation = new BeforeRunOperationCorrelation(_operationIds.Create(), null);
                var authorization = await CaptureAuthorizationAsync(
                    sessionId,
                    correlation,
                    cancellationToken).ConfigureAwait(false);
                var request = new SessionReadRequest(
                    new SessionOperationContext(
                        _agentId,
                        sessionId,
                        null,
                        correlation,
                        _identity,
                        authorization),
                    _branchId,
                    afterSequence,
                    maximumEntries);
                var result = await _sessionCoordinator.ReadAsync(
                    request,
                    _sessionProfile,
                    cancellationToken).ConfigureAwait(false);
                return ProjectHistoryRead(result, request);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var errorType = exception.GetType().FullName ?? exception.GetType().Name;
                ConversationLog.HistoryReadFaulted(_logger, _agentId, errorType);
                return new ConversationHistoryUnavailable("Conversation history is temporarily unavailable.");
            }
        }
        finally
        {
            _ = _turnLock.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ConversationSessionOpenResult> OpenAsync(
        SessionId sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _turnLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_sessionId is not null)
            {
                return new ConversationSessionOpenRejected(
                    "This conversation is already bound to a session and cannot be rebound.");
            }

            var correlation = new BeforeRunOperationCorrelation(_operationIds.Create(), null);
            var authorization = await CaptureAuthorizationAsync(sessionId, correlation, cancellationToken).ConfigureAwait(false);
            var loaded = await _sessionCoordinator.LoadAsync(
                new SessionOperationContext(_agentId, sessionId, null, correlation, _identity, authorization),
                _sessionProfile,
                cancellationToken).ConfigureAwait(false);
            if (loaded is not SessionLoaded session)
            {
                return new ConversationSessionOpenRejected("The requested session is unavailable or not visible.");
            }

            var descriptor = session.Descriptor;
            if (descriptor.Address.AgentId != _agentId
                || descriptor.TenantId != _identity.TenantId
                || descriptor.OwnerId != _identity.PrincipalId
                || descriptor.State != SessionLifecycleState.Active)
            {
                return new ConversationSessionOpenRejected("The requested session is unavailable or not visible.");
            }

            _sessionId = sessionId;
            _branchId = descriptor.ActiveBranchId;
            _binding = new ConversationSessionBoundEvent(sessionId, _branchId);
            return new ConversationSessionOpened(sessionId, _branchId);
        }
        finally
        {
            _ = _turnLock.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ConversationSessionListResult> ListAsync(
        SessionId? afterSessionId,
        int maximumResults,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResults);
        ObjectDisposedException.ThrowIf(_disposed, this);
        var correlation = new BeforeRunOperationCorrelation(_operationIds.Create(), null);
        var authorization = await CaptureAuthorizationAsync(null, correlation, cancellationToken).ConfigureAwait(false);
        var result = await _sessionCoordinator.ListAsync(
            new SessionDirectoryListRequest(_agentId, _identity, authorization, afterSessionId, maximumResults),
            cancellationToken).ConfigureAwait(false);
        return result switch
        {
            SessionDirectoryPage page => new ConversationSessionPage(
                [.. page.Locations.Select(static location => new ConversationSessionSummary(
                    location.Address.SessionId, location.StoreKey, location.RecordedAt))],
                page.NextCursor),
            SessionDirectoryListUnavailable unavailable => new ConversationSessionListUnavailable(unavailable.SafeMessage),
            _ => new ConversationSessionListUnavailable("Session discovery returned an unsupported outcome."),
        };
    }

    /// <summary>Initializes a conversation session from its validated collaborators and options.</summary>
    /// <param name="sessionCoordinator">Creates, loads, and appends to the underlying session.</param>
    /// <param name="securityProfileSelector">Captures authorization for session admission and each run.</param>
    /// <param name="turnExecutor">Runs each turn through the composed <see cref="AgentEngine"/> facade.</param>
    /// <param name="operationIds">Generates each session operation identity used for reads and opens.</param>
    /// <param name="timeProvider">The clock used for turn diagnostics.</param>
    /// <param name="options">The validated agent composition this session drives turns for.</param>
    /// <param name="logger">The optional logger that receives safe turn diagnostics; a null logger is used when omitted.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null, or a required option is unset.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="ConversationSessionOptions.AgentId"/> or <see cref="ConversationSessionOptions.SecurityProfileKey"/>
    /// or <see cref="ConversationSessionOptions.ConfigurationVersion"/> is default, or
    /// <see cref="ConversationSessionOptions.MaxTurns"/> or <see cref="ConversationSessionOptions.AttemptTimeout"/>
    /// is not positive.
    /// </exception>
    public DefaultConversationSession(
        ISessionCoordinator sessionCoordinator,
        ISecurityProfileSelector securityProfileSelector,
        IConversationTurnExecutor turnExecutor,
        IIdentifierGenerator<OperationId> operationIds,
        TimeProvider timeProvider,
        IOptions<ConversationSessionOptions> options,
        ILogger<DefaultConversationSession>? logger = null)
        : this(
            sessionCoordinator,
            securityProfileSelector,
            turnExecutor,
            operationIds,
            timeProvider,
            options,
            logger,
            toolPresenter: null)
    {
    }

    /// <summary>Initializes a conversation session with optional bounded tool presentation.</summary>
    /// <param name="sessionCoordinator">Creates, loads, and appends to the underlying session.</param>
    /// <param name="securityProfileSelector">Captures authorization for session admission and each run.</param>
    /// <param name="turnExecutor">Runs each turn through the composed <see cref="AgentEngine"/> facade.</param>
    /// <param name="operationIds">Generates each session operation identity used for reads and opens.</param>
    /// <param name="timeProvider">The clock used for turn diagnostics.</param>
    /// <param name="options">The validated agent composition this session drives turns for.</param>
    /// <param name="logger">The optional content-safe diagnostics logger.</param>
    /// <param name="toolPresenter">The optional observational presenter used for bounded live tool rendering.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null, or a required option is unset.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity, limit, or timeout option is invalid.</exception>
    public DefaultConversationSession(
        ISessionCoordinator sessionCoordinator,
        ISecurityProfileSelector securityProfileSelector,
        IConversationTurnExecutor turnExecutor,
        IIdentifierGenerator<OperationId> operationIds,
        TimeProvider timeProvider,
        IOptions<ConversationSessionOptions> options,
        ILogger<DefaultConversationSession>? logger,
        IToolPresenter? toolPresenter)
    {
        ArgumentNullException.ThrowIfNull(sessionCoordinator);
        ArgumentNullException.ThrowIfNull(securityProfileSelector);
        ArgumentNullException.ThrowIfNull(turnExecutor);
        ArgumentNullException.ThrowIfNull(operationIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        var optionValues = options.Value;
        ArgumentNullException.ThrowIfNull(optionValues);
        ArgumentOutOfRangeException.ThrowIfEqual(optionValues.AgentId, default, nameof(options));
        ArgumentNullException.ThrowIfNull(optionValues.Identity, nameof(options));
        ArgumentOutOfRangeException.ThrowIfEqual(optionValues.SecurityProfileKey, default, nameof(options));
        ArgumentOutOfRangeException.ThrowIfEqual(optionValues.ConfigurationVersion, default, nameof(options));
        ArgumentNullException.ThrowIfNull(optionValues.SessionProfile, nameof(options));
        ArgumentNullException.ThrowIfNull(optionValues.ModelSelectionPolicy, nameof(options));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(optionValues.MaxTurns, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(optionValues.AttemptTimeout, TimeSpan.Zero, nameof(options));
        ArgumentException.ThrowIfNotEqual(optionValues.Agent is null, optionValues.Configuration is null, nameof(options));
        if (optionValues is { Agent: { } agent, Configuration: { } configuration })
        {
            ArgumentException.ThrowIfNotEqual(agent.Id, optionValues.AgentId, nameof(options));
            ArgumentException.ThrowIfNotEqual(agent.Revision, optionValues.AgentDefinitionRevision, nameof(options));
            ArgumentException.ThrowIfNotEqual(agent.SecurityProfile, optionValues.SecurityProfileKey, nameof(options));
            ArgumentException.ThrowIfNotEqual(agent.SessionProfile, optionValues.SessionProfile.Reference.Key, nameof(options));
            ArgumentException.ThrowIfNotEqual(configuration.Version, optionValues.ConfigurationVersion, nameof(options));
            ArgumentException.ThrowIfNotEqual(configuration.Fingerprint, optionValues.SessionProfile.ConfigurationFingerprint, nameof(options));
        }

        var toolPresentationBindings = CaptureToolPresentationBindings(optionValues);

        _sessionCoordinator = sessionCoordinator;
        _securityProfileSelector = securityProfileSelector;
        _turnExecutor = turnExecutor;
        _operationIds = operationIds;
        _timeProvider = timeProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultConversationSession>.Instance;
        _toolPresenter = toolPresenter;

        _agentId = optionValues.AgentId;
        _identity = optionValues.Identity;
        _securityProfileKey = optionValues.SecurityProfileKey;
        _agentDefinitionRevision = optionValues.AgentDefinitionRevision;
        _configurationVersion = optionValues.ConfigurationVersion;
        _sessionProfile = optionValues.SessionProfile;
        _toolPresentationBindings = toolPresentationBindings;
        _output = optionValues.Output;
        ArgumentException.ThrowIfContainsNull([.. optionValues.BudgetLimits], nameof(options));
        _maxTurns = optionValues.MaxTurns;
        _attemptTimeout = optionValues.AttemptTimeout;
    }

    /// <inheritdoc/>
    public Task<ConversationTurnResult> SendAsync(string userText, CancellationToken cancellationToken = default) =>
        SendObservedAsync(userText, observer: null, cancellationToken);

    /// <inheritdoc/>
    public Task<ConversationTurnResult> SendAsync(
        string userText,
        IConversationEventObserver observer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observer);
        return SendObservedAsync(userText, observer, cancellationToken);
    }

    private async Task<ConversationTurnResult> SendObservedAsync(
        string userText,
        IConversationEventObserver? observer,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userText);
        ObjectDisposedException.ThrowIf(_disposed, this);

        using var activity = AgentKitDiagnostics.Activities.StartActivity(
            AgentKitActivityNames.ConversationTurn,
            ActivityKind.Internal,
            parentContext: Activity.Current?.Context ?? default,
            tags: new ActivityTagsCollection
            {
                { AgentKitTagNames.GenAiOperationName, AgentKitActivityNames.ConversationTurn },
                { AgentKitTagNames.AgentId, _agentId.ToString() },
            });
        var startedAt = _timeProvider.GetTimestamp();
        ConversationLog.TurnStarted(_logger, _agentId);

        var lockAcquired = false;
        try
        {
            await _turnLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            lockAcquired = true;
            var (result, outcome) = await SendCoreAsync(userText, observer, cancellationToken).ConfigureAwait(false);
            await ObserveAsync(observer, new ConversationTurnCompletedEvent(result.Succeeded, outcome), cancellationToken)
                .ConfigureAwait(false);
            activity.SetSuccessful(outcome);
            ConversationMetrics.RecordTurn(outcome, _timeProvider.GetElapsedTime(startedAt));
            ConversationLog.TurnSettled(_logger, _agentId, result.Events.Length);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await ObserveAsync(
                observer,
                new ConversationTurnCompletedEvent(false, "cancelled"),
                CancellationToken.None).ConfigureAwait(false);
            activity.SetFailed("cancelled", nameof(OperationCanceledException));
            ConversationMetrics.RecordTurn("cancelled", _timeProvider.GetElapsedTime(startedAt));
            ConversationLog.TurnCancelled(_logger, _agentId);
            throw;
        }
        catch (Exception exception)
        {
            await ObserveAsync(
                observer,
                new ConversationTurnCompletedEvent(false, "faulted"),
                CancellationToken.None).ConfigureAwait(false);
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            activity.SetFailed("faulted", errorType);
            ConversationMetrics.RecordTurn("faulted", _timeProvider.GetElapsedTime(startedAt));
            ConversationLog.TurnFaulted(_logger, _agentId, errorType);
            throw;
        }
        finally
        {
            if (lockAcquired)
            {
                _ = _turnLock.Release();
            }
        }
    }

    private async Task<(ConversationTurnResult Result, string Outcome)> SendCoreAsync(
        string userText,
        IConversationEventObserver? observer,
        CancellationToken cancellationToken)
    {
        await EnsureBoundSessionAsync(cancellationToken).ConfigureAwait(false);

        if (observer is not null && _binding is { } binding && !_bindingAnnounced)
        {
            _bindingAnnounced = true;
            await ObserveAsync(observer, binding, cancellationToken).ConfigureAwait(false);
        }

        var turnRequest = new ConversationTurnRunRequest(
            _agentId,
            _identity,
            _sessionId,
            userText,
            _maxTurns,
            _attemptTimeout,
            observer is null
                ? null
                : new ConversationRunObserver(
                    observer,
                    DescribeToolResult,
                    _toolPresenter,
                    _toolPresentationBindings));

        if (observer is not null)
        {
            AgentLoopResult loopResult;
            try
            {
                loopResult = await _turnExecutor.SendObservedAsync(turnRequest, cancellationToken).ConfigureAwait(false);
            }
            catch (AgentAdmissionRejectedException exception)
            {
                ConversationLog.TurnAdmissionFailed(_logger, _agentId);
                return (
                    Finish(false, [new ConversationAssistantTextEvent(exception.Rejection.Reason)], loopResult: null),
                    "admission_failed");
            }

            PublishBinding(loopResult.SessionId, loopResult.BranchId);
            return MapLoopResult(loopResult, observer, cancellationToken);
        }

        try
        {
            if (_output is null)
            {
                var result = await _turnExecutor.RunAsync<string>(turnRequest, cancellationToken).ConfigureAwait(false);
                return await MapTypedResult(result, observer, cancellationToken).ConfigureAwait(false);
            }

            var structured = await _turnExecutor.RunAsync<ValidatedOutput>(turnRequest, cancellationToken).ConfigureAwait(false);
            return await MapTypedResult(structured, observer, cancellationToken).ConfigureAwait(false);
        }
        catch (AgentAdmissionRejectedException exception)
        {
            ConversationLog.TurnAdmissionFailed(_logger, _agentId);
            return (
                Finish(false, [new ConversationAssistantTextEvent(exception.Rejection.Reason)], loopResult: null),
                "admission_failed");
        }

        (ConversationTurnResult Result, string Outcome) MapLoopResult(
            AgentLoopResult loopResult,
            IConversationEventObserver? liveObserver,
            CancellationToken token)
        {
            var events = ProjectEvents(loopResult);
            if (loopResult.Outcome is RunSucceeded)
            {
                if (loopResult.Output is { } output)
                {
                    var outputEvent = new ConversationOutputEvent(output);
                    return (Finish(true, events.Add(outputEvent), loopResult) with { Output = output }, "settled");
                }

                return (Finish(true, events, loopResult), "settled");
            }

            ConversationLog.TurnRunNotCompleted(_logger, _agentId, loopResult.Outcome.GetType().Name);
            var description = DescribeIncompleteOutcome(loopResult.Outcome);
            return (
                Finish(false, [.. events, new ConversationAssistantTextEvent(description)], loopResult),
                RunOutcomeKind(loopResult.Outcome));
        }

        Task<(ConversationTurnResult Result, string Outcome)> MapTypedResult<TOutput>(
            AgentRunResult<TOutput> result,
            IConversationEventObserver? liveObserver,
            CancellationToken token)
        {
            if (result is AgentRunRejected<TOutput> rejected)
            {
                ConversationLog.TurnAdmissionFailed(_logger, _agentId);
                return Task.FromResult((
                    Finish(false, [new ConversationAssistantTextEvent(rejected.Failure.SafeMessage)], loopResult: null),
                    "admission_failed"));
            }

            var finished = (AgentRunFinished<TOutput>) result;
            PublishBinding(finished.SessionId, finished.PreviousCursor.BranchId);
            var loopShaped = new AgentLoopResult(
                finished.AgentId,
                finished.SessionId,
                finished.PreviousCursor.BranchId,
                finished.RunId,
                finished.Outcome,
                finished.NewMessages,
                finished.PreviousCursor.Version,
                finished.Output is ValidatedOutput validated ? validated : null,
                finished.Usage,
                finished.Settlement);
            return Task.FromResult(MapLoopResult(loopShaped, liveObserver, token));
        }

        ConversationTurnResult Finish(
            bool succeeded,
            ImmutableArray<ConversationEvent> turnEvents,
            AgentLoopResult? loopResult) =>
            new(succeeded, turnEvents)
            {
                SessionId = _sessionId ?? loopResult?.SessionId,
                RunId = loopResult?.RunId,
            };
    }

    private void PublishBinding(SessionId sessionId, BranchId branchId)
    {
        _sessionId = sessionId;
        _branchId = branchId;
        _binding = new ConversationSessionBoundEvent(sessionId, branchId);
    }

    private async Task EnsureBoundSessionAsync(CancellationToken cancellationToken)
    {
        if (_sessionId is not null)
        {
            return;
        }

        ConversationLog.SessionCreating(_logger, _agentId);
        var sessionId = await _turnExecutor.EnsureSessionAsync(
            _agentId,
            _identity,
            _sessionId,
            cancellationToken).ConfigureAwait(false);
        var correlation = new BeforeRunOperationCorrelation(_operationIds.Create(), null);
        var authorization = await CaptureAuthorizationAsync(sessionId, correlation, cancellationToken).ConfigureAwait(false);
        var loaded = await _sessionCoordinator.LoadAsync(
            new SessionOperationContext(_agentId, sessionId, null, correlation, _identity, authorization),
            _sessionProfile,
            cancellationToken).ConfigureAwait(false);
        if (loaded is not SessionLoaded session)
        {
            throw new InvalidOperationException($"Could not load the underlying session: {loaded}");
        }

        PublishBinding(sessionId, session.Descriptor.ActiveBranchId);
    }

    /// <summary>Maps a non-completed run outcome onto a short, bounded metric/log outcome token.</summary>
    /// <param name="outcome">The run's terminal outcome, which is not <see cref="RunSucceeded"/>.</param>
    /// <returns>A stable, low-cardinality token distinguishing why the run did not complete.</returns>
    private static string RunOutcomeKind(AgentRunOutcome outcome) => outcome switch
    {
        RunIdle => "idle",
        RunDeferred => "deferred",
        RunCancelled => "cancelled",
        RunLimitReached => "budget_exhausted",
        RunPolicyHalted { Reason.Error.Code: var code } when code == AgentErrorCodes.RequestLimit => "turn_limit",
        RunPolicyHalted => "output_rejected",
        RunFailed => "failed",
        _ => "run_not_completed",
    };

    /// <summary>Projects one coordinator page without exposing non-message records or malformed stored content.</summary>
    /// <param name="result">The terminal coordinator read outcome.</param>
    /// <param name="request">The exact bounded read request used to produce <paramref name="result"/>.</param>
    /// <returns>A stable conversation page or a content-safe unavailable result.</returns>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    private static ConversationHistoryReadResult ProjectHistoryRead(
        SessionPageResult result,
        SessionReadRequest request)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(request);

        if (result is SessionReadFailed failed)
        {
            return new ConversationHistoryUnavailable(failed.SafeMessage);
        }

        if (result is not SessionPage page)
        {
            return new ConversationHistoryUnavailable("Conversation history is unavailable or not visible.");
        }

        if (!IsValidPage(page, request))
        {
            return new ConversationHistoryUnavailable("Conversation history returned invalid pagination data.");
        }

        var messages = ImmutableArray.CreateBuilder<AgentMessage>();
        foreach (var entry in page.Entries)
        {
            if (entry is not MessageSessionEntry messageEntry)
            {
                continue;
            }

            var message = messageEntry.Message;
            if (message.AgentId != request.Context.AgentId
                || message.SessionId != request.Context.SessionId)
            {
                return new ConversationHistoryUnavailable("Conversation history contains invalid message ownership data.");
            }

            messages.Add(message);
        }

        return new ConversationHistoryPage(messages.ToImmutable(), page.ThroughSequence, !page.HasMore);
    }

    /// <summary>Checks bounded forward-pagination evidence before exposing any stored message.</summary>
    /// <param name="page">The coordinator page to validate.</param>
    /// <param name="request">The exact request whose bounds and cursor constrain <paramref name="page"/>.</param>
    /// <returns><see langword="true"/> when the page can safely advance the caller's cursor.</returns>
    private static bool IsValidPage(SessionPage page, SessionReadRequest request)
    {
        Debug.Assert(page is not null, "A coordinator page is required.");
        Debug.Assert(request is not null, "The exact read request is required.");

        if (page.Entries.Length > request.PageSize)
        {
            return false;
        }

        if (page.Entries.IsEmpty)
        {
            return page.ThroughSequence == request.FromSequenceExclusive && !page.HasMore;
        }

        var previous = request.FromSequenceExclusive;
        var address = request.Context.ToAddress();
        foreach (var entry in page.Entries)
        {
            if (entry.Address != address || entry.Sequence.Value <= previous.Value)
            {
                return false;
            }

            previous = entry.Sequence;
        }

        return page.ThroughSequence == previous;
    }

    /// <summary>Validates and freezes the optional descriptor evidence against the exact advertised definitions.</summary>
    /// <param name="options">The already non-null conversation options.</param>
    /// <returns>An immutable map keyed by canonical tool identity.</returns>
    /// <exception cref="ArgumentException">
    /// A binding is absent from the advertised tools, duplicates an identity or alias, or otherwise disagrees with
    /// the advertised definition.
    /// </exception>
    private static ImmutableDictionary<ToolId, ConversationToolPresentationBinding> CaptureToolPresentationBindings(
        ConversationSessionOptions options)
    {
        Debug.Assert(options is not null, "Validated conversation options are required.");
        var bindings = ImmutableDictionary.CreateBuilder<ToolId, ConversationToolPresentationBinding>();
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in options.ToolPresentationBindings)
        {
            ArgumentNullException.ThrowIfNull(binding, nameof(options));
            if (!options.Tools.Contains(binding.AdvertisedTool))
            {
                throw new ArgumentException(
                    "Every tool presentation binding must reference an equal advertised tool definition.",
                    nameof(options));
            }

            if (!bindings.TryAdd(binding.Descriptor.Id, binding)
                || !aliases.Add(binding.AdvertisedTool.Name))
            {
                throw new ArgumentException(
                    "Tool presentation bindings must have unique canonical identities and advertised aliases.",
                    nameof(options));
            }
        }

        return bindings.ToImmutable();
    }

    private async Task<SecurityAuthorizationContext> CaptureAuthorizationAsync(
        SessionId? sessionId,
        OperationCorrelation correlation,
        CancellationToken cancellationToken)
    {
        var result = await _securityProfileSelector.SelectAsync(
            new SecurityAuthorizationCaptureRequest(
                new SecurityAuthorizationScope(_agentId, sessionId, correlation),
                _securityProfileKey,
                _agentDefinitionRevision,
                _configurationVersion,
                _identity),
            cancellationToken).ConfigureAwait(false);
        return result is SecurityAuthorizationCaptured captured
            ? captured.Authorization
            : throw new InvalidOperationException($"Could not capture authorization: {result}");
    }

    private static ImmutableArray<ConversationEvent> ProjectEvents(AgentLoopResult result)
    {
        var events = ImmutableArray.CreateBuilder<ConversationEvent>();
        foreach (var message in result.NewMessages)
        {
            switch (message)
            {
                case AssistantMessage assistant:
                    foreach (var part in assistant.Parts)
                    {
                        switch (part)
                        {
                            case TextPart text when !string.IsNullOrWhiteSpace(text.Text):
                                events.Add(new ConversationAssistantTextEvent(text.Text));
                                break;
                            case ToolCallPart call:
                                events.Add(new ConversationToolCallEvent(
                                    call.CallId, call.Tool.ProviderAlias.Value, call.Arguments.GetRawText()));
                                break;
                            case ReasoningPart { Content.Text: { } reasoningText }:
                                events.Add(new ConversationReasoningEvent(reasoningText));
                                break;
                            default:
                                break;
                        }
                    }

                    if (assistant.Response.Usage.ReportState != ModelUsageReportState.NotReported)
                    {
                        events.Add(new ConversationUsageEvent(assistant.Response.Usage));
                    }

                    break;
                case ToolMessage toolMessage:
                    foreach (var part in toolMessage.Parts.OfType<ToolResultPart>())
                    {
                        events.Add(new ConversationToolResultEvent(
                            part.CallId,
                            part.Tool.ProviderAlias.Value,
                            part.Outcome.Kind == ToolCallOutcomeKind.Success,
                            DescribeToolResult(part)));
                    }

                    break;
                default:
                    break;
            }
        }

        return events.ToImmutable();
    }

    private static string DescribeToolResult(ToolResultPart result)
    {
        Debug.Assert(result is not null, "A validated tool-result projection is required.");
        var content = string.Join(" ", result.Content.OfType<TextPart>().Select(static part => part.Text));
        if (result.Outcome.Kind == ToolCallOutcomeKind.Success)
        {
            return content;
        }

        var reason = result.Outcome.FailureReason ?? "denied";
        return string.IsNullOrWhiteSpace(content) ? reason : $"{reason}\n{content}";
    }

    private static async ValueTask ObserveAsync(
        IConversationEventObserver? observer,
        ConversationEvent conversationEvent,
        CancellationToken cancellationToken)
    {
        Debug.Assert(conversationEvent is not null, "A conversation event is required for observer delivery.");
        if (observer is null)
        {
            return;
        }

        try
        {
            await observer.OnEventAsync(conversationEvent, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Live presentation is best effort and cannot change durable conversation semantics.
        }
    }

    /// <summary>Describes a run outcome other than <see cref="RunSucceeded"/> in safe, non-sensitive text.</summary>
    /// <param name="outcome">The non-default terminal outcome to describe.</param>
    /// <returns>A human-readable sentence explaining why the run did not reach a final assistant message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="outcome"/> is null.</exception>
    private static string DescribeIncompleteOutcome(AgentRunOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        // Only each failure's bounded safe message is user-visible. AgentError.Diagnostics may carry provider or
        // transport detail and is for logs and diagnostics only.
        return outcome switch
        {
            RunIdle =>
                "The run ended idle with no pending work and no final assistant message.",
            RunDeferred =>
                "The run ended with work handed off to durable external ownership.",
            RunCancelled cancelled =>
                $"The run stopped: {cancelled.Reason.Error.SafeMessage}",
            RunLimitReached limitReached =>
                $"The run stopped: the {limitReached.Limit.Limit.Dimension.Value} budget is exhausted ({limitReached.Limit.Limit.SafeMessage}).",
            RunPolicyHalted { Reason.Error.Code: var code } policyHalted when code == AgentErrorCodes.RequestLimit =>
                $"The run stopped: {policyHalted.Reason.Error.SafeMessage}",
            RunPolicyHalted policyHalted =>
                $"The run stopped: its output was rejected ({policyHalted.Reason.Error.SafeMessage}).",
            RunFailed failed =>
                $"The run stopped: {failed.Failure.Error.SafeMessage}",
            _ => $"The run ended without a final assistant message (outcome: {outcome.GetType().Name}).",
        };
    }

    /// <inheritdoc/>
    public ValueTask<ToolPresentation?> PresentToolAsync(
        ContentPart part,
        CancellationToken cancellationToken = default) =>
        ConversationToolPresentationProjector.PresentAsync(
            part,
            _toolPresenter,
            _toolPresentationBindings,
            cancellationToken);

    /// <summary>Marks this session disposed so no further turns are admitted.</summary>
    /// <remarks>
    /// Disposal does not touch the underlying durable session; it remains exactly as last committed. The
    /// turn-serialization semaphore is intentionally not disposed: a turn already in flight must still be
    /// able to release it and hand its committed result back to the caller, and a
    /// <see cref="SemaphoreSlim"/> whose wait handle was never requested holds no unmanaged resources.
    /// </remarks>
    public void Dispose() => _disposed = true;
}
