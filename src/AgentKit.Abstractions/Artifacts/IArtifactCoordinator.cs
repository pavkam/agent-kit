// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Coordinates bounded artifact staging, atomic publication, compensating abort, and reference-commit reconciliation.</summary>
/// <remarks>
/// A coordinator is bound to one key and one logical artifact profile snapshot. It validates metadata and authority before
/// selecting a backend, never calls session, tool, or memory owners, and never makes a backend key, location, or signed URL part of
/// a portable reference.
/// </remarks>
public interface IArtifactCoordinator
{
    /// <summary>Stages complete content without making it readable.</summary>
    /// <param name="request">The bounded staging request.</param>
    /// <param name="cancellationToken">Cancels staging before publication.</param>
    /// <returns>A staging receipt or typed rejection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public Task<ArtifactPrepareResult> PrepareAsync(ArtifactPrepareRequest request, CancellationToken cancellationToken = default);

    /// <summary>Atomically publishes one valid staged preparation.</summary>
    /// <param name="request">The publication request.</param>
    /// <param name="cancellationToken">Cancels before the atomic publication point.</param>
    /// <returns>The committed reference or typed rejection; an equivalent retry returns the same reference.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<ArtifactFinalizeResult> FinalizeAsync(ArtifactFinalizeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Idempotently removes unpublished staging content.</summary>
    /// <param name="request">The compensating abort request.</param>
    /// <param name="cancellationToken">Cancels before removal.</param>
    /// <returns>Absence confirmation or typed rejection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<ArtifactAbortResult> AbortAsync(ArtifactAbortRequest request, CancellationToken cancellationToken = default);

    /// <summary>Opens one exact committed artifact version through the selected protected backend.</summary>
    /// <param name="request">The committed-artifact read request.</param>
    /// <param name="cancellationToken">Cancels before the stream is exposed.</param>
    /// <returns>An owned readable stream or typed rejection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public Task<ArtifactReadResult> ReadAsync(ArtifactReadRequest request, CancellationToken cancellationToken = default);

    /// <summary>Idempotently deletes one exact committed version when retention permits.</summary>
    /// <param name="request">The committed-artifact deletion request.</param>
    /// <param name="cancellationToken">Cancels before deletion commits.</param>
    /// <returns>Absence confirmation or typed rejection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<ArtifactDeleteResult> DeleteAsync(ArtifactDeleteRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reconciles one caller-owned reference-commit intent by preparation identity.</summary>
    /// <param name="request">The reconciliation request.</param>
    /// <param name="cancellationToken">Cancels before the next state transition.</param>
    /// <returns>
    /// A terminal disposition, a pending result that conservatively retains the object, or a typed rejection. Reconciliation first
    /// fences out a late reference commit, then collects only when the intent is terminally not committed.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<ArtifactReconciliationResult> ReconcileAsync(ArtifactReconciliationRequest request, CancellationToken cancellationToken = default);
}
