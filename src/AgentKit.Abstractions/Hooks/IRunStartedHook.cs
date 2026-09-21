// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Observes <see cref="AgentHookPoints.RunStarted"/>: a run has been admitted and is about to drive its first turn.</summary>
/// <remarks>
/// This is a read-only point. The loop dispatches it with <see cref="HookFailureMode.IsolateAndDiagnose"/>, so a hook that
/// throws is logged and skipped unless the host's minimum failure mode is stricter; cancellation always propagates.
/// Register implementations additively with an explicit <see cref="HookRegistrationDescriptor"/> per registration.
/// </remarks>
public interface IRunStartedHook
{
    /// <summary>Observes the run start.</summary>
    /// <param name="args">The run's established identities and bounds.</param>
    /// <param name="context">This invocation's registration and dispatch identities.</param>
    /// <param name="cancellationToken">Cancels the observation; cancellation propagates to the run.</param>
    /// <returns>A task that completes when the hook has finished.</returns>
    public ValueTask InvokeAsync(
        RunStartedEventArgs args,
        HookInvocationContext context,
        CancellationToken cancellationToken = default);
}
