// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Creates isolated artifact-store composition and deterministic lifecycle requests for one conformance case.</summary>
/// <remarks>The fixture owns every store and authority dependency it creates. Disposing it releases those resources.</remarks>
public interface IArtifactStoreConformanceFixture: IAsyncDisposable
{
    /// <summary>Gets the primary authenticated identity used by lifecycle requests.</summary>
    public ExecutionIdentity PrimaryIdentity { get; }

    /// <summary>Gets an authenticated identity in a distinct tenant partition.</summary>
    public ExecutionIdentity SecondaryIdentity { get; }

    /// <summary>Creates the public store contract through the implementation's normal composition path.</summary>
    /// <param name="cancellationToken">Cancels fixture composition before it completes.</param>
    /// <returns>The fixture-owned store under test.</returns>
    public ValueTask<IArtifactStore> CreateAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a valid deterministic preparation request, with optional values used by replay and partition cases.</summary>
    /// <param name="content">The complete content staged by the request.</param>
    /// <param name="identity">The authenticated identity, or the primary identity when omitted.</param>
    /// <param name="idempotencyKey">The stable replay key.</param>
    /// <param name="artifactId">A reserved artifact identity, or a fresh deterministic identity when omitted.</param>
    /// <param name="preparationId">A preparation identity, or a fresh deterministic identity when omitted.</param>
    /// <param name="version">The immutable version, or version 1 when omitted.</param>
    /// <param name="createdAt">The staging instant, or the fixture's deterministic current instant when omitted.</param>
    /// <param name="lifetime">The positive staging lifetime, or five minutes when omitted.</param>
    /// <param name="grantFingerprint">The grant fingerprint, or the exact request fingerprint when omitted.</param>
    /// <returns>A valid request whose grant can be registered through <see cref="RegisterGrantAsync"/>.</returns>
    public ArtifactStorePrepareRequest CreatePrepare(
        byte[] content,
        ExecutionIdentity? identity = null,
        string idempotencyKey = "prepare",
        ArtifactId? artifactId = null,
        ArtifactPreparationId? preparationId = null,
        ArtifactVersion? version = null,
        DateTimeOffset? createdAt = null,
        TimeSpan? lifetime = null,
        InputFingerprint? grantFingerprint = null);

    /// <summary>Creates a publication request for one preparation and authenticated identity.</summary>
    /// <param name="preparationId">The preparation to publish.</param>
    /// <param name="identity">The authenticated caller.</param>
    /// <returns>A valid publication request.</returns>
    public ArtifactStoreFinalizeRequest CreateFinalize(ArtifactPreparationId preparationId, ExecutionIdentity identity);

    /// <summary>Creates an abort request for one preparation and authenticated identity.</summary>
    /// <param name="preparationId">The preparation to remove.</param>
    /// <param name="identity">The authenticated caller.</param>
    /// <returns>A valid abort request.</returns>
    public ArtifactStoreAbortRequest CreateAbort(ArtifactPreparationId preparationId, ExecutionIdentity identity);

    /// <summary>Creates a read request bound to an exact reference and authenticated identity.</summary>
    /// <param name="reference">The committed reference to read.</param>
    /// <param name="identity">The authenticated caller.</param>
    /// <returns>A valid read request.</returns>
    public ArtifactStoreReadRequest CreateRead(ArtifactReference reference, ExecutionIdentity identity);

    /// <summary>Creates a deletion request bound to an exact reference and authenticated identity.</summary>
    /// <param name="reference">The committed reference to delete.</param>
    /// <param name="identity">The authenticated caller.</param>
    /// <returns>A valid deletion request.</returns>
    public ArtifactStoreDeleteRequest CreateDelete(ArtifactReference reference, ExecutionIdentity identity);

    /// <summary>Registers a request's grant with the fixture-owned authority before the effect is attempted.</summary>
    /// <param name="grant">The exact grant presented by the request.</param>
    /// <param name="cancellationToken">Cancels registration before it completes.</param>
    /// <returns>A task representing registration.</returns>
    public ValueTask RegisterGrantAsync(SecurityGrant grant, CancellationToken cancellationToken = default);
}
