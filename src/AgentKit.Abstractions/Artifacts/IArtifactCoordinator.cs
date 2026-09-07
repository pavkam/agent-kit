// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Coordinates bounded artifact staging, atomic publication, and compensating abort.</summary>
public interface IArtifactCoordinator
{
    /// <summary>Stages complete content without making it readable.</summary>
    /// <param name="request">The bounded staging request.</param>
    /// <param name="cancellationToken">Cancels staging before publication.</param>
    /// <returns>A staging receipt or typed rejection.</returns>
    public Task<ArtifactPrepareResult> PrepareAsync(ArtifactPrepareRequest request, CancellationToken cancellationToken = default);

    /// <summary>Atomically publishes one valid staged preparation.</summary>
    /// <param name="request">The publication request.</param>
    /// <param name="cancellationToken">Cancels before the atomic publication point.</param>
    /// <returns>The committed reference or typed rejection.</returns>
    public ValueTask<ArtifactFinalizeResult> FinalizeAsync(ArtifactFinalizeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Idempotently removes unpublished staging content.</summary>
    /// <param name="request">The compensating abort request.</param>
    /// <param name="cancellationToken">Cancels before removal.</param>
    /// <returns>Absence confirmation or typed rejection.</returns>
    public ValueTask<ArtifactAbortResult> AbortAsync(ArtifactAbortRequest request, CancellationToken cancellationToken = default);

    /// <summary>Opens one exact committed artifact version through the selected protected backend.</summary>
    /// <param name="request">The committed-artifact read request.</param>
    /// <param name="cancellationToken">Cancels before the stream is exposed.</param>
    /// <returns>An owned readable stream or typed rejection.</returns>
    public ValueTask<ArtifactReadResult> ReadAsync(ArtifactReadRequest request, CancellationToken cancellationToken = default);

    /// <summary>Idempotently deletes one exact committed version when retention permits.</summary>
    /// <param name="request">The committed-artifact deletion request.</param>
    /// <param name="cancellationToken">Cancels before deletion commits.</param>
    /// <returns>Absence confirmation or typed rejection.</returns>
    public ValueTask<ArtifactDeleteResult> DeleteAsync(ArtifactDeleteRequest request, CancellationToken cancellationToken = default);
}
