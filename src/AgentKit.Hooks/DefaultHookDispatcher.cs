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
/// This class holds no mutable state and no per-hook-point cache: every
/// call to <see cref="DispatchAsync{THook,TArgs}"/> resolves ordering fresh
/// from the <c>hooks</c> sequence it is given, so it is safe to register as
/// a singleton and share across every run and hook point in a process.
/// </remarks>
/// <param name="logger">The optional structured logger; a null value disables log publication.</param>
public sealed class DefaultHookDispatcher(ILogger<DefaultHookDispatcher>? logger = null): IHookDispatcher
{
    private readonly ILogger<DefaultHookDispatcher> _logger = logger ?? NullLogger<DefaultHookDispatcher>.Instance;

    /// <inheritdoc/>
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

        using var activity = AgentKitDiagnostics.Activities.StartActivity(AgentKitActivityNames.HookDispatch);
        _ = activity?.SetTag(AgentKitTagNames.HookPoint, point.ToString());
        _ = activity?.SetTag(AgentKitTagNames.HookInvocationId, args.InvocationId.ToString());
        _ = activity?.SetTag(AgentKitTagNames.AgentId, args.AgentId.ToString());
        _ = activity?.SetTag(AgentKitTagNames.SessionId, args.SessionId?.ToString());
        _ = activity?.SetTag(AgentKitTagNames.OperationId, args.Correlation.OperationId.ToString());

        try
        {
            await DispatchCoreAsync(
                point, hooks, args, invoke, scope, failureMode, maxReentrantDepth, activity, cancellationToken)
                .ConfigureAwait(false);
            activity.SetSuccessful(args is IShortCircuitingHookArgs { IsShortCircuited: true } ? "short_circuited" : "completed");
            HookLog.DispatchCompleted(_logger, point, args.InvocationId);
            HookMetrics.RecordDispatch("completed");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", nameof(OperationCanceledException));
            HookLog.DispatchCancelled(_logger, point, args.InvocationId);
            HookMetrics.RecordDispatch("cancelled");
            throw;
        }
        catch (Exception exception)
        {
            activity.SetFailed("failed", exception.GetType().FullName ?? exception.GetType().Name);
            HookLog.DispatchFailed(
                _logger,
                point,
                args.InvocationId,
                exception.GetType().FullName ?? exception.GetType().Name);
            HookMetrics.RecordDispatch("failed");
            throw;
        }
    }

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

            if (failureMode == HookFailureMode.Isolate)
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
                    HookLog.InvocationIsolated(
                        _logger,
                        point,
                        hook.Id,
                        args.InvocationId,
                        exception.GetType().FullName ?? exception.GetType().Name);
                    _ = activity?.AddEvent(new ActivityEvent(
                        "hook.failure.isolated",
                        tags: new ActivityTagsCollection
                        {
                            { AgentKitTagNames.Outcome, "isolated" },
                            { AgentKitTagNames.ErrorType, exception.GetType().FullName ?? exception.GetType().Name },
                        }));
                    args.RestoreMutableState(snapshot);
                    args.Validate();
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
