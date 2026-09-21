// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

/// <summary>
/// The first-party <see cref="IHookDispatcher"/>: resolves registrations through
/// <see cref="IHookOrderResolver"/>, invokes hooks sequentially, validates event arguments after every invocation,
/// honors typed short-circuiting, and enforces bounded reentrancy on the activation lease.
/// </summary>
public sealed class DefaultHookDispatcher: IHookDispatcher
{
    private readonly ILogger<DefaultHookDispatcher> _logger;
    private readonly HookFailureMode _minimumFailureMode;
    private readonly TimeSpan _defaultHookTimeout;
    private readonly IHookOrderResolver _orderResolver;
    private readonly IIdentifierGenerator<HookInvocationId> _invocationIds;
    private readonly IHookDiagnosticDispatcher? _diagnostics;
    private readonly TimeProvider _timeProvider;
    private readonly HookProfileRegistry? _profiles;

    /// <summary>
    /// Initializes a dispatcher with the default <see cref="AgentHookOptions"/> ceilings, which leave every
    /// caller's requested depth and failure mode unchanged.
    /// </summary>
    /// <param name="logger">The optional structured logger; a null value disables log publication.</param>
    public DefaultHookDispatcher(ILogger<DefaultHookDispatcher>? logger = null)
        : this(
            Options.Create(new AgentHookOptions()),
            logger,
            orderResolver: null,
            invocationIds: null,
            diagnostics: null,
            timeProvider: null)
    {
    }

    /// <summary>
    /// Initializes a dispatcher that enforces the supplied host ceilings on every dispatch.
    /// </summary>
    /// <param name="options">
    /// The host ceilings. <see cref="IOptions{TOptions}.Value"/> is read once here; later changes to the options
    /// instance do not affect this dispatcher.
    /// </param>
    /// <param name="logger">The optional structured logger; a null value disables log publication.</param>
    /// <param name="orderResolver">The order resolver used by the kernel dispatch overload, or null to use the default.</param>
    /// <param name="invocationIds">The invocation identity generator used by the kernel dispatch overload, or null to use the default.</param>
    /// <param name="diagnostics">The diagnostic dispatcher, or null to skip publication.</param>
    /// <param name="timeProvider">The clock used for deadlines and diagnostic timing, or null to use <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/> or its <see cref="IOptions{TOptions}.Value"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="AgentHookOptions.MaximumInvocationDepth"/> is less than 1,
    /// <see cref="AgentHookOptions.DefaultHookTimeout"/> is not positive, or
    /// <see cref="AgentHookOptions.MinimumFailureMode"/> is not a defined <see cref="HookFailureMode"/>. The
    /// exception names <paramref name="options"/>.
    /// </exception>
    public DefaultHookDispatcher(
        IOptions<AgentHookOptions> options,
        ILogger<DefaultHookDispatcher>? logger = null,
        IHookOrderResolver? orderResolver = null,
        IIdentifierGenerator<HookInvocationId>? invocationIds = null,
        IHookDiagnosticDispatcher? diagnostics = null,
        TimeProvider? timeProvider = null)
        : this(
            options,
            logger,
            orderResolver,
            invocationIds,
            diagnostics,
            timeProvider,
            profiles: null)
    {
    }

