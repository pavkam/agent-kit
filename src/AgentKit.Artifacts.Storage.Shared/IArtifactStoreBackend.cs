// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Storage;

/// <summary>Executes artifact lifecycle operations over one adapter's authoritative state after the gateway consumed the grant.</summary>
/// <remarks>A backend never sees an unauthorized request. It plans through <see cref="ArtifactPlanner"/> so every adapter shares one set of portable semantics, executes the plan durably, and returns the result only after the plan is committed.</remarks>
internal interface IArtifactStoreBackend
{
    /// <summary>Executes a staging request.</summary>
    /// <param name="request">The exact authorized request.</param>
    /// <param name="now">The operation instant.</param>
    /// <param name="cancellationToken">Cancels before the plan commits.</param>
    /// <returns>The staging receipt or typed rejection.</returns>
    public ValueTask<ArtifactStorePrepareResult> PrepareAsync(ArtifactStorePrepareRequest request, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Executes a publication request.</summary>
    /// <param name="request">The exact authorized request.</param>
    /// <param name="now">The operation instant.</param>
    /// <param name="cancellationToken">Cancels before the plan commits.</param>
    /// <returns>The committed reference or typed rejection.</returns>
    public ValueTask<ArtifactStoreFinalizeResult> FinalizeAsync(ArtifactStoreFinalizeRequest request, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Executes an abort request.</summary>
    /// <param name="request">The exact authorized request.</param>
    /// <param name="now">The operation instant.</param>
    /// <param name="cancellationToken">Cancels before the plan commits.</param>
    /// <returns>Absence confirmation or typed rejection.</returns>
    public ValueTask<ArtifactStoreAbortResult> AbortAsync(ArtifactStoreAbortRequest request, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Executes a read request.</summary>
    /// <param name="request">The exact authorized request.</param>
    /// <param name="cancellationToken">Cancels before the stream is exposed.</param>
    /// <returns>An owned readable stream or typed rejection.</returns>
    public ValueTask<ArtifactStoreReadResult> ReadAsync(ArtifactStoreReadRequest request, CancellationToken cancellationToken);

    /// <summary>Executes a deletion request.</summary>
    /// <param name="request">The exact authorized request.</param>
    /// <param name="now">The operation instant.</param>
    /// <param name="cancellationToken">Cancels before the plan commits.</param>
    /// <returns>Absence confirmation or typed rejection.</returns>
    public ValueTask<ArtifactStoreDeleteResult> DeleteAsync(ArtifactStoreDeleteRequest request, DateTimeOffset now, CancellationToken cancellationToken);
}
