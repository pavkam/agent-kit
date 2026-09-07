// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Persists unpublished and committed artifact bytes behind an exact security boundary.</summary>
public interface IArtifactStore
{
    /// <summary>Gets the backend audience that consumes artifact grants.</summary>
    public ComponentId SecurityAudience { get; }

    /// <summary>Stages validated complete bytes without publishing them.</summary>
    /// <param name="request">The exact authorized staging request.</param>
    /// <param name="cancellationToken">Cancels before staging commits.</param>
    /// <returns>A staging receipt or typed rejection.</returns>
    public ValueTask<ArtifactPrepareResult> PrepareAsync(ArtifactStorePrepareRequest request, CancellationToken cancellationToken = default);

    /// <summary>Atomically publishes one staged preparation.</summary>
    /// <param name="request">The exact authorized publication request.</param>
    /// <param name="cancellationToken">Cancels before publication commits.</param>
    /// <returns>The portable committed reference or typed rejection.</returns>
    public ValueTask<ArtifactFinalizeResult> FinalizeAsync(ArtifactStoreFinalizeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Idempotently removes unpublished staging content.</summary>
    /// <param name="request">The exact authorized abort request.</param>
    /// <param name="cancellationToken">Cancels before removal.</param>
    /// <returns>Absence confirmation or typed rejection.</returns>
    public ValueTask<ArtifactAbortResult> AbortAsync(ArtifactStoreAbortRequest request, CancellationToken cancellationToken = default);

    /// <summary>Opens committed bytes after consuming exact read authority.</summary>
    /// <param name="request">The exact authorized read request.</param>
    /// <param name="cancellationToken">Cancels before the stream is exposed.</param>
    /// <returns>An owned readable stream or typed rejection.</returns>
    public ValueTask<ArtifactReadResult> ReadAsync(ArtifactStoreReadRequest request, CancellationToken cancellationToken = default);

    /// <summary>Idempotently deletes committed bytes after consuming exact delete authority.</summary>
    /// <param name="request">The exact authorized deletion request.</param>
    /// <param name="cancellationToken">Cancels before deletion commits.</param>
    /// <returns>Absence confirmation or typed rejection.</returns>
    public ValueTask<ArtifactDeleteResult> DeleteAsync(ArtifactStoreDeleteRequest request, CancellationToken cancellationToken = default);
}
