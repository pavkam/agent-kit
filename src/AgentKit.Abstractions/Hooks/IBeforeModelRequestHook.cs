// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Runs at <see cref="AgentHookPoints.BeforeModelRequest"/>, before each model request of a run.</summary>
/// <remarks>
/// A hook may inspect the assembled request and narrow its settings; it cannot change messages, tools, or the
/// model. The loop dispatches this point with <see cref="HookFailureMode.FailOperation"/>: a throwing hook fails
/// the turn. Register implementations additively with an explicit <see cref="HookRegistrationDescriptor"/> per registration.
/// </remarks>
public interface IBeforeModelRequestHook
{
    /// <summary>Inspects, and optionally narrows, the request about to be sent.</summary>
    /// <param name="args">The request and its writable settings.</param>
    /// <param name="context">This invocation's registration and dispatch identities.</param>
    /// <param name="cancellationToken">Cancels the hook; cancellation propagates to the run.</param>
    /// <returns>A task that completes when the hook has finished.</returns>
    public ValueTask InvokeAsync(
        BeforeModelRequestEventArgs args,
        HookInvocationContext context,
        CancellationToken cancellationToken = default);
}
