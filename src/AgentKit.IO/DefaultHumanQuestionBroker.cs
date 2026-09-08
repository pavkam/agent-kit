// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Consumes exact publication authority before forwarding a grant-free prompt to the selected application channel.</summary>
public sealed class DefaultHumanQuestionBroker: IHumanQuestionBroker
{
    private readonly ISecurityGrantStore _grantStore;
    private readonly IHumanQuestionChannel _channel;
    private readonly IIdentifierGenerator<SecurityEnforcementIntentId> _intentIds;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DefaultHumanQuestionBroker> _logger;

    /// <summary>Initializes the broker with default local identities and disabled content-free logging.</summary>
    /// <param name="grantStore">The non-null store that atomically validates and consumes publication grants.</param>
    /// <param name="channel">The non-null application-owned question presentation and resolution channel.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public DefaultHumanQuestionBroker(ISecurityGrantStore grantStore, IHumanQuestionChannel channel)
        : this(grantStore, channel, new GuidSecurityEnforcementIntentIdGenerator(), TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultHumanQuestionBroker>.Instance)
    {
    }

    /// <summary>Initializes the broker with a replaceable source of fresh atomic permission-to-start identities.</summary>
    /// <param name="grantStore">The non-null store that atomically validates and consumes publication grants.</param>
    /// <param name="channel">The non-null application-owned question presentation and resolution channel.</param>
    /// <param name="intentIds">The non-null thread-safe source of distinct enforcement-intent identities.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public DefaultHumanQuestionBroker(ISecurityGrantStore grantStore, IHumanQuestionChannel channel,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds)
        : this(grantStore, channel, intentIds, TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultHumanQuestionBroker>.Instance)
    {
    }

