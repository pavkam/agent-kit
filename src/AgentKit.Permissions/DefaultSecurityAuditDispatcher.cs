// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

using Microsoft.Extensions.Options;

/// <summary>Delivers immutable audit records to captured additive sinks and fails closed for required delivery.</summary>
/// <remarks>
/// The dispatcher snapshots registrations, delivery policy, and finite per-sink deadline at construction. It checks
/// caller cancellation before and after each sink completion, so it never begins a later sink invocation after
/// cancellation becomes known. A deadline passes a linked cancellation token to cooperative sinks and bounds
/// cancellation-ignoring sinks with a task wait. A sink that already accepted a record remains an external durable
/// fact when cancellation or timeout propagates; the dispatcher does not reclassify that record or attempt
/// compensating deletion. It performs no authorization, grant, or audit persistence of its own, so audit
/// infrastructure cannot recursively authorize itself.
/// </remarks>
internal sealed class DefaultSecurityAuditDispatcher: ISecurityAuditDispatcher
{
    private readonly ImmutableArray<SecurityAuditSinkBinding> _bindings;
    private readonly SecurityAuditDelivery _defaultDelivery;
    private readonly TimeSpan _deliveryTimeout;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DefaultSecurityAuditDispatcher> _logger;

    /// <summary>Initializes the dispatcher from host-owned sink bindings and one validated options snapshot.</summary>
    /// <param name="bindings">The complete additive sink bindings to snapshot.</param>
    /// <param name="options">The validated permission options from which audit delivery is captured.</param>
    /// <param name="timeProvider">The deterministic clock used to schedule each delivery deadline and measure observational duration.</param>
    /// <param name="logger">The optional content-free diagnostic logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bindings"/>, <paramref name="options"/>, or <paramref name="timeProvider"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The captured audit delivery is undefined or its timeout cannot be scheduled by the underlying timer APIs.</exception>
    public DefaultSecurityAuditDispatcher(
        IEnumerable<SecurityAuditSinkBinding> bindings,
        IOptions<AgentPermissionOptions> options,
        TimeProvider timeProvider,
        ILogger<DefaultSecurityAuditDispatcher>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        var snapshot = bindings.ToImmutableArray();
        foreach (var binding in snapshot)
        {
            ArgumentNullException.ThrowIfNull(binding, nameof(bindings));
        }

        var configuredOptions = options.Value;
        ArgumentNullException.ThrowIfNull(configuredOptions, nameof(options));
        ArgumentOutOfRangeException.ThrowIfUndefined(configuredOptions.AuditDelivery, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(configuredOptions.AuditDeliveryTimeout, TimeSpan.Zero, nameof(options));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            configuredOptions.AuditDeliveryTimeout,
            AgentPermissionOptions.MaximumAuditDeliveryTimeout,
            nameof(options));
        _bindings = snapshot;
        _defaultDelivery = configuredOptions.AuditDelivery;
        _deliveryTimeout = configuredOptions.AuditDeliveryTimeout;
        _timeProvider = timeProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultSecurityAuditDispatcher>.Instance;
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityAuditDispatchResult> DispatchAsync(
        SecurityAuditRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        var started = TryGetTimestamp();
        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.SecurityAuditDispatch,
            ActivityKind.Internal,
            new ActivityTagsCollection
            {
                { AgentKitTagNames.AgentId, record.Scope.AgentId.ToString() },
                { AgentKitTagNames.SessionId, record.Scope.SessionId?.ToString() },
                { AgentKitTagNames.OperationId, record.Scope.Correlation.OperationId.ToString() },
                { AgentKitTagNames.SecurityRequestId, record.RequestId.ToString() },
                { AgentKitTagNames.SecurityAuditRecordId, record.Id.ToString() },
                { AgentKitTagNames.SecurityAuditEventKind, record.EventKind.ToString() },
                { AgentKitTagNames.SecurityAuditOutcome, record.Outcome.ToString() },
            });
        var activity = activityScope.Activity;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            SafeLog(() => SecurityLog.AuditDispatchStarted(_logger, record.Id, record.EventKind));
            var applicable = _bindings.Where(binding => binding.Registration.SupportedEventKinds.Contains(record.EventKind)).ToImmutableArray();
            var requiresDelivery = _defaultDelivery == SecurityAuditDelivery.Required
                || applicable.Any(static binding => binding.Registration.Delivery == SecurityAuditDelivery.Required);
            if (requiresDelivery && !applicable.Any(static binding => binding.Registration.ProvidesDurableAcceptance))
            {
                return Complete(
                    new SecurityAuditUnavailable("No compatible durable security audit sink is available."),
                    SecurityAuditDispatchOutcome.Unavailable,
                    record.Id,
                    activity,
                    started,
                    nameof(SecurityAuditUnavailable));
            }

            var durableAccepted = false;
            var durableAcceptanceTimedOut = false;
            foreach (var binding in applicable)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var required = binding.Registration.Delivery == SecurityAuditDelivery.Required;
                if (required && !binding.Registration.ProvidesDurableAcceptance)
                {
                    return Complete(
                        new SecurityAuditUnavailable("A required security audit sink cannot prove durable acceptance."),
                        SecurityAuditDispatchOutcome.Unavailable,
                        record.Id,
                        activity,
                        started,
                        nameof(SecurityAuditUnavailable));
                }

                Task? writeTask = null;
                CancellationTokenSource? deadline = null;
                try
                {
                    deadline = new CancellationTokenSource(_deliveryTimeout, _timeProvider);
                    using (deadline)
                    {
                        using var sinkCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
                        writeTask = binding.Sink.WriteAsync(record, sinkCancellation.Token).AsTask();
                        await writeTask.WaitAsync(sinkCancellation.Token).ConfigureAwait(false);
                        cancellationToken.ThrowIfCancellationRequested();
                        if (deadline.IsCancellationRequested)
                        {
                            ObserveLateSinkCompletion(writeTask);
                            if (!required)
                            {
                                durableAcceptanceTimedOut |= binding.Registration.ProvidesDurableAcceptance;
                                SafeLog(() => SecurityLog.BestEffortAuditDispatchFailed(
                                    _logger,
                                    record.Id,
                                    nameof(TimeoutException)));
                                continue;
                            }

                            return Complete(
                                new SecurityAuditTimedOut("Required security audit delivery timed out; durable acceptance is unknown."),
                                SecurityAuditDispatchOutcome.TimedOut,
                                record.Id,
                                activity,
                                started,
                                nameof(TimeoutException));
                        }

                        durableAccepted |= binding.Registration.ProvidesDurableAcceptance;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    ObserveLateSinkCompletion(writeTask);
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new UnreachableException();
                }
                catch (OperationCanceledException) when (deadline?.IsCancellationRequested == true)
                {
                    ObserveLateSinkCompletion(writeTask);
                    if (!required)
                    {
                        durableAcceptanceTimedOut |= binding.Registration.ProvidesDurableAcceptance;
                        SafeLog(() => SecurityLog.BestEffortAuditDispatchFailed(
                            _logger,
                            record.Id,
                            nameof(TimeoutException)));
                        continue;
                    }

                    return Complete(
                        new SecurityAuditTimedOut("Required security audit delivery timed out; durable acceptance is unknown."),
                        SecurityAuditDispatchOutcome.TimedOut,
                        record.Id,
                        activity,
                        started,
                        nameof(TimeoutException));
                }
                catch (Exception exception) when (!required)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    SafeLog(() => SecurityLog.BestEffortAuditDispatchFailed(
                        _logger,
                        record.Id,
                        ErrorType(exception)));
                }
                catch (Exception exception)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return Complete(
                        new SecurityAuditFailed("Required security audit delivery failed before durable acceptance."),
                        SecurityAuditDispatchOutcome.Failed,
                        record.Id,
                        activity,
                        started,
                        ErrorType(exception));
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            return _defaultDelivery == SecurityAuditDelivery.Required && !durableAccepted
                ? durableAcceptanceTimedOut
                    ? Complete(
                        new SecurityAuditTimedOut("Security audit delivery timed out; durable acceptance is unknown."),
                        SecurityAuditDispatchOutcome.TimedOut,
                        record.Id,
                        activity,
                        started,
                        nameof(TimeoutException))
                    : Complete(
                        new SecurityAuditFailed("No compatible durable security audit sink accepted the record."),
                        SecurityAuditDispatchOutcome.Failed,
                        record.Id,
                        activity,
                        started,
                        nameof(SecurityAuditFailed))
                : Complete(
                new SecurityAuditAccepted(),
                SecurityAuditDispatchOutcome.Accepted,
                record.Id,
                activity,
                started,
                null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Observe(
                SecurityAuditDispatchOutcome.Cancelled,
                record.Id,
                activity,
                started,
                nameof(OperationCanceledException));
            throw;
        }
    }

