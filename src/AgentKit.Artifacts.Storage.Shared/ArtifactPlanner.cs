// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Storage;

/// <summary>Plans every artifact lifecycle transition as a pure function of one consistent entry view, shared by every adapter.</summary>
/// <remarks>
/// The planner owns the portable semantics the conformance suite verifies: tenant partitioning, replay identity, single-winner
/// finalization, tombstones, legal hold, and external ownership. It performs no I/O. An adapter supplies a lookup over its
/// authoritative state, executes the returned plan durably, and only then returns the plan's result.
/// </remarks>
internal static class ArtifactPlanner
{
    private const string _unavailablePreparation = "The preparation is unavailable or belongs to another tenant.";
    private const string _unavailableArtifact = "The exact committed artifact version was not found.";

    /// <summary>Plans staging of validated bytes.</summary>
    /// <param name="lookup">The consistent entry view.</param>
    /// <param name="request">The exact authorized staging request.</param>
    /// <param name="now">The operation instant.</param>
    /// <returns>A plan whose result is the staging receipt, the original receipt for an equivalent replay, or a typed rejection.</returns>
    internal static ArtifactPlan<ArtifactStorePrepareResult> PlanPrepare(IArtifactEntryLookup lookup, ArtifactStorePrepareRequest request, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        ArgumentNullException.ThrowIfNull(request);
        _ = now;
        if (request.TenantId != request.Identity.TenantId)
        {
            return Reject(ArtifactFailureKind.Denied, "The declared tenant does not match the authenticated identity.");
        }

        if (request.CreatedBy != request.Identity.PrincipalId)
        {
            return Reject(ArtifactFailureKind.Denied, "The declared creator does not match the authenticated identity.");
        }

        var observed = FileSecurityBinding.ContentFingerprint(request.Content.AsSpan());
        if (request.Content.Length != request.Metadata.DeclaredLength
            || observed != request.ContentHash
            || (request.Metadata.DeclaredContentHash is { } declared && declared != observed))
        {
            return Reject(ArtifactFailureKind.IntegrityMismatch, "Backend integrity validation did not match the declaration.");
        }

        if (lookup.ByReplay(request.TenantId, request.IdempotencyKey) is { } prior)
        {
            return Equivalent(prior, request)
                ? new ArtifactPlan<ArtifactStorePrepareResult>(prior.Receipt)
                : Reject(ArtifactFailureKind.Conflict, "The prepare idempotency key was reused with different content or policy.");
        }

        if (lookup.ByPreparation(request.TenantId, request.PreparationId) is not null)
        {
            return Reject(ArtifactFailureKind.Conflict, "The preparation identity is already in use.");
        }

        var entry = new ArtifactEntry(
            request.TenantId, request.PreparationId, request.ArtifactId, request.Version, request.ProfileKey,
            request.ProfileVersion, request.CreatedBy, request.DirectoryId, request.Metadata, request.ContentHash,
            request.IdempotencyKey, request.CreatedAt, request.ExpiresAt, ArtifactEntryState.Prepared, null, null);
        return new ArtifactPlan<ArtifactStorePrepareResult>(entry.Receipt)
        {
            Upserts = [entry],
            Stage = new ArtifactPayloadStage(entry, request.Content),
        };
    }