    /// <summary>Initializes the broker with replaceable safe operational-observation dependencies.</summary>
    /// <param name="grantStore">The non-null store that atomically validates and consumes publication grants.</param>
    /// <param name="channel">The non-null application-owned question presentation and resolution channel.</param>
    /// <param name="intentIds">The non-null thread-safe source of distinct enforcement-intent identities.</param>
    /// <param name="timeProvider">The non-null clock used only for observational elapsed duration.</param>
    /// <param name="logger">The non-null content-free structured logger.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public DefaultHumanQuestionBroker(ISecurityGrantStore grantStore, IHumanQuestionChannel channel,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds, TimeProvider timeProvider,
        ILogger<DefaultHumanQuestionBroker> logger)
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
    public ComponentId SecurityAudience { get; } = new("agentkit.io.human-question");

    /// <inheritdoc/>
    public async ValueTask<HumanQuestionResult> AskAsync(HumanQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var started = TryGetTimestamp();
        var tags = new ActivityTagsCollection
        {
                { AgentKitTagNames.QuestionId, request.Id.ToString() },
                { AgentKitTagNames.AgentId, request.AgentId.ToString() },
                { AgentKitTagNames.SessionId, request.SessionId?.ToString() },
                { AgentKitTagNames.ToolCallId, request.ToolCallId.ToString() },
                { AgentKitTagNames.OperationId, request.Correlation.OperationId.ToString() },
                { AgentKitTagNames.SecurityRequestId, request.Grant.RequestId.ToString() },
                { AgentKitTagNames.TenantId, request.Identity.TenantId.ToString() },
            };
        if (request.Correlation is InRunOperationCorrelation { RunId: { } runId, TurnId: var turnId })
        {
            tags.Add(AgentKitTagNames.RunId, runId.ToString());
            if (turnId is { } establishedTurnId)
            {
                tags.Add(AgentKitTagNames.TurnId, establishedTurnId.ToString());
            }
        }

        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.HumanQuestionPublish, ActivityKind.Internal, tags);
        var activity = activityScope.Activity;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!HumanQuestionEnforcementReceipt.HasCompatibleCapturedAuthorization(request))
            {
                return CompleteUnavailable(activity, request,
                    HumanQuestionPublicationOutcome.CapturedAuthorizationMismatch, started,
                    "The captured authorization does not match the question publication.");
            }

            var enforcement = HumanQuestionEnforcementReceipt.Create(request, SecurityAudience);
            var intent = new SecurityEnforcementIntent(_intentIds.Create(), null);
            var consumption = await _grantStore.ValidateAndConsumeAsync(
                request.Grant, enforcement, intent, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!HumanQuestionEnforcementReceipt.IsFreshExact(consumption, request.Grant, enforcement, intent))
            {
                return CompleteUnavailable(activity, request, HumanQuestionPublicationOutcome.GrantDenied, started,
                    HumanQuestionEnforcementReceipt.DenialMessage(consumption));
            }

            var result = await _channel.AskAsync(new HumanQuestionPrompt(request.Id, request.AgentId,
                request.SessionId, request.ToolCallId, request.Correlation, request.Identity, request.Prompt,
                request.Options, request.AllowsFreeText, request.Deadline), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var outcome = result.QuestionId == request.Id
                ? result switch
                {
                    HumanQuestionAnswered => HumanQuestionPublicationOutcome.Answered,
                    HumanQuestionTimedOut => HumanQuestionPublicationOutcome.TimedOut,
                    HumanQuestionUnavailable => HumanQuestionPublicationOutcome.ChannelUnavailable,
                    _ => HumanQuestionPublicationOutcome.Failed,
                }
                : HumanQuestionPublicationOutcome.Failed;
            return Complete(activity, request, outcome, started, result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SafeSetActivity(() => activity.SetFailed(HumanQuestionPublicationOutcome.Cancelled.ToStableValue(),
                nameof(OperationCanceledException)));
            SafeLog(() => LogCancelled(request));
            SafeObserve(HumanQuestionPublicationOutcome.Cancelled, started);
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            SafeSetActivity(() => activity.SetFailed(HumanQuestionPublicationOutcome.Failed.ToStableValue(), errorType));
            SafeLog(() => LogFailed(request, errorType));
            SafeObserve(HumanQuestionPublicationOutcome.Failed, started);
            throw;
        }
    }

    private HumanQuestionResult CompleteUnavailable(Activity? activity, HumanQuestionRequest request,
        HumanQuestionPublicationOutcome outcome, long? started, string message)
    {
        Debug.Assert(request is not null, "A public broker call supplies a non-null request.");
        Debug.Assert(outcome is HumanQuestionPublicationOutcome.GrantDenied or HumanQuestionPublicationOutcome.CapturedAuthorizationMismatch,
            "A typed unavailable result must represent a pre-channel enforcement denial.");
        return Complete(activity, request, outcome, started, new HumanQuestionUnavailable(request.Id, message));
    }

    private HumanQuestionResult Complete(Activity? activity, HumanQuestionRequest request,
        HumanQuestionPublicationOutcome outcome, long? started, HumanQuestionResult result)
    {
        Debug.Assert(request is not null, "A public broker call supplies a non-null request.");
        Debug.Assert(Enum.IsDefined(outcome), "Human-question observation receives a defined terminal outcome.");
        Debug.Assert(result is not null, "Human-question completion retains the channel's non-null semantic result.");
        var outcomeValue = outcome.ToStableValue();
        SafeSetActivity(() =>
        {
            if (outcome is HumanQuestionPublicationOutcome.Answered)
            {
                activity.SetSuccessful(outcomeValue);
            }
            else
            {
                activity.SetFailed(outcomeValue, outcomeValue);
            }
        });
        SafeLog(() => LogCompleted(request, outcome is HumanQuestionPublicationOutcome.Failed ? LogLevel.Error : LogLevel.Information, outcomeValue));
        SafeObserve(outcome, started);
        return result;
    }


    private void LogCompleted(HumanQuestionRequest request, LogLevel level, string outcome)
    {
        Debug.Assert(request is not null, "A public broker call supplies a non-null request.");
        Debug.Assert(Enum.IsDefined(level), "Completed human-question logging uses a defined severity.");
        Debug.Assert(!string.IsNullOrWhiteSpace(outcome), "Completed human-question logging records a stable outcome.");
        var correlation = request.Correlation as InRunOperationCorrelation;
        HumanQuestionBrokerLog.Completed(_logger, request.Id, request.Identity.TenantId, request.AgentId, request.SessionId,
            correlation?.RunId, correlation?.TurnId, request.ToolCallId, request.Correlation.OperationId,
            request.Grant.RequestId, level, outcome);
    }

    private void LogCancelled(HumanQuestionRequest request)
    {
        Debug.Assert(request is not null, "A public broker call supplies a non-null request.");
        var correlation = request.Correlation as InRunOperationCorrelation;
        HumanQuestionBrokerLog.Cancelled(_logger, request.Id, request.Identity.TenantId, request.AgentId, request.SessionId,
            correlation?.RunId, correlation?.TurnId, request.ToolCallId, request.Correlation.OperationId,
            request.Grant.RequestId);
    }

    private void LogFailed(HumanQuestionRequest request, string errorType)
    {
        Debug.Assert(request is not null, "A public broker call supplies a non-null request.");
        Debug.Assert(!string.IsNullOrWhiteSpace(errorType), "Failed human-question logging records a normalized error type.");
        var correlation = request.Correlation as InRunOperationCorrelation;
        HumanQuestionBrokerLog.Failed(_logger, request.Id, request.Identity.TenantId, request.AgentId, request.SessionId,
            correlation?.RunId, correlation?.TurnId, request.ToolCallId, request.Correlation.OperationId,
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

    private void SafeObserve(HumanQuestionPublicationOutcome outcome, long? started)
    {
        try { IOMetrics.RecordHumanQuestionPublication(outcome, TryGetElapsedTime(started)); }
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
