// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Storage;

/// <summary>Is the persisted form of one <see cref="ArtifactEntry"/>, carrying only plain values so a durable adapter can encode it under its own contract.</summary>
/// <remarks>A committed reference is reconstructed from the entry's own identity, metadata, and the small <see cref="StoredArtifactReference"/>, so the persisted form never stores a value twice.</remarks>
/// <param name="Tenant">The tenant partition text.</param>
/// <param name="PreparationId">The staging identity.</param>
/// <param name="ArtifactId">The reserved logical artifact.</param>
/// <param name="Version">The reserved immutable version text.</param>
/// <param name="ProfileKey">The captured profile key text.</param>
/// <param name="ProfileVersion">The captured profile revision.</param>
/// <param name="CreatedBy">The creating principal text.</param>
/// <param name="DirectoryId">The logical directory text.</param>
/// <param name="Metadata">The validated metadata.</param>
/// <param name="ContentHash">The verified complete-content hash text.</param>
/// <param name="IdempotencyKey">The prepare replay key text.</param>
/// <param name="CreatedAt">The staging instant.</param>
/// <param name="ExpiresAt">The staging expiry.</param>
/// <param name="State">The lifecycle state.</param>
/// <param name="Reference">The committed reference details; present exactly when the entry is finalized.</param>
/// <param name="DeletedAt">The tombstone instant, or <see langword="null"/> while live.</param>
internal sealed record StoredArtifactEntry(
    string Tenant,
    Guid PreparationId,
    Guid ArtifactId,
    string Version,
    string ProfileKey,
    long ProfileVersion,
    string CreatedBy,
    string DirectoryId,
    StoredArtifactMetadata Metadata,
    string ContentHash,
    string IdempotencyKey,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    ArtifactEntryState State,
    StoredArtifactReference? Reference,
    DateTimeOffset? DeletedAt)
{
    /// <summary>Converts an entry to its persisted form.</summary>
    /// <param name="value">The non-null entry.</param>
    /// <returns>The document.</returns>
    internal static StoredArtifactEntry FromDomain(ArtifactEntry value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
            value.TenantId.Value, value.PreparationId.Value, value.ArtifactId.Value, value.Version.Value, value.ProfileKey.Value,
            value.ProfileVersion.Value, value.CreatedBy.Value, value.DirectoryId.Value, StoredArtifactMetadata.FromDomain(value.Metadata),
            value.ContentHash.Value, value.IdempotencyKey.Value, value.CreatedAt, value.ExpiresAt, value.State,
            value.Reference is null ? null : StoredArtifactReference.FromDomain(value.Reference), value.DeletedAt);
    }

    /// <summary>Restores the entry, re-running every domain validation.</summary>
    /// <returns>The entry.</returns>
    /// <exception cref="InvalidDataException">The state is undefined, or a reference is present exactly when the entry is not finalized.</exception>
    internal ArtifactEntry ToDomain()
    {
        var isFinalized = State == ArtifactEntryState.Finalized;
        var hasReference = Reference is not null;
        if (!Enum.IsDefined(State) || isFinalized != hasReference)
        {
            throw new InvalidDataException("A stored artifact entry has an inconsistent state.");
        }

        var metadata = Metadata.ToDomain();
        var tenant = new TenantId(Tenant);
        var version = new ArtifactVersion(Version);
        var reference = Reference is null
            ? null
            : new ArtifactReference(
                new ArtifactId(ArtifactId), version, new ArtifactDirectoryId(DirectoryId), new ArtifactProfileKey(Reference.ProfileKey),
                new ArtifactProfileVersion(Reference.ProfileVersion), tenant, metadata.OwnerId, new PrincipalId(CreatedBy),
                metadata.MediaType, Reference.Length, new ArtifactIntegrity(new ContentHash(Reference.ContentHash), Reference.VerifiedAt),
                metadata.Classification, metadata.Ownership, metadata.Mutability, metadata.Retention, metadata.ExternalOwnership,
                Reference.CreatedAt);
        return new ArtifactEntry(
            tenant, new ArtifactPreparationId(PreparationId), new ArtifactId(ArtifactId), version, new ArtifactProfileKey(ProfileKey),
            new ArtifactProfileVersion(ProfileVersion), new PrincipalId(CreatedBy), new ArtifactDirectoryId(DirectoryId), metadata,
            new ContentHash(ContentHash), new IdempotencyKey(IdempotencyKey), CreatedAt, ExpiresAt, State, reference, DeletedAt);
    }
}
