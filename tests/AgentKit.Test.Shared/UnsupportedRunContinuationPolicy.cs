// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>
/// An <see cref="IRunContinuationPolicy"/> test double that throws
/// <see cref="NotSupportedException"/> whenever a continuation decision is
/// requested.
/// </summary>
/// <remarks>
/// Useful as a placeholder collaborator for tests whose scripted
/// <see cref="IAgentLoop"/> never actually reaches continuation evaluation,
/// so the composed <see cref="AgentRunServices"/> bundle can still be
/// resolved without a behaviorally meaningful fake.
/// </remarks>
public sealed class UnsupportedRunContinuationPolicy: IRunContinuationPolicy
{
    /// <inheritdoc/>
    public ValueTask<RunContinuationDecision> DecideAsync(
        RunContinuationContext context, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This test double does not support continuation evaluation.");
}