    internal DefaultHookDispatcher(
        IOptions<AgentHookOptions> options,
        ILogger<DefaultHookDispatcher>? logger,
        IHookOrderResolver? orderResolver,
        IIdentifierGenerator<HookInvocationId>? invocationIds,
        IHookDiagnosticDispatcher? diagnostics,
        TimeProvider? timeProvider,
        HookProfileRegistry? profiles)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Value, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Value.MaximumInvocationDepth, 1, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.Value.DefaultHookTimeout, TimeSpan.Zero, nameof(options));
        ArgumentOutOfRangeException.ThrowIfUndefined(options.Value.MinimumFailureMode, nameof(options));

        _logger = logger ?? NullLogger<DefaultHookDispatcher>.Instance;
        _minimumFailureMode = options.Value.MinimumFailureMode;
        _defaultHookTimeout = options.Value.DefaultHookTimeout;
        _orderResolver = orderResolver ?? new HookOrderResolver();
        _invocationIds = invocationIds ?? new GuidIdentifierGenerator<HookInvocationId>(static value => new HookInvocationId(value));
        _diagnostics = diagnostics;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _profiles = profiles;
    }

    /// <inheritdoc/>
    public async ValueTask DispatchAsync<THook, TEventArgs>(
        HookPointDefinition<THook, TEventArgs> point,
        HookDispatchContext context,
        TEventArgs eventArgs,
        HookFailureMode? failurePolicyTightening = null,
        CancellationToken cancellationToken = default)
        where THook : class
        where TEventArgs : AgentHookEventArgs
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(eventArgs);
        if (!point.Id.Equals(context.Dispatch.Point) || !point.Id.Equals(eventArgs.Point))
        {
            throw new ArgumentException(
                "The hook point definition, dispatch metadata, and event arguments must agree on the same hook point identity.",
                nameof(point));
        }

        if (failurePolicyTightening is { } tightening)
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(tightening, nameof(failurePolicyTightening));
        }

        var scoped = eventArgs as IAgentScopedHookStage;
        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.HookDispatch,
            ActivityKind.Internal,
            new KeyValuePair<string, object?>[]
            {
                new(AgentKitTagNames.HookPoint, point.Id.ToString()),
                new(AgentKitTagNames.HookDispatchId, eventArgs.DispatchId.ToString()),
                new(AgentKitTagNames.AgentId, scoped?.AgentId.ToString()),
                new(AgentKitTagNames.SessionId, scoped?.SessionId?.ToString()),
                new(AgentKitTagNames.OperationId, eventArgs.Correlation.OperationId.ToString()),
            });
        var activity = activityScope.Activity;

        try
        {
            await DispatchKernelCoreAsync(point, context, eventArgs, failurePolicyTightening, activity, cancellationToken)
                .ConfigureAwait(false);
            var outcome = eventArgs is IShortCircuitingHookArgs { IsShortCircuited: true } ? "short_circuited" : "completed";
            SafeSetActivity(() => activity.SetSuccessful(outcome));
            SafeLog(() => HookLog.DispatchCompleted(_logger, point.Id, eventArgs.DispatchId));
            SafeObserve(static () => HookMetrics.RecordDispatch("completed"));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SafeSetActivity(() => activity.SetFailed("cancelled", nameof(OperationCanceledException)));
            SafeLog(() => HookLog.DispatchCancelled(_logger, point.Id, eventArgs.DispatchId));
            SafeObserve(static () => HookMetrics.RecordDispatch("cancelled"));
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            SafeSetActivity(() => activity.SetFailed("failed", errorType));
            SafeLog(() => HookLog.DispatchFailed(_logger, point.Id, eventArgs.DispatchId, errorType));
            SafeObserve(static () => HookMetrics.RecordDispatch("failed"));
            throw;
        }
    }

    private async Task DispatchKernelCoreAsync<THook, TEventArgs>(
        HookPointDefinition<THook, TEventArgs> point,
        HookDispatchContext context,
        TEventArgs eventArgs,
        HookFailureMode? failurePolicyTightening,
        Activity? activity,
        CancellationToken cancellationToken)
        where THook : class
        where TEventArgs : AgentHookEventArgs
    {
        var registrations = ImmutableArray.CreateBuilder<HookRegistrationDescriptor>();
        foreach (var registration in context.Catalog.Registrations)
        {
            if (registration.Point.Equals(point.Id))
            {
                registrations.Add(registration);
            }
        }

        var order = _orderResolver.Resolve(registrations.ToImmutable());
        if (order is HookOrderInvalid invalid)
        {
            throw new HookCompositionException(string.Join("; ", invalid.Diagnostics));
        }

        var ordered = ((HookOrderResolved) order).Ordered;
        var hostBaseline = Strictest(point.FailureInvariant, _minimumFailureMode);
        if (failurePolicyTightening is { } tightening)
        {
            hostBaseline = Strictest(hostBaseline, tightening);
        }

        if (_profiles is not null)
        {
            var profileOptions = _profiles.GetRequired(context.Catalog.ProfileKey);
            if (profileOptions.DefaultRequestedFailureMode is { } profileMode)
            {
                hostBaseline = Strictest(hostBaseline, profileMode);
            }
        }

        var tracker = context.Activation.InvocationTracker;
        foreach (var registration in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var effectiveFailureMode = ResolveRegistrationFailureMode(point, hostBaseline, registration.RequestedFailureMode);
            var resolution = await context.Activation.ResolveAsync<THook>(registration.Id, cancellationToken).ConfigureAwait(false);
            if (resolution is HookInstanceUnavailable<THook> unavailable)
            {
                throw new HookCompositionException(unavailable.Reason);
            }

            var hook = ((HookInstanceResolved<THook>) resolution).Hook;
            var attempt = new HookInvocationAttempt(point.Id, context.Dispatch.DispatchId, registration.Reentrancy);
            var tracking = tracker.TryEnter(attempt);
            if (tracking is HookInvocationTrackingRejected)
            {
                var rejectedInvocation = new HookInvocationContext(
                    registration.Id,
                    _invocationIds.Create(),
                    context.Dispatch.DispatchId,
                    depth: 0);
                await PublishDiagnosticAsync(
                        point.Id,
                        rejectedInvocation,
                        HookInvocationOutcome.Rejected,
                        _timeProvider.GetUtcNow(),
                        TimeSpan.Zero,
                        cancellationToken)
                    .ConfigureAwait(false);
                throw new HookReentrancyException(
                    $"Hook point '{point.Id}' is already active at the maximum permitted depth on this activation lease.");
            }

            var depth = ((HookInvocationTrackingEntered) tracking).Depth;
            var invocation = new HookInvocationContext(
                registration.Id,
                _invocationIds.Create(),
                context.Dispatch.DispatchId,
                depth);
            var startedAt = _timeProvider.GetUtcNow();
            try
            {
                if (ShouldIsolate(point, effectiveFailureMode))
                {
                    var snapshot = eventArgs.CaptureMutableState();
                    try
                    {
                        await InvokeWithDeadlineAsync(
                                point,
                                hook,
                                eventArgs,
                                invocation,
                                context.Dispatch,
                                cancellationToken)
                            .ConfigureAwait(false);
                        point.Validator.Validate(eventArgs);
                        await PublishDiagnosticAsync(
                                point.Id,
                                invocation,
                                HookInvocationOutcome.Succeeded,
                                startedAt,
                                _timeProvider.GetUtcNow() - startedAt,
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception) when (IsDeadlineFault(exception, cancellationToken))
                    {
                        eventArgs.RestoreMutableState(snapshot);
                        point.Validator.Validate(eventArgs);
                        await PublishDiagnosticAsync(
                                point.Id,
                                invocation,
                                HookInvocationOutcome.IsolatedFault,
                                startedAt,
                                _timeProvider.GetUtcNow() - startedAt,
                                cancellationToken)
                            .ConfigureAwait(false);
                        SafeLog(() => HookLog.InvocationIsolated(
                            _logger,
                            point.Id,
                            new HookId(registration.Id.ToString()),
                            eventArgs.DispatchId,
                            nameof(TimeoutException)));
                        SafeSetActivity(() => activity?.AddEvent(new ActivityEvent(
                            "hook.failure.isolated",
                            tags: new ActivityTagsCollection
                            {
                                { AgentKitTagNames.Outcome, "isolated" },
                                { AgentKitTagNames.ErrorType, nameof(TimeoutException) },
                            })));
                        continue;
                    }
                    catch (Exception exception)
                    {
                        eventArgs.RestoreMutableState(snapshot);
                        point.Validator.Validate(eventArgs);
                        await PublishDiagnosticAsync(
                                point.Id,
                                invocation,
                                HookInvocationOutcome.IsolatedFault,
                                startedAt,
                                _timeProvider.GetUtcNow() - startedAt,
                                cancellationToken)
                            .ConfigureAwait(false);
                        var errorType = exception.GetType().FullName ?? exception.GetType().Name;
                        SafeLog(() => HookLog.InvocationIsolated(_logger, point.Id, new HookId(registration.Id.ToString()), eventArgs.DispatchId, errorType));
                        SafeSetActivity(() => activity?.AddEvent(new ActivityEvent(
                            "hook.failure.isolated",
                            tags: new ActivityTagsCollection
                            {
                                { AgentKitTagNames.Outcome, "isolated" },
                                { AgentKitTagNames.ErrorType, errorType },
                            })));
                        continue;
                    }
                }
                else
                {
                    await InvokeWithDeadlineAsync(
                            point,
                            hook,
                            eventArgs,
                            invocation,
                            context.Dispatch,
                            cancellationToken)
                        .ConfigureAwait(false);
                    point.Validator.Validate(eventArgs);
                    await PublishDiagnosticAsync(
                            point.Id,
                            invocation,
                            HookInvocationOutcome.Succeeded,
                            startedAt,
                            _timeProvider.GetUtcNow() - startedAt,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (IsDeadlineFault(exception, cancellationToken))
            {
                await PublishDiagnosticAsync(
                        point.Id,
                        invocation,
                        HookInvocationOutcome.Failed,
                        startedAt,
                        _timeProvider.GetUtcNow() - startedAt,
                        cancellationToken)
                    .ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"Hook '{registration.Id}' on point '{point.Id}' did not quiesce before its dispatch deadline.",
                    exception);
            }
            finally
            {
                tracker.Leave(context.Dispatch.DispatchId);
            }

            if (eventArgs is IShortCircuitingHookArgs { IsShortCircuited: true })
            {
                break;
            }
        }
    }

    private async ValueTask InvokeWithDeadlineAsync<THook, TEventArgs>(
        HookPointDefinition<THook, TEventArgs> point,
        THook hook,
        TEventArgs eventArgs,
        HookInvocationContext invocation,
        HookDispatchMetadata dispatch,
        CancellationToken cancellationToken)
        where THook : class
        where TEventArgs : AgentHookEventArgs
    {
        var hostDeadline = dispatch.Timestamp + _defaultHookTimeout;
        var effectiveDeadline = dispatch.Deadline < hostDeadline ? dispatch.Deadline : hostDeadline;
        var now = _timeProvider.GetUtcNow();
        var remaining = effectiveDeadline - now;
        if (remaining <= TimeSpan.Zero)
        {
            throw new TimeoutException("The hook dispatch deadline has already elapsed.");
        }

        using var deadlineSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadlineSource.CancelAfter(remaining);
        try
        {
            await point.Invoke(hook, eventArgs, invocation, deadlineSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The hook invocation exceeded its dispatch deadline.");
        }
    }

    private static bool IsDeadlineFault(Exception exception, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested && exception is TimeoutException;

    private async ValueTask PublishDiagnosticAsync(
        HookPointId point,
        HookInvocationContext invocation,
        HookInvocationOutcome outcome,
        DateTimeOffset startedAt,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        if (_diagnostics is null)
        {
            return;
        }

        var diagnostic = new HookInvocationDiagnostic(point, invocation, outcome, startedAt, duration);
        await _diagnostics.PublishAsync(diagnostic, cancellationToken).ConfigureAwait(false);
    }

    private static bool ShouldIsolate<THook, TEventArgs>(HookPointDefinition<THook, TEventArgs> point, HookFailureMode effectiveFailureMode)
        where THook : class
        where TEventArgs : AgentHookEventArgs =>
        point.Kind == HookPointKind.Observational && effectiveFailureMode == HookFailureMode.IsolateAndDiagnose;

    private static HookFailureMode ResolveRegistrationFailureMode<THook, TEventArgs>(
        HookPointDefinition<THook, TEventArgs> point,
        HookFailureMode hostBaseline,
        HookFailureMode registrationRequest)
        where THook : class
        where TEventArgs : AgentHookEventArgs
    {
        var mode = Strictest(hostBaseline, registrationRequest);
        return point.Kind == HookPointKind.Observational
            ? mode
            : HookFailureMode.FailOperation;
    }

    private static HookFailureMode Strictest(HookFailureMode requested, HookFailureMode minimum) =>
        requested == HookFailureMode.IsolateAndDiagnose && minimum == HookFailureMode.IsolateAndDiagnose
            ? HookFailureMode.IsolateAndDiagnose
            : HookFailureMode.FailOperation;

    private static void SafeSetActivity(Action observation)
    {
        try
        {
            observation();
        }
        catch (Exception)
        {
        }
    }

    private static void SafeLog(Action observation)
    {
        try
        {
            observation();
        }
        catch (Exception)
        {
        }
    }

    private static void SafeObserve(Action observation)
    {
        try
        {
            observation();
        }
        catch (Exception)
        {
        }
    }
}
