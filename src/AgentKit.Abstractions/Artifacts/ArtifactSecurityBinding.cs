// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics;
using System.Security.Cryptography;

/// <summary>Produces exact, versioned security evidence for artifact lifecycle operations.</summary>
/// <remarks>
/// Fingerprint schema version 2 binds every portable artifact-reference field. Version 2 intentionally
/// invalidates grants produced by the earlier incomplete format so an upgraded effecting boundary fails
/// closed instead of accepting authority that was bound to fewer inputs.
/// </remarks>
public static class ArtifactSecurityBinding
{
    private const string _fingerprintSchema = "agentkit.artifact-security-binding/v2";
    private const string _prepareFingerprintSchema = "agentkit.artifact-prepare-security-binding/v1";

    /// <summary>Names one logical artifact.</summary>
    /// <param name="id">The artifact identity.</param>
    /// <returns>The protected artifact resource.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is empty.</exception>
    public static ProtectedResource ArtifactResource(ArtifactId id)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        return new(ProtectedResourceKind.Artifact, $"artifact:{id}");
    }

    /// <summary>Names one unpublished preparation.</summary>
    /// <param name="id">The preparation identity.</param>
    /// <returns>The protected preparation resource.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is empty.</exception>
    public static ProtectedResource PreparationResource(ArtifactPreparationId id)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        return new(ProtectedResourceKind.Artifact, $"artifact-preparation:{id}");
    }

    /// <summary>Fingerprints staging intent including declared integrity and lifecycle policy.</summary>
    /// <param name="artifactId">The reserved artifact identity.</param>
    /// <param name="preparationId">The staging identity.</param>
    /// <param name="version">The reserved immutable artifact version.</param>
    /// <param name="profileKey">The selected logical profile.</param>
    /// <param name="profileVersion">The selected positive profile revision.</param>
    /// <param name="tenantId">The authenticated tenant partition.</param>
    /// <param name="createdBy">The authenticated creating principal.</param>
    /// <param name="directoryId">The logical directory.</param>
    /// <param name="metadata">The exact declared metadata.</param>
    /// <param name="createdAt">The concrete staging creation instant.</param>
    /// <param name="expiresAt">The concrete staging expiry after <paramref name="createdAt"/>.</param>
    /// <returns>A deterministic SHA-256 fingerprint.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An identity is empty, the profile version is not positive, or the expiry is not after creation.</exception>
    /// <exception cref="ArgumentException">A string-backed identity, version, profile, or directory is blank.</exception>
    /// <exception cref="ArgumentNullException">
    /// A string-backed identity or version value is default, or <paramref name="metadata"/> is null.
    /// </exception>
    /// <remarks>This prepare-specific schema has no fallback to the earlier incomplete prepare fingerprint.</remarks>
    public static InputFingerprint PrepareFingerprint(
        ArtifactId artifactId,
        ArtifactPreparationId preparationId,
        ArtifactVersion version,
        ArtifactProfileKey profileKey,
        ArtifactProfileVersion profileVersion,
        TenantId tenantId,
        PrincipalId createdBy,
        ArtifactDirectoryId directoryId,
        ArtifactMetadata metadata,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(artifactId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(preparationId, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(version.Value, nameof(version));
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey.Value, nameof(profileKey));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(profileVersion.Value, nameof(profileVersion));
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy.Value, nameof(createdBy));
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryId.Value, nameof(directoryId));
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiresAt, createdAt);
        return Hash(new
        {
            schema = _prepareFingerprintSchema,
            action = "prepare",
            artifactId = artifactId.ToString(),
            preparationId = preparationId.ToString(),
            version = version.Value,
            profileKey = profileKey.Value,
            profileVersion = profileVersion.Value,
            tenantId = tenantId.Value,
            createdBy = createdBy.Value,
            directoryId = directoryId.Value,
            ownerId = metadata.OwnerId.Value,
            metadata.MediaType,
            metadata.DeclaredLength,
            contentHash = metadata.DeclaredContentHash.Value,
            classification = metadata.Classification.ToString(),
            ownership = metadata.Ownership.ToString(),
            mutability = metadata.Mutability.ToString(),
            retentionPolicy = metadata.Retention.Policy.Value,
            expiresAt = metadata.Retention.ExpiresAt?.ToUniversalTime().ToString("O"),
            metadata.Retention.LegalHold,
            createdAt = createdAt.ToUniversalTime().ToString("O"),
            stagingExpiresAt = expiresAt.ToUniversalTime().ToString("O"),
        });
    }

    /// <summary>Fingerprints publication of one exact preparation.</summary>
    /// <param name="preparationId">The staging identity.</param>
    /// <returns>A deterministic fingerprint.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="preparationId"/> is empty.</exception>
    public static InputFingerprint FinalizeFingerprint(ArtifactPreparationId preparationId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(preparationId, default);
        return Hash(new { schema = _fingerprintSchema, action = "finalize", preparationId = preparationId.ToString() });
    }

    /// <summary>Fingerprints removal of one exact preparation and reason.</summary>
    /// <param name="preparationId">The staging identity.</param>
    /// <param name="reason">The declared reason.</param>
    /// <returns>A deterministic fingerprint.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="preparationId"/> is empty or <paramref name="reason"/> is undefined.</exception>
    public static InputFingerprint AbortFingerprint(ArtifactPreparationId preparationId, ArtifactAbortReason reason)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(preparationId, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(reason);
        return Hash(new { schema = _fingerprintSchema, action = "abort", preparationId = preparationId.ToString(), reason = reason.ToString() });
    }

    /// <summary>Fingerprints observation of one exact immutable artifact version.</summary>
    /// <param name="reference">The exact portable reference.</param>
    /// <returns>A deterministic fingerprint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is null.</exception>
    public static InputFingerprint ReadFingerprint(ArtifactReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return ReferenceFingerprint(reference, "read");
    }

    /// <summary>Fingerprints deletion of one exact immutable artifact version.</summary>
    /// <param name="reference">The exact portable reference.</param>
    /// <returns>A deterministic fingerprint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is null.</exception>
    public static InputFingerprint DeleteFingerprint(ArtifactReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return ReferenceFingerprint(reference, "delete");
    }

    private static InputFingerprint ReferenceFingerprint(ArtifactReference reference, string action)
    {
        Debug.Assert(reference is not null, "The public caller validates the artifact reference.");
        Debug.Assert(action is "read" or "delete", "Only supported reference operations reach fingerprint generation.");
        return Hash(new
        {
            schema = _fingerprintSchema,
            action,
            artifactId = reference.Id.ToString(),
            version = reference.Version.Value,
            directoryId = reference.DirectoryId.Value,
            profileKey = reference.ProfileKey.Value,
            profileVersion = reference.ProfileVersion.Value,
            tenantId = reference.TenantId.Value,
            ownerId = reference.OwnerId.Value,
            createdBy = reference.CreatedBy.Value,
            reference.MediaType,
            length = reference.Length,
            contentHash = reference.Integrity.ContentHash.Value,
            integrityVerifiedAt = reference.Integrity.VerifiedAt.ToUniversalTime().ToString("O"),
            classification = reference.Classification.ToString(),
            ownership = reference.Ownership.ToString(),
            mutability = reference.Mutability.ToString(),
            retentionPolicy = reference.Retention.Policy.Value,
            retentionExpiresAt = reference.Retention.ExpiresAt?.ToUniversalTime().ToString("O"),
            reference.Retention.LegalHold,
            createdAt = reference.CreatedAt.ToUniversalTime().ToString("O"),
        });
    }

    private static InputFingerprint Hash<T>(T value)
    {
        Debug.Assert(value is not null, "Fingerprint payloads are always constructed values.");
        return new(Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))));
    }
}
