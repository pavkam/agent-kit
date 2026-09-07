// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries coordinator-validated bytes and exact authority to the staging backend.</summary>
public sealed record ArtifactStorePrepareRequest
{
    /// <summary>Initializes an exact backend staging request.</summary>
    /// <param name="artifactId">The reserved artifact.</param>
    /// <param name="preparationId">The staging identity.</param>
    /// <param name="version">The reserved immutable version.</param>
    /// <param name="profileKey">The captured logical profile.</param>
    /// <param name="profileVersion">The captured profile revision.</param>
    /// <param name="tenantId">The tenant partition.</param>
    /// <param name="createdBy">The creating principal.</param>
    /// <param name="directoryId">The logical directory.</param>
    /// <param name="metadata">The validated metadata.</param>
    /// <param name="content">The exact complete bytes.</param>
    /// <param name="createdAt">The staging time.</param>
    /// <param name="expiresAt">The staging expiry.</param>
    /// <param name="scope">The exact security scope.</param>
    /// <param name="identity">The authenticated identity.</param>
    /// <param name="grant">The single-use backend grant.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <exception cref="ArgumentNullException">A reference value is null.</exception>
    /// <exception cref="ArgumentException">Bytes are default or a value is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is empty.</exception>
    public ArtifactStorePrepareRequest(
        ArtifactId artifactId, ArtifactPreparationId preparationId, ArtifactVersion version,
        ArtifactProfileKey profileKey, ArtifactProfileVersion profileVersion,
        TenantId tenantId, PrincipalId createdBy, ArtifactDirectoryId directoryId,
        ArtifactMetadata metadata, ImmutableArray<byte> content,
        DateTimeOffset createdAt, DateTimeOffset expiresAt,
        SecurityAuthorizationScope scope, ExecutionIdentity identity,
        SecurityGrant grant, IdempotencyKey idempotencyKey)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(artifactId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(preparationId, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(version.Value, nameof(version));
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey.Value, nameof(profileKey));
        ArgumentOutOfRangeException.ThrowIfEqual(profileVersion, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy.Value, nameof(createdBy));
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryId.Value, nameof(directoryId));
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfDefault(content);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        ArtifactId = artifactId; PreparationId = preparationId; Version = version;
        ProfileKey = profileKey; ProfileVersion = profileVersion; TenantId = tenantId;
        CreatedBy = createdBy; DirectoryId = directoryId; Metadata = metadata; Content = content;
        CreatedAt = createdAt; ExpiresAt = expiresAt; Scope = scope; Identity = identity;
        Grant = grant; IdempotencyKey = idempotencyKey;
    }
    /// <summary>Gets the reserved artifact.</summary>
    public ArtifactId ArtifactId { get; }
    /// <summary>Gets the staging identity.</summary>
    public ArtifactPreparationId PreparationId { get; }
    /// <summary>Gets the reserved immutable version.</summary>
    public ArtifactVersion Version { get; }
    /// <summary>Gets the captured profile.</summary>
    public ArtifactProfileKey ProfileKey { get; }
    /// <summary>Gets the captured profile revision.</summary>
    public ArtifactProfileVersion ProfileVersion { get; }
    /// <summary>Gets the tenant partition.</summary>
    public TenantId TenantId { get; }
    /// <summary>Gets the creating principal.</summary>
    public PrincipalId CreatedBy { get; }
    /// <summary>Gets the logical directory.</summary>
    public ArtifactDirectoryId DirectoryId { get; }
    /// <summary>Gets the validated metadata.</summary>
    public ArtifactMetadata Metadata { get; }
    /// <summary>Gets the exact complete bytes.</summary>
    public ImmutableArray<byte> Content { get; }
    /// <summary>Gets the staging time.</summary>
    public DateTimeOffset CreatedAt { get; }
    /// <summary>Gets the staging expiry.</summary>
    public DateTimeOffset ExpiresAt { get; }
    /// <summary>Gets the exact security scope.</summary>
    public SecurityAuthorizationScope Scope { get; }
    /// <summary>Gets the authenticated identity.</summary>
    public ExecutionIdentity Identity { get; }
    /// <summary>Gets the single-use backend grant.</summary>
    public SecurityGrant Grant { get; }
    /// <summary>Gets the replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }
}