    /// <summary>Plans atomic publication of one staged preparation.</summary>
    /// <param name="lookup">The consistent entry view.</param>
    /// <param name="request">The exact authorized publication request.</param>
    /// <param name="now">The operation instant, which stamps the reference and integrity evidence.</param>
    /// <returns>A plan whose result is the reference, the original reference for an equivalent retry, or a typed rejection.</returns>
    internal static ArtifactPlan<ArtifactStoreFinalizeResult> PlanFinalize(IArtifactEntryLookup lookup, ArtifactStoreFinalizeRequest request, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        ArgumentNullException.ThrowIfNull(request);
        var tenant = request.Identity.TenantId;
        var entry = lookup.ByPreparation(tenant, request.PreparationId);
        if (entry is null || entry.State == ArtifactEntryState.Aborted)
        {
            return RejectFinalize(ArtifactFailureKind.NotFound, _unavailablePreparation);
        }

        if (entry.State == ArtifactEntryState.Finalized)
        {
            return entry.IsDeleted
                ? RejectFinalize(ArtifactFailureKind.NotFound, _unavailablePreparation)
                : new ArtifactPlan<ArtifactStoreFinalizeResult>(new ArtifactStoreFinalized(entry.Reference!));
        }

        if (entry.ExpiresAt <= now)
        {
            var expired = entry with { State = ArtifactEntryState.Aborted };
            return new ArtifactPlan<ArtifactStoreFinalizeResult>(
                new ArtifactStoreFinalizeRejected(new ArtifactFailure(ArtifactFailureKind.NotFound, "The preparation expired before publication.")))
            {
                Upserts = [expired],
                Releases = [entry],
            };
        }

        if (lookup.ByArtifact(tenant, entry.ArtifactId, entry.Version) is { } claimed)
        {
            return RejectFinalize(
                ArtifactFailureKind.Conflict,
                claimed.IsDeleted
                    ? "A deleted immutable artifact version cannot be published again."
                    : "The immutable artifact version is already committed.");
        }

        var metadata = entry.Metadata;
        var reference = new ArtifactReference(
            entry.ArtifactId, entry.Version, entry.DirectoryId, entry.ProfileKey, entry.ProfileVersion, entry.TenantId,
            metadata.OwnerId, entry.CreatedBy, metadata.MediaType, metadata.DeclaredLength,
            new ArtifactIntegrity(entry.ContentHash, now), metadata.Classification, metadata.Ownership,
            metadata.Mutability, metadata.Retention, metadata.ExternalOwnership, now);
        return new ArtifactPlan<ArtifactStoreFinalizeResult>(new ArtifactStoreFinalized(reference))
        {
            Upserts = [entry with { State = ArtifactEntryState.Finalized, Reference = reference }],
        };
    }

    /// <summary>Plans idempotent removal of unpublished staging.</summary>
    /// <param name="lookup">The consistent entry view.</param>
    /// <param name="request">The exact authorized abort request.</param>
    /// <returns>A plan whose result confirms absence, or rejects an unknown or already committed preparation.</returns>
    internal static ArtifactPlan<ArtifactStoreAbortResult> PlanAbort(IArtifactEntryLookup lookup, ArtifactStoreAbortRequest request)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        ArgumentNullException.ThrowIfNull(request);
        var entry = lookup.ByPreparation(request.Identity.TenantId, request.PreparationId);
        return entry?.State switch
        {
            null => new ArtifactPlan<ArtifactStoreAbortResult>(
                new ArtifactStoreAbortRejected(new ArtifactFailure(ArtifactFailureKind.NotFound, _unavailablePreparation))),
            ArtifactEntryState.Finalized => new ArtifactPlan<ArtifactStoreAbortResult>(
                entry.IsDeleted
                    ? new ArtifactStoreAborted(true)
                    : new ArtifactStoreAbortRejected(new ArtifactFailure(ArtifactFailureKind.Conflict, "Committed artifact content cannot be aborted."))),
            ArtifactEntryState.Aborted => new ArtifactPlan<ArtifactStoreAbortResult>(new ArtifactStoreAborted(true)),
            ArtifactEntryState.Prepared => new ArtifactPlan<ArtifactStoreAbortResult>(new ArtifactStoreAborted(false))
            {
                Upserts = [entry with { State = ArtifactEntryState.Aborted }],
                Releases = [entry],
            },
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>Plans a committed read against the authoritative stored reference.</summary>
    /// <param name="lookup">The consistent entry view.</param>
    /// <param name="request">The exact authorized read request.</param>
    /// <returns>The live entry to open, or a rejection indistinguishable across unavailable, foreign, tombstoned, and mismatched content.</returns>
    internal static ArtifactReadDecision PlanRead(IArtifactEntryLookup lookup, ArtifactStoreReadRequest request)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        ArgumentNullException.ThrowIfNull(request);
        if (request.Reference.TenantId != request.Identity.TenantId)
        {
            return ArtifactReadDecision.Reject(ArtifactFailureKind.NotFound, "The committed artifact is unavailable or belongs to another tenant.");
        }

        var entry = lookup.ByArtifact(request.Identity.TenantId, request.Reference.Id, request.Reference.Version);
        return entry is null || entry.IsDeleted || entry.Reference != request.Reference
            ? ArtifactReadDecision.Reject(ArtifactFailureKind.NotFound, _unavailableArtifact)
            : ArtifactReadDecision.Open(entry);
    }

