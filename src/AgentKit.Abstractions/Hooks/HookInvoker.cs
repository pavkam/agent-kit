// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Invokes one closed hook interface's dedicated method against shared event arguments.</summary>
/// <typeparam name="THook">The closed hook interface for this point.</typeparam>
/// <typeparam name="TEventArgs">The closed event-argument type for this point.</typeparam>
/// <param name="hook">The resolved hook instance to invoke.</param>
/// <param name="eventArgs">The event arguments shared across every hook in this dispatch, mutated in place.</param>
/// <param name="invocation">This individual execution's identity and depth.</param>
/// <param name="cancellationToken">Cancels the invocation; cancellation always propagates to the owning operation.</param>
/// <returns>A task that completes when the hook has finished.</returns>
/// <remarks>
/// A closed <see cref="HookPointDefinition{THook,TEventArgs}"/> supplies exactly one invoker, typically a static
/// lambda calling the hook interface's own dedicated method (for example
/// <c>(hook, args, invocation, ct) =&gt; hook.InvokeAsync(args, invocation, ct)</c>). This is the only place the
/// dispatch kernel calls into a hook implementation; it never receives an untyped delegate over an arbitrary
/// object payload.
/// </remarks>
public delegate ValueTask HookInvoker<THook, TEventArgs>(
    THook hook,
    TEventArgs eventArgs,
    HookInvocationContext invocation,
    CancellationToken cancellationToken)
    where THook : class
    where TEventArgs : AgentHookEventArgs;
