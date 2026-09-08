// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Consumes exact single-use delegation authority immediately before child dispatch.</summary>
public sealed class DefaultTaskDelegationBroker: ITaskDelegationBroker
{
    private readonly ISecurityGrantStore _grantStore;
    private readonly ITaskDelegationChannel _channel;
    private readonly IIdentifierGenerator<SecurityEnforcementIntentId> _intentIds;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DefaultTaskDelegationBroker> _logger;

    /// <summary>Initializes the broker with default local identities and disabled content-free logging.</summary>
    /// <param name="grantStore">The non-null atomic validator and consumer of delegation grants.</param>
    /// <param name="channel">The non-null goal-aware child dispatcher.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public DefaultTaskDelegationBroker(ISecurityGrantStore grantStore, ITaskDelegationChannel channel)
        : this(grantStore, channel, new GuidSecurityEnforcementIntentIdGenerator(), TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultTaskDelegationBroker>.Instance)
    {
    }

    /// <summary>Initializes the broker with a replaceable source of fresh atomic permission-to-start identities.</summary>
    /// <param name="grantStore">The non-null atomic validator and consumer of delegation grants.</param>
    /// <param name="channel">The non-null goal-aware child dispatcher.</param>
    /// <param name="intentIds">The non-null thread-safe source of distinct enforcement-intent identities.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public DefaultTaskDelegationBroker(ISecurityGrantStore grantStore, ITaskDelegationChannel channel,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds)
        : this(grantStore, channel, intentIds, TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultTaskDelegationBroker>.Instance)
    {
    }

