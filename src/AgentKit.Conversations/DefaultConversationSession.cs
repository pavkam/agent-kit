// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>
/// The default <see cref="IConversationSession"/>: composes <c>ISessionCoordinator</c>,
/// <c>ISecurityProfileSelector</c>, and <c>IAgentLoop</c> into one durable, authorized conversational turn.
/// </summary>
/// <remarks>
/// This class lazily creates its underlying session on the first <see cref="SendAsync"/> call and reuses that
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
    private readonly LlmToolChoice _toolChoice;
    private readonly LlmRequestSettings _requestSettings;
    private readonly int _maxTurns;
    private readonly TimeSpan _attemptTimeout;

    private SessionId? _sessionId;
    private BranchId _branchId;
    private bool _disposed;

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

        _sessionCoordinator = sessionCoordinator;
        _securityProfileSelector = securityProfileSelector;
        _agentLoop = agentLoop;
        _runIds = runIds;
        _operationIds = operationIds;
        _messageIds = messageIds;
        _sessionEntryIds = sessionEntryIds;
        _timeProvider = timeProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultConversationSession>.Instance;

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
        _toolChoice = optionValues.ToolChoice;
        _requestSettings = optionValues.RequestSettings;
        _maxTurns = optionValues.MaxTurns;
        _attemptTimeout = optionValues.AttemptTimeout;
    }

    /// <inheritdoc/>
    public async Task<ConversationTurnResult> SendAsync(string userText, CancellationToken cancellationToken = default)
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

        await _turnLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await SendCoreAsync(userText, cancellationToken).ConfigureAwait(false);
            var outcome = result.Succeeded ? "settled" : "admission_failed";
            activity.SetSuccessful(outcome);
            ConversationMetrics.RecordTurn(outcome, _timeProvider.GetElapsedTime(startedAt));
            ConversationLog.TurnSettled(_logger, _agentId, result.Events.Length);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", nameof(OperationCanceledException));
            ConversationMetrics.RecordTurn("cancelled", _timeProvider.GetElapsedTime(startedAt));
            ConversationLog.TurnCancelled(_logger, _agentId);
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            activity.SetFailed("faulted", errorType);
            ConversationMetrics.RecordTurn("faulted", _timeProvider.GetElapsedTime(startedAt));
            ConversationLog.TurnFaulted(_logger, _agentId, errorType);
            throw;
        }
        finally
        {
            _ = _turnLock.Release();
        }
    }

    private async Task<ConversationTurnResult> SendCoreAsync(string userText, CancellationToken cancellationToken)
    {
        await EnsureSessionAsync(cancellationToken).ConfigureAwait(false);

        var runId = _runIds.Create();
        var correlation = new InRunOperationCorrelation(_operationIds.Create(), runId, null);
        var appendAuthorization = await CaptureAuthorizationAsync(_sessionId, correlation, cancellationToken).ConfigureAwait(false);

        var address = new SessionAddress(_agentId, _sessionId!.Value);
        var loadResult = await _sessionCoordinator.LoadAsync(
            new SessionOperationContext(_agentId, _sessionId.Value, null, correlation, _identity, appendAuthorization),
            _sessionProfile,
            cancellationToken).ConfigureAwait(false);
        var currentVersion = loadResult is SessionLoaded loaded ? loaded.Descriptor.Version : new SessionVersion(0);
        var nextSequence = new SessionSequence(currentVersion.Value + 1);
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
                null,
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
                [new ConversationAssistantTextEvent($"Could not record the message: {appendResult}")]);
        }

        var runAuthorization = await CaptureAuthorizationAsync(_sessionId, correlation, cancellationToken).ConfigureAwait(false);
        var request = new AgentRunRequest(
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
                                events.Add(new ConversationToolCallEvent(call.Tool.Name, call.Arguments.GetRawText()));
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
                            part.Tool.Name,
                            part.Outcome.Kind == ToolCallOutcomeKind.Success,
                            part.Outcome.Kind == ToolCallOutcomeKind.Success
                                ? string.Join(" ", part.Content.OfType<TextPart>().Select(static p => p.Text))
                                : part.Outcome.FailureReason ?? "denied"));
                    }

                    break;
                default:
                    break;
            }
        }

        return events.ToImmutable();
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
                $"The run stopped: context preparation failed ({contextFailed.Failure}).",
            AgentRunFailed runFailed =>
                $"The run stopped: the model provider attempt failed ({runFailed.Failure}).",
            AgentRunCancelled cancelled =>
                $"The run stopped: {cancelled.SafeMessage}",
            AgentRunIdle =>
                "The run ended idle with no pending work and no final assistant message.",
            AgentRunInvalidState invalidState =>
                $"The run stopped: its captured lifecycle evidence was inconsistent ({invalidState.SafeMessage}).",
            AgentRunOutputRejected outputRejected =>
                $"The run stopped: its terminal output was rejected ({outputRejected.Rejection}).",
            _ => $"The run ended without a final assistant message (outcome: {outcome.GetType().Name}).",
        };
    }

    /// <summary>Releases this session's internal turn-serialization lock.</summary>
    /// <remarks>Disposal does not touch the underlying durable session; it remains exactly as last committed.</remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _turnLock.Dispose();
    }
}
