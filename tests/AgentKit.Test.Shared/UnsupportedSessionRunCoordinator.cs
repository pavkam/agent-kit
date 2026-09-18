// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>
/// An <see cref="ISessionRunCoordinator"/> test double that throws
/// <see cref="NotSupportedException"/> from its only member.
/// </summary>
/// <remarks>
/// Useful as a placeholder collaborator for tests whose scripted
/// <see cref="IAgentLoop"/> or admission path never actually acquires a
/// distributed run lease, so the composed <see cref="AgentRunServices"/>
/// bundle or a <see cref="SessionExecutionCapability"/> can still be
/// constructed without a behaviorally meaningful fake.
/// </remarks>
public sealed class UnsupportedSessionRunCoordinator: ISessionRunCoordinator
{
    /// <inheritdoc/>
    public ValueTask<SessionRunLeaseResult> AcquireAsync(
        SessionRunLeaseRequest request, SessionExecutionCapability session, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This test double does not support run-lease acquisition.");
}
