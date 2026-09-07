// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Security.Cryptography;

/// <summary>Produces exact security evidence for artifact staging and publication.</summary>
public static class ArtifactSecurityBinding
{
    /// <summary>Names one logical artifact.</summary>
    /// <param name="id">The artifact identity.</param>
    /// <returns>The protected artifact resource.</returns>
    public static ProtectedResource ArtifactResource(ArtifactId id) => new(ProtectedResourceKind.Artifact, $"artifact:{id}");

    /// <summary>Names one unpublished preparation.</summary>
    /// <param name="id">The preparation identity.</param>
    /// <returns>The protected preparation resource.</returns>
    public static ProtectedResource PreparationResource(ArtifactPreparationId id) => new(ProtectedResourceKind.Artifact, $"artifact-preparation:{id}");

    /// <summary>Fingerprints staging intent including declared integrity and lifecycle policy.</summary>
    /// <param name="artifactId">The reserved artifact identity.</param>
    /// <param name="preparationId">The staging identity.</param>
    /// <param name="directoryId">The logical directory.</param>
    /// <param name="metadata">The exact declared metadata.</param>
    /// <returns>A deterministic SHA-256 fingerprint.</returns>
    public static InputFingerprint PrepareFingerprint(ArtifactId artifactId, ArtifactPreparationId preparationId, ArtifactDirectoryId directoryId, ArtifactMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return Hash(new
        {
            artifactId = artifactId.ToString(),
            preparationId = preparationId.ToString(),
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
        });
    }

    /// <summary>Fingerprints publication of one exact preparation.</summary>
    /// <param name="preparationId">The staging identity.</param>
    /// <returns>A deterministic fingerprint.</returns>
    public static InputFingerprint FinalizeFingerprint(ArtifactPreparationId preparationId) => Hash(new { preparationId = preparationId.ToString(), action = "finalize" });

    /// <summary>Fingerprints removal of one exact preparation and reason.</summary>
    /// <param name="preparationId">The staging identity.</param>
    /// <param name="reason">The declared reason.</param>
    /// <returns>A deterministic fingerprint.</returns>
    public static InputFingerprint AbortFingerprint(ArtifactPreparationId preparationId, ArtifactAbortReason reason) => Hash(new { preparationId = preparationId.ToString(), action = "abort", reason = reason.ToString() });

    /// <summary>Fingerprints observation of one exact immutable artifact version.</summary>
    /// <param name="reference">The exact portable reference.</param>
    /// <returns>A deterministic fingerprint.</returns>
    public static InputFingerprint ReadFingerprint(ArtifactReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return ReferenceFingerprint(reference, "read");
    }

    /// <summary>Fingerprints deletion of one exact immutable artifact version.</summary>
    /// <param name="reference">The exact portable reference.</param>
    /// <returns>A deterministic fingerprint.</returns>
    public static InputFingerprint DeleteFingerprint(ArtifactReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return ReferenceFingerprint(reference, "delete");
    }

    private static InputFingerprint ReferenceFingerprint(ArtifactReference reference, string action) => Hash(new
    {
        action,
        artifactId = reference.Id.ToString(),
        version = reference.Version.Value,
        tenantId = reference.TenantId.Value,
        length = reference.Length,
        contentHash = reference.Integrity.ContentHash.Value,
        profileKey = reference.ProfileKey.Value,
        profileVersion = reference.ProfileVersion.Value,
    });

    private static InputFingerprint Hash<T>(T value) => new(Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))));
}
