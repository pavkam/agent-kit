// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Runs at <see cref="AgentHookPoints.OutputValidating"/>, before a candidate is accepted.</summary>
/// <remarks>
/// A hook may veto a candidate that already passed schema validation by setting <see cref="OutputValidatingEventArgs.Reject"/>.
/// The loop dispatches this point with <see cref="HookFailureMode.FailOperation"/> so a throwing hook fails the turn.
/// </remarks>
public interface IOutputValidatingHook
{
    /// <summary>Inspects a provisional output candidate and optionally vetoes acceptance.</summary>
    /// <param name="args">The candidate and veto surface.</param>
    /// <param name="context">This invocation's registration and dispatch identities.</param>
    /// <param name="cancellationToken">Cancels the hook; cancellation propagates to the run.</param>
    /// <returns>A task that completes when the hook has finished.</returns>
    public ValueTask InvokeAsync(
        OutputValidatingEventArgs args,
        HookInvocationContext context,
        CancellationToken cancellationToken = default);
}