    private SecurityAuditDispatchResult Complete(
        SecurityAuditDispatchResult result,
        SecurityAuditDispatchOutcome outcome,
        SecurityAuditRecordId recordId,
        Activity? activity,
        long? started,
        string? errorType)
    {
        Debug.Assert(result is not null, "Completed dispatch requires a terminal result.");
        Debug.Assert(outcome != SecurityAuditDispatchOutcome.Cancelled,
            "Caller cancellation does not produce a terminal dispatch result.");
        Observe(outcome, recordId, activity, started, errorType);
        return result;
    }

    private void Observe(
        SecurityAuditDispatchOutcome outcome,
        SecurityAuditRecordId recordId,
        Activity? activity,
        long? started,
        string? errorType)
    {
        Debug.Assert(Enum.IsDefined(outcome), "Audit dispatch observation receives a defined terminal outcome.");
        Debug.Assert(outcome == SecurityAuditDispatchOutcome.Accepted || !string.IsNullOrWhiteSpace(errorType),
            "A non-accepted dispatch outcome requires a safe error type.");
        SafeSetActivity(() =>
        {
            if (outcome == SecurityAuditDispatchOutcome.Accepted)
            {
                activity.SetSuccessful(outcome.ToStableValue());
            }
            else
            {
                activity.SetFailed(outcome.ToStableValue(), errorType!);
            }
        });
        SafeLog(() => LogCompletion(outcome, recordId, errorType));
        SafeObserve(outcome, started);
    }