    /// <summary>Initializes the broker with replaceable safe operational-observation dependencies.</summary>
    /// <param name="grantStore">The non-null atomic validator and consumer of delegation grants.</param>
    /// <param name="channel">The non-null goal-aware child dispatcher.</param>
    /// <param name="intentIds">The non-null thread-safe source of distinct enforcement-intent identities.</param>
    /// <param name="timeProvider">The non-null clock used only for observational elapsed duration.</param>
    /// <param name="logger">The non-null content-free structured logger.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public DefaultTaskDelegationBroker(ISecurityGrantStore grantStore, ITaskDelegationChannel channel,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds, TimeProvider timeProvider,
        ILogger<DefaultTaskDelegationBroker> logger)
    {
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _grantStore = grantStore;
        _channel = channel;
        _intentIds = intentIds;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("agentkit.goals.delegation");

    /// <inheritdoc/>
    public async ValueTask<TaskDelegationResult> DelegateAsync(TaskDelegationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var started = TryGetTimestamp();
        var tags = new ActivityTagsCollection
        {
                { AgentKitTagNames.DelegationId, request.Prompt.Id.ToString() },
                { AgentKitTagNames.AgentId, request.Prompt.ParentAgentId.ToString() },
                { AgentKitTagNames.SessionId, request.Prompt.ParentSessionId.ToString() },
                { AgentKitTagNames.RunId, request.Prompt.ParentRunId.ToString() },
                { AgentKitTagNames.ToolCallId, request.Prompt.ToolCallId.ToString() },
                { AgentKitTagNames.OperationId, request.Prompt.Correlation.OperationId.ToString() },
                { AgentKitTagNames.SecurityRequestId, request.Grant.RequestId.ToString() },
                { AgentKitTagNames.TenantId, request.Prompt.Identity.TenantId.ToString() },
            };
        if (request.Prompt.Correlation.TurnId is { } turnId)
        {
            tags.Add(AgentKitTagNames.TurnId, turnId.ToString());
        }

        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.TaskDelegationDispatch, ActivityKind.Internal, tags);
        var activity = activityScope.Activity;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TaskDelegationEnforcementReceipt.HasCompatibleCapturedAuthorization(request))
            {
                return CompleteRejected(activity, request,
                    TaskDelegationPublicationOutcome.CapturedAuthorizationMismatch, started,
                    "The captured authorization does not match the task delegation.");
            }

            var enforcement = TaskDelegationEnforcementReceipt.Create(request, SecurityAudience);
            var intent = new SecurityEnforcementIntent(_intentIds.Create(), null);
            var consumption = await _grantStore.ValidateAndConsumeAsync(
                request.Grant, enforcement, intent, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!TaskDelegationEnforcementReceipt.IsFreshExact(consumption, request.Grant, enforcement, intent))
            {
                return CompleteRejected(activity, request, TaskDelegationPublicationOutcome.GrantDenied,
                    started, TaskDelegationEnforcementReceipt.DenialMessage(consumption));
            }

            var result = await _channel.DelegateAsync(request.Prompt, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var outcome = result.Id == request.Prompt.Id
                ? result switch
                {
                    TaskDelegationChildResult => TaskDelegationPublicationOutcome.Dispatched,
                    TaskDelegationRejected => TaskDelegationPublicationOutcome.ChannelRejected,
                    _ => TaskDelegationPublicationOutcome.Failed,
                }
                : TaskDelegationPublicationOutcome.Failed;
            return Complete(activity, request, outcome, started, result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SafeSetActivity(() => activity.SetFailed(TaskDelegationPublicationOutcome.Cancelled.ToStableValue(),
                nameof(OperationCanceledException)));
            SafeLog(() => LogCancelled(request));
            SafeObserve(TaskDelegationPublicationOutcome.Cancelled, started);
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            SafeSetActivity(() => activity.SetFailed(TaskDelegationPublicationOutcome.Failed.ToStableValue(), errorType));
            SafeLog(() => LogFailed(request, errorType));
            SafeObserve(TaskDelegationPublicationOutcome.Failed, started);
            throw;
        }
    }

    private TaskDelegationResult CompleteRejected(Activity? activity, TaskDelegationRequest request,
        TaskDelegationPublicationOutcome outcome, long? started, string message)
    {
        Debug.Assert(request is not null, "A public broker call supplies a non-null request.");
        Debug.Assert(outcome is TaskDelegationPublicationOutcome.GrantDenied
            or TaskDelegationPublicationOutcome.CapturedAuthorizationMismatch,
            "A typed rejected result must represent a pre-channel enforcement denial.");
        return Complete(activity, request, outcome, started, new TaskDelegationRejected(request.Prompt.Id, message));
    }

    private TaskDelegationResult Complete(Activity? activity, TaskDelegationRequest request,
        TaskDelegationPublicationOutcome outcome, long? started, TaskDelegationResult result)
    {
        Debug.Assert(request is not null, "A public broker call supplies a non-null request.");
        Debug.Assert(Enum.IsDefined(outcome), "Task-delegation observation receives a defined terminal outcome.");
        Debug.Assert(result is not null, "Task-delegation completion retains the channel's non-null semantic result.");
        var outcomeValue = outcome.ToStableValue();
        SafeSetActivity(() =>
        {
            if (outcome is TaskDelegationPublicationOutcome.Dispatched)
            {
                activity.SetSuccessful(outcomeValue);
            }
            else
            {
                activity.SetFailed(outcomeValue, outcomeValue);
            }
        });
        SafeLog(() => LogCompleted(request, outcome is TaskDelegationPublicationOutcome.Failed ? LogLevel.Error : LogLevel.Information, outcomeValue));
        SafeObserve(outcome, started);
        return result;
    }


    private void LogCompleted(TaskDelegationRequest request, LogLevel level, string outcome)
    {
        Debug.Assert(request is not null, "A public broker call supplies a non-null request.");
        Debug.Assert(Enum.IsDefined(level), "Completed task-delegation logging uses a defined severity.");
        Debug.Assert(!string.IsNullOrWhiteSpace(outcome), "Completed task-delegation logging records a stable outcome.");
        TaskDelegationBrokerLog.Completed(_logger, request.Prompt.Id, request.Prompt.Identity.TenantId,
            request.Prompt.ParentAgentId, request.Prompt.ParentSessionId, request.Prompt.ParentRunId,
            request.Prompt.Correlation.TurnId, request.Prompt.ToolCallId, request.Prompt.Correlation.OperationId,
            request.Grant.RequestId, level, outcome);
    }

    private void LogCancelled(TaskDelegationRequest request)
    {
        Debug.Assert(request is not null, "A public broker call supplies a non-null request.");
        TaskDelegationBrokerLog.Cancelled(_logger, request.Prompt.Id, request.Prompt.Identity.TenantId,
            request.Prompt.ParentAgentId, request.Prompt.ParentSessionId, request.Prompt.ParentRunId,
            request.Prompt.Correlation.TurnId, request.Prompt.ToolCallId, request.Prompt.Correlation.OperationId,
            request.Grant.RequestId);
    }

    private void LogFailed(TaskDelegationRequest request, string errorType)
    {
        Debug.Assert(request is not null, "A public broker call supplies a non-null request.");
        Debug.Assert(!string.IsNullOrWhiteSpace(errorType), "Failed task-delegation logging records a normalized error type.");
        TaskDelegationBrokerLog.Failed(_logger, request.Prompt.Id, request.Prompt.Identity.TenantId,
            request.Prompt.ParentAgentId, request.Prompt.ParentSessionId, request.Prompt.ParentRunId,
            request.Prompt.Correlation.TurnId, request.Prompt.ToolCallId, request.Prompt.Correlation.OperationId,
            request.Grant.RequestId, errorType);
    }

    private long? TryGetTimestamp()
    {
        try { return _timeProvider.GetTimestamp(); } catch { return null; }
    }

    private TimeSpan? TryGetElapsedTime(long? started)
    {
        if (started is not { } timestamp)
        {
            return null;
        }

        try { return _timeProvider.GetElapsedTime(timestamp); } catch { return null; }
    }

    private void SafeObserve(TaskDelegationPublicationOutcome outcome, long? started)
    {
        try { GoalsMetrics.RecordTaskDelegationPublication(outcome, TryGetElapsedTime(started)); }
        catch { }
    }

    private static void SafeSetActivity(Action observation)
    {
        try { observation(); } catch { }
    }

    private static void SafeLog(Action observation)
    {
        try { observation(); } catch { }
    }
}
