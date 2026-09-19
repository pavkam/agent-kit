// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

/// <summary>
/// The first-party <see cref="IHookDispatcher"/>: resolves a deterministic
/// order via <see cref="HookOrdering"/>, invokes hooks sequentially,
/// validates event arguments after every invocation, honors typed
/// short-circuiting, and enforces an explicit, non-ambient reentrancy
/// bound.
/// </summary>
/// <remarks>
/// <para>
/// This class holds no mutable state and no per-hook-point cache: every
/// call to <see cref="DispatchAsync{THook,TArgs}"/> resolves ordering fresh
/// from the <c>hooks</c> sequence it is given, so it is safe to register as
/// a singleton and share across every run and hook point in a process.
/// </para>
/// <para>
/// The host ceilings from <see cref="AgentHookOptions"/> are captured once at
/// construction and compose monotonically with each call's arguments: the
/// effective reentrancy limit is the smaller of the caller's
/// <c>maxReentrantDepth</c> and <see cref="AgentHookOptions.MaximumInvocationDepth"/>,
/// and the effective failure mode is the stricter of the caller's
/// <c>failureMode</c> and <see cref="AgentHookOptions.MinimumFailureMode"/>.
/// </para>
/// </remarks>
public sealed class DefaultHookDispatcher: IHookDispatcher
{
    private readonly ILogger<DefaultHookDispatcher> _logger;
    private readonly int _maximumInvocationDepth;
    private readonly HookFailureMode _minimumFailureMode;

    /// <summary>
    /// Initializes a dispatcher with the default <see cref="AgentHookOptions"/> ceilings, which leave every
    /// caller's requested depth and failure mode unchanged.
    /// </summary>
    /// <param name="logger">The optional structured logger; a null value disables log publication.</param>
    public DefaultHookDispatcher(ILogger<DefaultHookDispatcher>? logger = null)
        : this(Options.Create(new AgentHookOptions()), logger)
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
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/> or its <see cref="IOptions{TOptions}.Value"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="AgentHookOptions.MaximumInvocationDepth"/> is less than 1, or
    /// <see cref="AgentHookOptions.MinimumFailureMode"/> is not a defined <see cref="HookFailureMode"/>. The
    /// exception names <paramref name="options"/>.
    /// </exception>
    public DefaultHookDispatcher(IOptions<AgentHookOptions> options, ILogger<DefaultHookDispatcher>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Value, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Value.MaximumInvocationDepth, 1, nameof(options));
        ArgumentOutOfRangeException.ThrowIfUndefined(options.Value.MinimumFailureMode, nameof(options));

        _logger = logger ?? NullLogger<DefaultHookDispatcher>.Instance;
        _maximumInvocationDepth = options.Value.MaximumInvocationDepth;
        _minimumFailureMode = options.Value.MinimumFailureMode;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Before any hook runs, <paramref name="maxReentrantDepth"/> is clamped to the host's
    /// <see cref="AgentHookOptions.MaximumInvocationDepth"/> and <paramref name="failureMode"/> is escalated to the
    /// host's <see cref="AgentHookOptions.MinimumFailureMode"/> when that is stricter. Either adjustment is recorded
    /// as a content-free debug log event; neither can relax what the caller requested.
    /// </remarks>
    public async Task DispatchAsync<THook, TArgs>(
        HookPointId point,
        IEnumerable<THook> hooks,
        TArgs args,
        Func<THook, TArgs, HookDispatchScope, CancellationToken, Task> invoke,
        HookDispatchScope scope,
        HookFailureMode failureMode = HookFailureMode.FailOperation,
        int maxReentrantDepth = 1,
        CancellationToken cancellationToken = default)
        where THook : IHook
        where TArgs : AgentHookEventArgs
    {
        ArgumentNullException.ThrowIfNull(hooks);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(invoke);
        ArgumentNullException.ThrowIfNull(scope);

        var scoped = args as AgentScopedHookEventArgs;
        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.HookDispatch,
            ActivityKind.Internal,
            new KeyValuePair<string, object?>[]
            {
                new(AgentKitTagNames.HookPoint, point.ToString()),
                new(AgentKitTagNames.HookDispatchId, args.DispatchId.ToString()),
                new(AgentKitTagNames.AgentId, scoped?.AgentId.ToString()),
                new(AgentKitTagNames.SessionId, scoped?.SessionId?.ToString()),
                new(AgentKitTagNames.OperationId, args.Correlation.OperationId.ToString()),
            });
        var activity = activityScope.Activity;

        var effectiveDepth = Math.Min(maxReentrantDepth, _maximumInvocationDepth);
        if (effectiveDepth != maxReentrantDepth)
        {
            SafeLog(() => HookLog.ReentrantDepthClamped(_logger, point, args.DispatchId, maxReentrantDepth, effectiveDepth));
        }

        var effectiveFailureMode = Strictest(failureMode, _minimumFailureMode);
        if (effectiveFailureMode != failureMode)
        {
            SafeLog(() => HookLog.FailureModeEscalated(_logger, point, args.DispatchId, failureMode, effectiveFailureMode));
        }