    /// <summary>Plans deletion of one exact committed version by committing a tombstone.</summary>
    /// <param name="lookup">The consistent entry view.</param>
    /// <param name="request">The exact authorized deletion request.</param>
    /// <param name="now">The tombstone instant.</param>
    /// <returns>A plan whose result confirms absence, or rejects a foreign, mismatched, held, or externally owned version.</returns>
    internal static ArtifactPlan<ArtifactStoreDeleteResult> PlanDelete(IArtifactEntryLookup lookup, ArtifactStoreDeleteRequest request, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        ArgumentNullException.ThrowIfNull(request);
        if (request.Reference.TenantId != request.Identity.TenantId)
        {
            return RejectDelete(ArtifactFailureKind.NotFound, "The committed artifact is unavailable or belongs to another tenant.");
        }

        var entry = lookup.ByArtifact(request.Identity.TenantId, request.Reference.Id, request.Reference.Version);
        if (entry is null || entry.Reference != request.Reference)
        {
            return RejectDelete(ArtifactFailureKind.NotFound, _unavailableArtifact);
        }

        if (entry.IsDeleted)
        {
            return new ArtifactPlan<ArtifactStoreDeleteResult>(new ArtifactStoreDeleted(true));
        }

        var stored = entry.Reference;
        return stored.Retention.LegalHold
            ? RejectDelete(ArtifactFailureKind.RetentionConflict, "Artifact retention prohibits deletion.")
            : stored.ExternalOwnership is { AgentKitMayDelete: false }
                ? RejectDelete(ArtifactFailureKind.RetentionConflict, "The external owner retains deletion authority.")
                : new ArtifactPlan<ArtifactStoreDeleteResult>(new ArtifactStoreDeleted(false))
                {
                    Upserts = [entry with { DeletedAt = now }],
                    Releases = [entry],
                };
    }

    private static bool Equivalent(ArtifactEntry prior, ArtifactStorePrepareRequest request) =>
        prior.DirectoryId == request.DirectoryId
        && prior.Metadata == request.Metadata
        && prior.ContentHash == request.ContentHash
        && prior.Version == request.Version
        && prior.ProfileKey == request.ProfileKey
        && prior.ProfileVersion == request.ProfileVersion
        && prior.TenantId == request.Identity.TenantId
        && prior.CreatedBy == request.Identity.PrincipalId
        && prior.ExpiresAt - prior.CreatedAt == request.ExpiresAt - request.CreatedAt;

    private static ArtifactPlan<ArtifactStorePrepareResult> Reject(ArtifactFailureKind kind, string message) =>
        new(new ArtifactStorePrepareRejected(new ArtifactFailure(kind, message)));

    private static ArtifactPlan<ArtifactStoreFinalizeResult> RejectFinalize(ArtifactFailureKind kind, string message) =>
        new(new ArtifactStoreFinalizeRejected(new ArtifactFailure(kind, message)));

    private static ArtifactPlan<ArtifactStoreDeleteResult> RejectDelete(ArtifactFailureKind kind, string message) =>
        new(new ArtifactStoreDeleteRejected(new ArtifactFailure(kind, message)));
}
