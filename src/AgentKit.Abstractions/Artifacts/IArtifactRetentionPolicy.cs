// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Resolves retention when content is staged and decides whether a committed version may be deleted.</summary>
/// <remarks>The policy is stateless and thread-safe. A resolved decision is captured before authorization so the grant binds the exact retention the reference will carry.</remarks>
public interface IArtifactRetentionPolicy
{
    /// <summary>Resolves the retention a new preparation and its finalized reference carry.</summary>
    /// <param name="request">The requested and default retention.</param>
    /// <param name="cancellationToken">Cancels evaluation.</param>
    /// <returns>The resolved retention, or a typed retention conflict.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<ArtifactRetentionDecision> ResolveAsync(ArtifactRetentionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Decides whether one committed version may be deleted now.</summary>
    /// <param name="reference">The exact committed reference, whose retention is authoritative.</param>
    /// <param name="now">The evaluation instant.</param>
    /// <param name="cancellationToken">Cancels evaluation.</param>
    /// <returns>An allowing decision carrying the reference's retention, or a typed retention conflict.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<ArtifactRetentionDecision> EvaluateDeletionAsync(ArtifactReference reference, DateTimeOffset now, CancellationToken cancellationToken = default);
}