        try
        {
            await DispatchCoreAsync(
                point, hooks, args, invoke, scope, effectiveFailureMode, effectiveDepth, activity, cancellationToken)
                .ConfigureAwait(false);
            var outcome = args is IShortCircuitingHookArgs { IsShortCircuited: true } ? "short_circuited" : "completed";
            SafeSetActivity(() => activity.SetSuccessful(outcome));
            SafeLog(() => HookLog.DispatchCompleted(_logger, point, args.DispatchId));
            SafeObserve(static () => HookMetrics.RecordDispatch("completed"));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SafeSetActivity(() => activity.SetFailed("cancelled", nameof(OperationCanceledException)));
            SafeLog(() => HookLog.DispatchCancelled(_logger, point, args.DispatchId));
            SafeObserve(static () => HookMetrics.RecordDispatch("cancelled"));
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            SafeSetActivity(() => activity.SetFailed("failed", errorType));
            SafeLog(() => HookLog.DispatchFailed(_logger, point, args.DispatchId, errorType));
            SafeObserve(static () => HookMetrics.RecordDispatch("failed"));
            throw;
        }
    }

    /// <summary>Runs one activity mutation, containing a hostile diagnostics listener so it cannot fail the dispatch.</summary>
    private static void SafeSetActivity(Action observation)
    {
        try
        {
            observation();
        }
        catch (Exception)
        {
            // Instrumentation is observational only; a listener failure must never alter the dispatch outcome.
        }
    }

    /// <summary>Runs one log call, containing a hostile logging provider so it cannot fail the dispatch.</summary>
    private static void SafeLog(Action observation)
    {
        try
        {
            observation();
        }
        catch (Exception)
        {
            // Instrumentation is observational only; a logging-provider failure must never alter the dispatch outcome.
        }
    }

    /// <summary>Runs one metrics call, containing a hostile measurement callback so it cannot fail the dispatch.</summary>
    private static void SafeObserve(Action observation)
    {
        try
        {
            observation();
        }
        catch (Exception)
        {
            // Instrumentation is observational only; a meter-listener failure must never alter the dispatch outcome.
        }
    }

    /// <summary>
    /// Composes two failure modes under the strictness order <see cref="HookFailureMode.IsolateAndDiagnose"/> &lt;
    /// <see cref="HookFailureMode.FailOperation"/>. Only two isolating inputs yield isolation; any other input,
    /// including an undefined caller value, fails closed to <see cref="HookFailureMode.FailOperation"/>.
    /// </summary>
    /// <param name="requested">The mode requested by the caller of this dispatch.</param>
    /// <param name="minimum">The host's minimum mode captured at construction.</param>
    /// <returns>The stricter of the two modes.</returns>
    private static HookFailureMode Strictest(HookFailureMode requested, HookFailureMode minimum) =>
        requested == HookFailureMode.IsolateAndDiagnose && minimum == HookFailureMode.IsolateAndDiagnose
            ? HookFailureMode.IsolateAndDiagnose
            : HookFailureMode.FailOperation;

    private async Task DispatchCoreAsync<THook, TArgs>(
        HookPointId point,
        IEnumerable<THook> hooks,
        TArgs args,
        Func<THook, TArgs, HookDispatchScope, CancellationToken, Task> invoke,
        HookDispatchScope scope,
        HookFailureMode failureMode,
        int maxReentrantDepth,
        Activity? activity,
        CancellationToken cancellationToken)
        where THook : IHook
        where TArgs : AgentHookEventArgs
    {

        var activeDepth = scope.DepthOf(point);
        if (activeDepth >= maxReentrantDepth)
        {
            throw new HookReentrancyException(
                $"Hook point '{point}' is already active at depth {activeDepth} on this call path (maximum {maxReentrantDepth}).");
        }

        var materialized = hooks as IReadOnlyList<THook> ?? [.. hooks];
        var ordered = HookOrdering.Sort(materialized);
        var nestedScope = scope.Entering(point);

        foreach (var hook in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (failureMode == HookFailureMode.IsolateAndDiagnose)
            {
                // Isolation must not leak a partial mutation, replacement value, or short-circuit marker from a
                // hook that failed midway: the permitted writable state is captured before the hook runs and
                // restored if it throws, so later hooks and the owning operation observe the pre-hook state.
                var snapshot = args.CaptureMutableState();
                try
                {
                    await invoke(hook, args, nestedScope, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    // Restore before any diagnostics call: if a hostile logger or listener throws, the
                    // isolated hook's partial mutation must already be rolled back, not leaked because
                    // the exception propagated out of this catch block before restoration ran.
                    args.RestoreMutableState(snapshot);
                    args.Validate();
                    var errorType = exception.GetType().FullName ?? exception.GetType().Name;
                    SafeLog(() => HookLog.InvocationIsolated(_logger, point, hook.Id, args.DispatchId, errorType));
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
                await invoke(hook, args, nestedScope, cancellationToken).ConfigureAwait(false);
            }

            args.Validate();

            if (args is IShortCircuitingHookArgs { IsShortCircuited: true })
            {
                break;
            }
        }
    }
}
