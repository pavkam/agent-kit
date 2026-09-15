// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>
/// The default <see cref="IConversationSession"/>: composes <c>ISessionCoordinator</c>,
/// <c>ISecurityProfileSelector</c>, and <c>IAgentLoop</c> into one durable, authorized conversational turn.
/// </summary>
/// <remarks>
/// This class lazily creates its underlying session on the first <see cref="SendAsync(string, CancellationToken)"/> call and reuses that
/// same session and active branch for every later call. Calls are serialized with an internal lock, so this
/// type is safe to share as a singleton for one conversation; a host that needs several independent
/// conversations against the same agent composes one instance per conversation instead.
/// </remarks>
public sealed class DefaultConversationSession: IConversationSession, IDisposable
{
    private readonly ISessionCoordinator _sessionCoordinator;
    private readonly ISecurityProfileSelector _securityProfileSelector;
    private readonly IAgentLoop _agentLoop;
    private readonly IIdentifierGenerator<RunId> _runIds;
    private readonly IIdentifierGenerator<OperationId> _operationIds;
    private readonly IIdentifierGenerator<MessageId> _messageIds;
    private readonly IIdentifierGenerator<SessionEntryId> _sessionEntryIds;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DefaultConversationSession> _logger;
    private readonly SemaphoreSlim _turnLock = new(1, 1);

    private readonly AgentDefinition? _agent;
    private readonly EffectiveConfigurationSnapshot? _configuration;
    private readonly AgentId _agentId;
    private readonly ExecutionIdentity _identity;
    private readonly SecurityProfileKey _securityProfileKey;
    private readonly AgentDefinitionRevision _agentDefinitionRevision;
    private readonly ConfigurationVersion _configurationVersion;
    private readonly SessionProfileSnapshot _sessionProfile;
    private readonly ModelSelectionPolicy _modelSelectionPolicy;
    private readonly ModelRequirements _modelRequirements;
    private readonly ImmutableArray<AgentMessage> _instructions;
    private readonly ImmutableArray<LlmToolDefinition> _tools;
    private readonly ImmutableDictionary<ToolId, ConversationToolPresentationBinding> _toolPresentationBindings;
    private readonly IToolPresenter? _toolPresenter;
    private readonly LlmToolChoice _toolChoice;
    private readonly LlmRequestSettings _requestSettings;
    private readonly int _maxTurns;
    private readonly TimeSpan _attemptTimeout;

