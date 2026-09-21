// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Observes <see cref="AgentHookPoints.ContextAssembled"/> after context assembly succeeds for one model request.</summary>
/// <remarks>
/// This is a read-only point. The loop dispatches it with <see cref="HookFailureMode.IsolateAndDiagnose"/> so a hook
/// failure is logged and skipped unless the host's minimum failure mode is stricter; cancellation always propagates.
/// </remarks>
public interface IContextAssembledHook
{
    /// <summary>Observes the assembled context.</summary>
    /// <param name="args">The assembled request and repair evidence.</param>
    /// <param name="context">This invocation's registration and dispatch identities.</param>
    /// <param name="cancellationToken">Cancels the observation; cancellation propagates to the run.</param>
    /// <returns>A task that completes when the hook has finished.</returns>
    public ValueTask InvokeAsync(
        ContextAssembledEventArgs args,
        HookInvocationContext context,
        CancellationToken cancellationToken = default);
}
