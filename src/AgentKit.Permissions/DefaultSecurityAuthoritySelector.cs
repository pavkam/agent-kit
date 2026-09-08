// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Resolves explicitly registered security-authority bindings by their captured exact key.</summary>
/// <remarks>
/// The selector is a read-only activation boundary. It creates no authorization decision, grant, or
/// audit record, and never searches for an unkeyed fallback authority.
/// </remarks>
internal sealed class DefaultSecurityAuthoritySelector: ISecurityAuthoritySelector
{
    private readonly FrozenDictionary<ComponentKey<ISecurityAuthority>, ISecurityAuthority> _authorities;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DefaultSecurityAuthoritySelector> _logger;

    /// <summary>Initializes the selector from explicit singleton bindings.</summary>
    /// <param name="bindings">The complete authority bindings installed by the host composition.</param>
    /// <param name="timeProvider">The deterministic clock used only for observational duration.</param>
    /// <param name="logger">The optional content-free diagnostic logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bindings"/> or <paramref name="timeProvider"/> is null.</exception>
    /// <exception cref="ArgumentException">Two bindings use the same component key.</exception>
    public DefaultSecurityAuthoritySelector(
        IEnumerable<SecurityAuthorityBinding> bindings,
        TimeProvider timeProvider,
        ILogger<DefaultSecurityAuthoritySelector>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var snapshot = bindings.ToArray();
        ArgumentException.ThrowIfDuplicateSecurityAuthorityBinding(snapshot, nameof(bindings));

        _authorities = snapshot.ToFrozenDictionary(static binding => binding.Key, static binding => binding.Authority);
        _timeProvider = timeProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultSecurityAuthoritySelector>.Instance;
    }

    /// <inheritdoc/>
    public ValueTask<SecurityAuthoritySelectionResult> SelectAsync(
        SecurityAuthorizationContext authorization,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        var started = TryGetTimestamp();
        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.SecurityAuthoritySelect,
            ActivityKind.Internal,
            new ActivityTagsCollection
            {
                { AgentKitTagNames.GenAiOperationName, AgentKitActivityNames.SecurityAuthoritySelect },
                { AgentKitTagNames.AgentId, authorization.Scope.AgentId.ToString() },
                { AgentKitTagNames.SessionId, authorization.Scope.SessionId?.ToString() },
                { AgentKitTagNames.OperationId, authorization.Scope.Correlation.OperationId.ToString() },
                { AgentKitTagNames.SecurityAuthorityKey, authorization.AuthorityKey.ToString() },
            });
        var activity = activityScope.Activity;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            SafeLog(() => SecurityLog.AuthoritySelectionStarted(_logger, authorization.AuthorityKey));
            SecurityAuthoritySelectionResult result = _authorities.TryGetValue(authorization.AuthorityKey, out var authority)
                ? new SecurityAuthoritySelected(authorization, authority)
                : new SecurityAuthoritySelectionUnavailable(
                    authorization,
                    "The captured security authority binding is unavailable.");
            var outcome = result is SecurityAuthoritySelected
                ? SecurityAuthoritySelectionOutcome.Selected
                : SecurityAuthoritySelectionOutcome.Unavailable;
            if (result is SecurityAuthoritySelected)
            {
                SafeSetActivity(() => activity.SetSuccessful(outcome.ToStableValue()));
                SafeLog(() => SecurityLog.AuthoritySelected(_logger, authorization.AuthorityKey));
            }
            else
            {
                SafeSetActivity(() => activity.SetFailed(outcome.ToStableValue(), outcome.ToStableValue()));
                SafeLog(() => SecurityLog.AuthorityUnavailable(_logger, authorization.AuthorityKey));
            }

            SafeObserve(outcome, started);
            return ValueTask.FromResult(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SafeSetActivity(() => activity.SetFailed(
                SecurityAuthoritySelectionOutcome.Cancelled.ToStableValue(),
                nameof(OperationCanceledException)));
            SafeLog(() => SecurityLog.AuthoritySelectionCancelled(_logger, authorization.AuthorityKey));
            SafeObserve(SecurityAuthoritySelectionOutcome.Cancelled, started);
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            SafeSetActivity(() => activity.SetFailed(SecurityAuthoritySelectionOutcome.Failed.ToStableValue(), errorType));
            SafeLog(() => SecurityLog.AuthoritySelectionFaulted(_logger, authorization.AuthorityKey, errorType));
            SafeObserve(SecurityAuthoritySelectionOutcome.Failed, started);
            throw;
        }
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

    private void SafeObserve(SecurityAuthoritySelectionOutcome outcome, long? started)
    {
        Debug.Assert(Enum.IsDefined(outcome), "Selection observation receives a defined terminal outcome.");
        TimeSpan? elapsed = null;
        if (started is { } timestamp)
        {
            try
            {
                elapsed = _timeProvider.GetElapsedTime(timestamp);
            }
            catch
            {
                // A failed observational clock must not fabricate elapsed time or alter selection.
            }
        }

        try
        {
            SecurityMetrics.RecordAuthoritySelection(outcome, elapsed);
        }
        catch
        {
            // Meter listeners are observational and cannot alter selection.
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
            // Activity listeners are observational and cannot alter selection.
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
            // Logging providers are observational and cannot alter selection.
        }
    }
}