    private void LogCompletion(SecurityAuditDispatchOutcome outcome, SecurityAuditRecordId recordId, string? errorType)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(outcome);
        switch (outcome)
        {
            case SecurityAuditDispatchOutcome.Accepted:
                SecurityLog.AuditDispatchAccepted(_logger, recordId);
                return;
            case SecurityAuditDispatchOutcome.Unavailable:
                SecurityLog.AuditDispatchUnavailable(_logger, recordId);
                return;
            case SecurityAuditDispatchOutcome.Failed:
                SecurityLog.AuditDispatchFailed(_logger, recordId, errorType!);
                return;
            case SecurityAuditDispatchOutcome.TimedOut:
                SecurityLog.AuditDispatchTimedOut(_logger, recordId);
                return;
            case SecurityAuditDispatchOutcome.Cancelled:
                SecurityLog.AuditDispatchCancelled(_logger, recordId);
                return;
            default:
                throw new UnreachableException();
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

    private void SafeObserve(SecurityAuditDispatchOutcome outcome, long? started)
    {
        Debug.Assert(Enum.IsDefined(outcome), "Audit dispatch observation receives a defined terminal outcome.");
        TimeSpan? elapsed = null;
        if (started is { } timestamp)
        {
            try
            {
                elapsed = _timeProvider.GetElapsedTime(timestamp);
            }
            catch
            {
                // A failed observational clock must not fabricate elapsed time or alter audit delivery.
            }
        }

        try
        {
            SecurityMetrics.RecordAuditDispatch(outcome, elapsed);
        }
        catch
        {
            // Meter listeners are observational and cannot alter audit delivery.
        }
    }

    private static string ErrorType(Exception exception)
    {
        Debug.Assert(exception is not null, "A caught sink failure always has an exception.");
        return exception.GetType().FullName ?? exception.GetType().Name;
    }

    private static void ObserveLateSinkCompletion(Task? writeTask)
    {
        if (writeTask is null)
        {
            return;
        }

        try
        {
            _ = writeTask.ContinueWith(
                static completed => _ = completed.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
        catch
        {
            // Late sink observation is best-effort and cannot change the bounded dispatch result.
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
            // Activity listeners are observational and cannot alter audit delivery.
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
            // Logging providers are observational and cannot alter audit delivery.
        }
    }
}
