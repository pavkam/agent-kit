// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Dispatches one closed hook point against a captured catalog, activation lease, and shared event arguments.
/// </summary>
/// <remarks>
/// Runtime components depend on this contract, never on the concrete dispatcher package:
/// <c>AgentKit.Hooks</c> supplies the first-party implementation and its <c>AddAgentHooks</c> registration, but a
/// replacement implementation is expected to satisfy the same ordering, mutation, failure, cancellation, and
/// diagnostics behavior.
/// </remarks>
public interface IHookDispatcher
{
    /// <summary>
    /// Dispatches one closed hook point against a captured catalog, activation lease, and shared event arguments.
    /// </summary>
    /// <typeparam name="THook">The closed hook interface for this point.</typeparam>
    /// <typeparam name="TEventArgs">The closed event-argument type for this point.</typeparam>
    /// <param name="point">The closed point definition whose identity and invariants govern this dispatch.</param>
    /// <param name="context">The captured catalog, dispatch metadata, and activation lease for this emission.</param>
    /// <param name="eventArgs">The event arguments passed to every hook, mutated in place when the point is mutating.</param>
    /// <param name="failurePolicyTightening">
    /// An optional failure-mode tightening applied monotonically with the point invariant, host minimum, and each
    /// registration's requested mode. Absent means no extra tightening beyond those sources.
    /// </param>
    /// <param name="cancellationToken">A token used to cancel remaining hook invocations.</param>
    /// <returns>A task that completes when every hook has run, dispatch was short-circuited, or failure ended it.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="point"/>, <paramref name="context"/>, or <paramref name="eventArgs"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="point"/>'s identity does not match <paramref name="context"/> or <paramref name="eventArgs"/>.
    /// </exception>
    /// <exception cref="HookCompositionException">
    /// Registrations for this point cannot be ordered, a registration cannot be resolved, or activation failed.
    /// </exception>
    /// <exception cref="HookReentrancyException">
    /// Entering the point would exceed the permitted reentrant depth on this activation lease.
    /// </exception>
    /// <exception cref="HookValidationException">
    /// A hook left <paramref name="eventArgs"/> in a state the point validator rejects.
    /// </exception>
    public ValueTask DispatchAsync<THook, TEventArgs>(
        HookPointDefinition<THook, TEventArgs> point,
        HookDispatchContext context,
        TEventArgs eventArgs,
        HookFailureMode? failurePolicyTightening = null,
        CancellationToken cancellationToken = default)
        where THook : class
        where TEventArgs : AgentHookEventArgs;
}