    private SessionId? _sessionId;
    private BranchId _branchId;
    private bool _disposed;

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
    /// <param name="agentLoop">Drives the multi-turn tool-calling run.</param>
    /// <param name="runIds">Generates each turn's run identity.</param>
    /// <param name="operationIds">Generates each turn's operation identity.</param>
    /// <param name="messageIds">Generates each committed message's identity.</param>
    /// <param name="sessionEntryIds">Generates each committed session-entry identity.</param>
    /// <param name="timeProvider">The clock used to timestamp committed messages and entries.</param>
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
        IAgentLoop agentLoop,
        IIdentifierGenerator<RunId> runIds,
        IIdentifierGenerator<OperationId> operationIds,
        IIdentifierGenerator<MessageId> messageIds,
        IIdentifierGenerator<SessionEntryId> sessionEntryIds,
        TimeProvider timeProvider,
        IOptions<ConversationSessionOptions> options,
        ILogger<DefaultConversationSession>? logger = null)
        : this(
            sessionCoordinator,
            securityProfileSelector,
            agentLoop,
            runIds,
            operationIds,
            messageIds,
            sessionEntryIds,
            timeProvider,
            options,
            logger,
            toolPresenter: null)
    {
    }

    /// <summary>Initializes a conversation session with optional bounded tool presentation.</summary>
    /// <param name="sessionCoordinator">Creates, loads, and appends to the underlying session.</param>
    /// <param name="securityProfileSelector">Captures authorization for session admission and each run.</param>
    /// <param name="agentLoop">Drives the multi-turn tool-calling run.</param>
    /// <param name="runIds">Generates each turn's run identity.</param>
    /// <param name="operationIds">Generates each turn's operation identity.</param>
    /// <param name="messageIds">Generates each committed message's identity.</param>
    /// <param name="sessionEntryIds">Generates each committed session-entry identity.</param>
    /// <param name="timeProvider">The clock used to timestamp committed messages and entries.</param>
    /// <param name="options">The validated agent composition this session drives turns for.</param>
    /// <param name="logger">The optional content-safe diagnostics logger.</param>
    /// <param name="toolPresenter">The optional observational presenter used for bounded live tool rendering.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null, or a required option is unset.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity, limit, or timeout option is invalid.</exception>
    public DefaultConversationSession(
        ISessionCoordinator sessionCoordinator,
        ISecurityProfileSelector securityProfileSelector,
        IAgentLoop agentLoop,
        IIdentifierGenerator<RunId> runIds,
        IIdentifierGenerator<OperationId> operationIds,
        IIdentifierGenerator<MessageId> messageIds,
        IIdentifierGenerator<SessionEntryId> sessionEntryIds,
        TimeProvider timeProvider,
        IOptions<ConversationSessionOptions> options,
        ILogger<DefaultConversationSession>? logger,
        IToolPresenter? toolPresenter)
    {
        ArgumentNullException.ThrowIfNull(sessionCoordinator);
        ArgumentNullException.ThrowIfNull(securityProfileSelector);
        ArgumentNullException.ThrowIfNull(agentLoop);
        ArgumentNullException.ThrowIfNull(runIds);
        ArgumentNullException.ThrowIfNull(operationIds);
        ArgumentNullException.ThrowIfNull(messageIds);
        ArgumentNullException.ThrowIfNull(sessionEntryIds);
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
        _agentLoop = agentLoop;
        _runIds = runIds;
        _operationIds = operationIds;
        _messageIds = messageIds;
        _sessionEntryIds = sessionEntryIds;
        _timeProvider = timeProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultConversationSession>.Instance;
        _toolPresenter = toolPresenter;

        _agent = optionValues.Agent;
        _configuration = optionValues.Configuration;
        _agentId = optionValues.AgentId;
        _identity = optionValues.Identity;
        _securityProfileKey = optionValues.SecurityProfileKey;
        _agentDefinitionRevision = optionValues.AgentDefinitionRevision;
        _configurationVersion = optionValues.ConfigurationVersion;
        _sessionProfile = optionValues.SessionProfile;
        _modelSelectionPolicy = optionValues.ModelSelectionPolicy;
        _modelRequirements = optionValues.ModelRequirements;
        _instructions = [.. optionValues.Instructions];
        _tools = [.. optionValues.Tools];
        _toolPresentationBindings = toolPresentationBindings;
        _toolChoice = optionValues.ToolChoice;
        _requestSettings = optionValues.RequestSettings;
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
            var result = await SendCoreAsync(userText, observer, cancellationToken).ConfigureAwait(false);
            var outcome = result.Succeeded ? "settled" : "admission_failed";
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

    private async Task<ConversationTurnResult> SendCoreAsync(
        string userText,
        IConversationEventObserver? observer,
        CancellationToken cancellationToken)
    {
        await EnsureSessionAsync(cancellationToken).ConfigureAwait(false);

        var runId = _runIds.Create();
        var correlation = new InRunOperationCorrelation(_operationIds.Create(), runId, null);
        var appendAuthorization = await CaptureAuthorizationAsync(_sessionId, correlation, cancellationToken).ConfigureAwait(false);

        var address = new SessionAddress(_agentId, _sessionId!.Value);
        var sessionContext = new SessionOperationContext(
            _agentId, _sessionId.Value, null, correlation, _identity, appendAuthorization);
        var loadResult = await _sessionCoordinator.LoadAsync(
            sessionContext,
            _sessionProfile,
            cancellationToken).ConfigureAwait(false);
        if (loadResult is not SessionLoaded loaded)
        {
            return new ConversationTurnResult(false, [new ConversationAssistantTextEvent("The session could not be loaded.")]);
        }

        var readResult = await _sessionCoordinator.ReadAsync(
            new SessionReadRequest(sessionContext, _branchId, new SessionSequence(0), pageSize: 1),
            _sessionProfile,
            cancellationToken).ConfigureAwait(false);
        if (readResult is not SessionPage { Snapshot: { } snapshot })
        {
            return new ConversationTurnResult(false, [new ConversationAssistantTextEvent("The session history snapshot could not be captured.")]);
        }

        var currentVersion = snapshot.Version;
        var nextSequence = new SessionSequence(snapshot.UpperSequence.Value + 1);
        var now = _timeProvider.GetUtcNow();

        var userMessage = new MessageSessionEntry(
            _sessionEntryIds.Create(),
            address,
            correlation,
            _branchId,
            nextSequence,
            null,
            now,
            new SchemaVersion("1"),
            new UserMessage(
                _messageIds.Create(),
                _agentId,
                _sessionId.Value,
                loaded.Descriptor.ConversationId,
                _branchId,
                runId,
                null,
                now,
                MessageState.Complete,
                [new TextPart(userText, TextSemantics.Plain, ExtensionData.Empty)],
                ExtensionData.Empty));

        var appendResult = await _sessionCoordinator.AppendAsync(
            new SessionAppendRequest(
                new SessionOperationContext(_agentId, _sessionId.Value, null, correlation, _identity, appendAuthorization),
                _branchId,
                currentVersion,
                new IdempotencyKey(Guid.NewGuid().ToString()),
                [userMessage]),
            _sessionProfile,
            cancellationToken).ConfigureAwait(false);
        if (appendResult is not SessionAppended)
        {
            ConversationLog.TurnAdmissionFailed(_logger, _agentId);
            return new ConversationTurnResult(
                false,
                [new ConversationAssistantTextEvent($"Could not record the message: {DescribeAppendResult(appendResult)}")]);
        }

        var runAuthorization = await CaptureAuthorizationAsync(_sessionId, correlation, cancellationToken).ConfigureAwait(false);
        var request = (_agent, _configuration) is ({ } agent, { } configuration)
            ? new AgentRunRequest(
                agent,
                _sessionId.Value,
                _branchId,
                runId,
                _identity,
                runAuthorization,
                _sessionProfile,
                configuration,
                _maxTurns,
                _attemptTimeout,
                ExtensionData.Empty)
            : new AgentRunRequest(
                _agentId,
                _sessionId.Value,
                _branchId,
                runId,
                _identity,
                runAuthorization,
                _sessionProfile,
                _modelSelectionPolicy,
                _modelRequirements,
                _instructions,
                _tools,
                _toolChoice,
                _requestSettings,
                _maxTurns,
                _attemptTimeout,
                ExtensionData.Empty);
        request = request with
        {
            Observer = observer is null
                ? null
                : new ConversationRunObserver(
                    observer,
                    DescribeToolResult,
                    _toolPresenter,
                    _toolPresentationBindings),
        };

        var loopResult = await _agentLoop.RunAsync(request, cancellationToken).ConfigureAwait(false);
        var events = ProjectEvents(loopResult);
        if (loopResult.Outcome is AgentRunCompleted)
        {
            return new ConversationTurnResult(true, events);
        }

        ConversationLog.TurnAdmissionFailed(_logger, _agentId);
        var description = DescribeIncompleteOutcome(loopResult.Outcome);
        return new ConversationTurnResult(false, [.. events, new ConversationAssistantTextEvent(description)]);
    }

    private async Task EnsureSessionAsync(CancellationToken cancellationToken)
    {
        if (_sessionId is not null)
        {
            return;
        }

        ConversationLog.SessionCreating(_logger, _agentId);
        var correlation = new BeforeRunOperationCorrelation(_operationIds.Create(), null);
        var authorization = await CaptureAuthorizationAsync(null, correlation, cancellationToken).ConfigureAwait(false);
        var result = await _sessionCoordinator.CreateAsync(
            new SessionCreateRequest(
                _agentId,
                _identity,
                authorization,
                null,
                new IdempotencyKey(Guid.NewGuid().ToString()),
                ExtensionData.Empty),
            _sessionProfile,
            cancellationToken).ConfigureAwait(false);

        if (result is not SessionCreated created)
        {
            throw new InvalidOperationException($"Could not create the underlying session: {result}");
        }

        _sessionId = created.Descriptor.Address.SessionId;
        _branchId = created.Descriptor.ActiveBranchId;
    }

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
    /// <param name="options">The already nonnull conversation options.</param>
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
                                    call.CallId, call.Tool.Name, call.Arguments.GetRawText()));
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
                            part.Tool.Name,
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

    /// <summary>Describes a run outcome other than <see cref="AgentRunCompleted"/> in safe, non-sensitive text.</summary>
    /// <param name="outcome">The non-default terminal outcome to describe.</param>
    /// <returns>A human-readable sentence explaining why the run did not reach a final assistant message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="outcome"/> is null.</exception>
    private static string DescribeIncompleteOutcome(AgentRunOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return outcome switch
        {
            AgentRunSessionOperationFailed sessionFailed =>
                $"The run stopped: a session operation failed ({sessionFailed.SafeMessage}).",
            AgentRunTurnLimitReached limitReached =>
                $"The run stopped after reaching its {limitReached.MaxTurns}-turn limit with tool calls still pending.",
            AgentRunModelSelectionFailed modelSelectionFailed =>
                $"The run stopped: no usable model could be selected ({modelSelectionFailed.SafeReason}).",
            AgentRunContextPreparationFailed contextFailed =>
                $"The run stopped: context preparation failed ({contextFailed.Failure.Kind}: {contextFailed.Failure.SafeMessage}).",
            // Only the failure's bounded safe fields are user-visible. ProviderFailure.DiagnosticCause and
            // Extensions may carry transport detail or secrets and are for logs and diagnostics only.
            AgentRunFailed runFailed =>
                $"The run stopped: the model provider attempt failed ({runFailed.Failure.Kind}: {runFailed.Failure.SafeMessage}).",
            AgentRunCancelled cancelled =>
                $"The run stopped: {cancelled.SafeMessage}",
            AgentRunIdle =>
                "The run ended idle with no pending work and no final assistant message.",
            AgentRunInvalidState invalidState =>
                $"The run stopped: its captured lifecycle evidence was inconsistent ({invalidState.SafeMessage}).",
            AgentRunOutputRejected { Rejection: OutputRejected outputRejected } =>
                $"The run stopped: its terminal output was rejected ({outputRejected.Failure.Kind}: {outputRejected.Failure.SafeMessage}).",
            AgentRunOutputRejected =>
                "The run stopped: its output definition could not be applied.",
            _ => $"The run ended without a final assistant message (outcome: {outcome.GetType().Name}).",
        };
    }

    /// <summary>Describes a non-success append result using only its bounded safe fields.</summary>
    /// <param name="result">The append result that was not <see cref="SessionAppended"/>.</param>
    /// <returns>A short, content-free description.</returns>
    private static string DescribeAppendResult(SessionAppendResult result) => result switch
    {
        SessionAppendConflict conflict => $"the session changed concurrently (expected version {conflict.ExpectedVersion.Value}, actual {conflict.ActualVersion.Value}).",
        SessionAppendFailed failed => failed.SafeMessage,
        SessionAppendNotFound => "the session or branch was not found.",
        _ => result.GetType().Name,
    };

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
