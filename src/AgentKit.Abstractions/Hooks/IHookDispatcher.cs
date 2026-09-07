// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Resolves a set of registered hooks for one hook point into a
/// deterministic order and invokes them sequentially against one
/// <see cref="AgentHookEventArgs"/> instance, validating after every
/// invocation and honoring typed short-circuiting.
/// </summary>
/// <remarks>
/// <para>
/// Runtime components depend on this contract, never on the concrete
/// dispatcher package: <c>AgentKit.Hooks</c> supplies the first-party
/// implementation and its <c>AddAgentHooks</c> registration, but a
/// replacement implementation is expected to satisfy the same ordering,
/// mutation, failure, cancellation, scope, and diagnostics behavior.
/// </para>
/// <para>
/// A dispatcher builds its ordering catalog fresh from the <c>hooks</c>
/// sequence supplied to each call rather than caching one across calls;
/// callers that want a stable catalog for the
/// lifetime of a run capture and reuse the same <c>IEnumerable&lt;THook&gt;</c>
/// snapshot (for example, a value resolved once from dependency injection
/// at the start of a run) rather than re-resolving a fresh, potentially
/// different enumerable on every dispatch.
/// </para>
/// </remarks>
public interface IHookDispatcher
{
    /// <summary>Dispatches one hook point to every hook in <paramref name="hooks"/>, in resolved order.</summary>
    /// <typeparam name="THook">The hook interface for this point.</typeparam>
    /// <typeparam name="TArgs">The event-argument type for this point.</typeparam>
    /// <param name="point">The hook point being dispatched.</param>
    /// <param name="hooks">The hooks registered for <paramref name="point"/>.</param>
    /// <param name="args">The event arguments passed to every hook, mutated in place.</param>
    /// <param name="invoke">Invokes one hook against <paramref name="args"/> and the scope for any nested dispatch it performs.</param>
    /// <param name="scope">The reentrancy scope for this call path; pass <see cref="HookDispatchScope.Root"/> for a fresh top-level dispatch.</param>
    /// <param name="failureMode">Whether a hook exception fails the whole dispatch or is isolated.</param>
    /// <param name="maxReentrantDepth">
    /// The maximum number of dispatches for <paramref name="point"/> permitted to be simultaneously active on
    /// <paramref name="scope"/>'s call path. The default of 1 forbids any reentrant dispatch of the same point.
    /// </param>
    /// <param name="cancellationToken">A token used to cancel remaining hook invocations.</param>
    /// <returns>A task that completes when every hook has run, dispatch was short-circuited, or failure ended it.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="hooks"/>, <paramref name="args"/>, <paramref name="invoke"/>, or <paramref name="scope"/> is null.
    /// </exception>
    /// <exception cref="HookCompositionException">
    /// <paramref name="hooks"/> cannot be resolved into one deterministic order: a duplicate
    /// <see cref="HookId"/>, an ordering cycle, a missing <see cref="IHook.DependsOn"/> target, or more than one
    /// hook declaring <see cref="HookPriority.First"/> or <see cref="HookPriority.Last"/>.
    /// </exception>
    /// <exception cref="HookReentrancyException">
    /// <paramref name="point"/> is already active on <paramref name="scope"/>'s call path at or beyond
    /// <paramref name="maxReentrantDepth"/>.
    /// </exception>
    /// <exception cref="HookValidationException">
    /// A hook left <paramref name="args"/> in a state <see cref="AgentHookEventArgs.Validate"/> rejects.
    /// </exception>
    public Task DispatchAsync<THook, TArgs>(
        HookPointId point,
        IEnumerable<THook> hooks,
        TArgs args,
        Func<THook, TArgs, HookDispatchScope, CancellationToken, Task> invoke,
        HookDispatchScope scope,
        HookFailureMode failureMode = HookFailureMode.FailOperation,
        int maxReentrantDepth = 1,
        CancellationToken cancellationToken = default)
        where THook : IHook
        where TArgs : AgentHookEventArgs;
}
