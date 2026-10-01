// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Persists unpublished and committed artifact bytes behind an exact security boundary.</summary>
/// <remarks>
/// Every operation atomically validates and consumes its single-use grant immediately before touching state. Stores are
/// tenant-partitioned, never expose unpublished staging as committed content, and return typed store-level results; the
/// coordinator maps them to portable results. Implementations may be singleton only when thread-safe and free of request state.
/// </remarks>
public interface IArtifactStore
{
    /// <summary>Gets the backend audience that consumes artifact grants.</summary>
    /// <value>A nondefault component identity that every grant presented to this store must name.</value>
    public ComponentId SecurityAudience { get; }

    /// <summary>Stages validated complete bytes without publishing them.</summary>
    /// <param name="request">The exact authorized staging request.</param>
    /// <param name="cancellationToken">Cancels before staging commits.</param>
    /// <returns>A staging receipt or typed rejection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before staging commits.</exception>
    public Task<ArtifactStorePrepareResult> PrepareAsync(ArtifactStorePrepareRequest request, CancellationToken cancellationToken = default);

    /// <summary>Atomically publishes one staged preparation.</summary>
    /// <param name="request">The exact authorized publication request.</param>
    /// <param name="cancellationToken">Cancels before publication commits.</param>
    /// <returns>The portable committed reference or typed rejection; an equivalent retry returns the same reference.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before publication commits.</exception>
    public ValueTask<ArtifactStoreFinalizeResult> FinalizeAsync(ArtifactStoreFinalizeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Idempotently removes unpublished staging content.</summary>
    /// <param name="request">The exact authorized abort request.</param>
    /// <param name="cancellationToken">Cancels before removal.</param>
    /// <returns>Absence confirmation or typed rejection; a finalized preparation is never aborted as unfinished staging.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before removal.</exception>
    public ValueTask<ArtifactStoreAbortResult> AbortAsync(ArtifactStoreAbortRequest request, CancellationToken cancellationToken = default);

    /// <summary>Opens committed bytes after consuming exact read authority.</summary>
    /// <param name="request">The exact authorized read request.</param>
    /// <param name="cancellationToken">Cancels before the stream is exposed.</param>
    /// <returns>An owned readable stream or typed rejection; a tombstoned, foreign, or mismatched reference is unavailable.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the stream is exposed.</exception>
    public Task<ArtifactStoreReadResult> ReadAsync(ArtifactStoreReadRequest request, CancellationToken cancellationToken = default);

    /// <summary>Idempotently deletes committed bytes after consuming exact delete authority.</summary>
    /// <param name="request">The exact authorized deletion request.</param>
    /// <param name="cancellationToken">Cancels before deletion commits.</param>
    /// <returns>Absence confirmation or typed rejection; legal hold and external ownership without delegated delete authority reject.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before deletion commits.</exception>
    public ValueTask<ArtifactStoreDeleteResult> DeleteAsync(ArtifactStoreDeleteRequest request, CancellationToken cancellationToken = default);
}
