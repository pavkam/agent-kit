// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>
/// An <see cref="ISessionCoordinator"/> test double that throws
/// <see cref="NotSupportedException"/> from every mandatory member.
/// </summary>
/// <remarks>
/// Useful as a placeholder collaborator for tests whose scripted
/// <see cref="IAgentLoop"/> never actually reaches session coordination, so
/// the composed <see cref="AgentRunServices"/> bundle can still be
/// resolved without a behaviorally meaningful fake.
/// </remarks>
public sealed class UnsupportedSessionCoordinator: ISessionCoordinator
{
    /// <inheritdoc/>
    public ValueTask<SessionCreateResult> CreateAsync(
        SessionCreateRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This test double does not support session creation.");

    /// <inheritdoc/>
    public ValueTask<SessionLoadResult> LoadAsync(
        SessionOperationContext context, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This test double does not support session loading.");

    /// <inheritdoc/>
    public ValueTask<SessionAppendResult> AppendAsync(
        SessionAppendRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This test double does not support session appends.");

    /// <inheritdoc/>
    public ValueTask<SessionPageResult> ReadAsync(
        SessionReadRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This test double does not support session reads.");

    /// <inheritdoc/>
    public ValueTask<SessionBranchResult> BranchAsync(
        SessionBranchRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This test double does not support branching.");

    /// <inheritdoc/>
    public ValueTask<SessionDeleteResult> DeleteAsync(
        SessionDeleteRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This test double does not support session deletion.");
}
