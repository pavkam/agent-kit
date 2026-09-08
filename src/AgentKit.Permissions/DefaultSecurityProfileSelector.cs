// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit.Permissions;

/// <summary>Captures fresh authorization evidence from an exact immutable security-profile publication.</summary>
/// <remarks>The selector verifies reader-returned coordinates before binding scope and identity. It never substitutes a current profile, and diagnostics cannot alter the result.</remarks>
internal sealed class DefaultSecurityProfileSelector: ISecurityProfileSelector
{
    private readonly ISecurityProfilePublicationReader _publications;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DefaultSecurityProfileSelector> _logger;

    /// <summary>Initializes a selector over the immutable publication reader owned by the composition.</summary>
    /// <param name="publications">The exact-coordinate publication reader.</param>
    /// <param name="timeProvider">The deterministic clock used only for observational duration.</param>
    /// <param name="logger">The optional content-free diagnostic logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="publications"/> or <paramref name="timeProvider"/> is null.</exception>
    public DefaultSecurityProfileSelector(
        ISecurityProfilePublicationReader publications,
        TimeProvider timeProvider,
        ILogger<DefaultSecurityProfileSelector>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(publications);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _publications = publications;
        _timeProvider = timeProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultSecurityProfileSelector>.Instance;
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityAuthorizationCaptureResult> SelectAsync(
        SecurityAuthorizationCaptureRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var started = TryGetTimestamp();
        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.SecurityProfileCapture,
            ActivityKind.Internal,
            new ActivityTagsCollection
            {
                { AgentKitTagNames.AgentId, request.Scope.AgentId.ToString() },
                { AgentKitTagNames.SessionId, request.Scope.SessionId?.ToString() },
                { AgentKitTagNames.OperationId, request.Scope.Correlation.OperationId.ToString() },
                { AgentKitTagNames.SecurityProfileKey, request.ProfileKey.ToString() },
            });
        var activity = activityScope.Activity;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            SafeLog(() => SecurityLog.ProfileCaptureStarted(_logger, request.ProfileKey));
            var result = await _publications.ReadAsync(request.Scope.AgentId, request.AgentDefinitionRevision, request.ConfigurationVersion, request.ProfileKey, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (result is SecurityProfilePublicationFound { Publication: var publication })
            {
                if (!MatchesRequest(publication, request))
                {
                    return CompleteUnavailable(
                        activity,
                        request.ProfileKey,
                        SecurityProfileCaptureOutcome.MismatchedPublication,
                        started);
                }

                var captured = new SecurityAuthorizationCaptured(new SecurityAuthorizationContext(
                    publication.ProfileKey,
                    publication.ProfileVersion,
                    publication.PolicySnapshot,
                    publication.AuthorityKey,
                    request.AgentDefinitionRevision,
                    request.ConfigurationVersion,
                    request.Scope,
                    request.Identity));
                SafeSetActivity(() => activity.SetSuccessful(SecurityProfileCaptureOutcome.Captured.ToStableValue()));
                SafeLog(() => SecurityLog.ProfileCaptured(_logger, request.ProfileKey));
                SafeObserve(SecurityProfileCaptureOutcome.Captured, started);
                return captured;
            }

            return CompleteUnavailable(
                activity,
                request.ProfileKey,
                SecurityProfileCaptureOutcome.Unavailable,
                started);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SafeSetActivity(() => activity.SetFailed(
                SecurityProfileCaptureOutcome.Cancelled.ToStableValue(),
                nameof(OperationCanceledException)));
            SafeLog(() => SecurityLog.ProfileCaptureCancelled(_logger, request.ProfileKey));
            SafeObserve(SecurityProfileCaptureOutcome.Cancelled, started);
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            SafeSetActivity(() => activity.SetFailed(SecurityProfileCaptureOutcome.Failed.ToStableValue(), errorType));
            SafeLog(() => SecurityLog.ProfileCaptureFaulted(_logger, request.ProfileKey, errorType));
            SafeObserve(SecurityProfileCaptureOutcome.Failed, started);
            throw;
        }
    }

    private static bool MatchesRequest(
        SecurityProfilePublication publication,
        SecurityAuthorizationCaptureRequest request)
    {
        Debug.Assert(publication is not null, "Publication matching requires reader-supplied evidence.");
        Debug.Assert(request is not null, "Publication matching requires a validated capture request.");
        return publication.AgentId == request.Scope.AgentId
            && publication.AgentDefinitionRevision == request.AgentDefinitionRevision
            && publication.ConfigurationVersion == request.ConfigurationVersion
            && publication.ProfileKey == request.ProfileKey;
    }

    private SecurityAuthorizationCaptureUnavailable CompleteUnavailable(
        Activity? activity,
        SecurityProfileKey profileKey,
        SecurityProfileCaptureOutcome outcome,
        long? started)
    {
        Debug.Assert(
            outcome is SecurityProfileCaptureOutcome.Unavailable or SecurityProfileCaptureOutcome.MismatchedPublication,
            "Unavailable completion requires a fail-closed publication outcome.");
        SafeSetActivity(() => activity.SetFailed(outcome.ToStableValue(), outcome.ToStableValue()));
        SafeLog(() => SecurityLog.ProfileCaptureUnavailable(_logger, profileKey, outcome.ToStableValue()));
        SafeObserve(outcome, started);
        return new SecurityAuthorizationCaptureUnavailable("The exact security-profile publication is unavailable.");
    }

    private long? TryGetTimestamp()
    {
        try
        {
            return _timeProvider.GetTimestamp();
        }
        catch
        {
            return null;
        }
    }

    private void SafeObserve(SecurityProfileCaptureOutcome outcome, long? started)
    {
        Debug.Assert(Enum.IsDefined(outcome), "Profile-capture observation receives a defined terminal outcome.");
        TimeSpan? elapsed = null;
        if (started is { } timestamp)
        {
            try
            {
                elapsed = _timeProvider.GetElapsedTime(timestamp);
            }
            catch
            {
                // A failed observational clock must not fabricate elapsed time or alter capture.
            }
        }

        try
        {
            SecurityMetrics.RecordProfileCapture(outcome, elapsed);
        }
        catch
        {
            // Meter listeners are observational and cannot alter capture.
        }
    }

    private static void SafeSetActivity(Action action)
    {
        Debug.Assert(action is not null, "Activity observation requires a callback.");
        try
        {
            action();
        }
        catch
        {
            // Activity listeners are observational and cannot alter capture.
        }
    }

    private static void SafeLog(Action action)
    {
        Debug.Assert(action is not null, "Log observation requires a callback.");
        try
        {
            action();
        }
        catch
        {
            // Logging providers are observational and cannot alter capture.
        }
    }
}
