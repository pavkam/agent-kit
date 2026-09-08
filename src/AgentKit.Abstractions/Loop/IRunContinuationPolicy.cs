// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Proposes the next state for one accepted run from an immutable continuation snapshot.</summary>
/// <remarks>The session owner revalidates and commits any proposal; implementations neither mutate session state nor reserve resources.</remarks>
public interface IRunContinuationPolicy
{
    /// <summary>Evaluates the captured continuation evidence.</summary>
    /// <param name="context">The immutable safe-boundary snapshot to evaluate.</param>
    /// <param name="cancellationToken">Cancels this uncommitted policy evaluation.</param>
    /// <returns>A proposal to continue, complete successfully, or halt.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<RunContinuationDecision> DecideAsync(
        RunContinuationContext context,
        CancellationToken cancellationToken = default);
}
