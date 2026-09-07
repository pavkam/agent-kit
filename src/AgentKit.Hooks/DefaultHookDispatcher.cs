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
public sealed class DefaultHookDispatcher: IHookDispatcher
{
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
                try
                {
                    await invoke(hook, args, nestedScope, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
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
