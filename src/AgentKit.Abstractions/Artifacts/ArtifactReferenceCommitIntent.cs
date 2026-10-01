// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records a caller's durable intent to commit one artifact reference into its owning store before finalization.</summary>
/// <remarks>The caller owns the intent store and its transaction with the session, tool, or memory record. A crash leaves an explicit pending intent for reconciliation; the artifact runtime never calls back into the referencing coordinator.</remarks>
public sealed record ArtifactReferenceCommitIntent
{
    /// <summary>Initializes a reference-commit intent.</summary>
    /// <param name="id">The intent identity.</param>
    /// <param name="tenantId">The tenant partition.</param>
    /// <param name="preparationId">The preparation whose finalized reference will be committed.</param>
    /// <param name="artifactId">The reserved logical artifact.</param>
    /// <param name="version">The reserved immutable version.</param>
    /// <param name="ownerId">The retention owner that will hold the reference.</param>
    /// <param name="pin">The retention fence covering the pending commit window; it must cover this intent.</param>
    /// <param name="state">The current state.</param>
    /// <param name="recordedAt">The instant the intent was recorded.</param>
    /// <param name="updatedAt">The instant of the latest transition, not before <paramref name="recordedAt"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pin"/> is null.</exception>
    /// <exception cref="ArgumentException">A string-backed identity is blank or <paramref name="pin"/> covers another intent.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is empty, the state is undefined, or <paramref name="updatedAt"/> precedes <paramref name="recordedAt"/>.</exception>
    public ArtifactReferenceCommitIntent(
        ArtifactReferenceCommitIntentId id, TenantId tenantId, ArtifactPreparationId preparationId,
        ArtifactId artifactId, ArtifactVersion version, ArtifactOwnerId ownerId, ArtifactPin pin,
        ArtifactReferenceCommitState state, DateTimeOffset recordedAt, DateTimeOffset updatedAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentOutOfRangeException.ThrowIfEqual(preparationId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(artifactId, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(version.Value, nameof(version));
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId.Value, nameof(ownerId));
        ArgumentNullException.ThrowIfNull(pin);
        ArgumentException.ThrowIfNotEqual(pin.IntentId, id, nameof(pin));
        ArgumentOutOfRangeException.ThrowIfUndefined(state);
        ArgumentOutOfRangeException.ThrowIfLessThan(updatedAt, recordedAt);
        Id = id; TenantId = tenantId; PreparationId = preparationId; ArtifactId = artifactId; Version = version;
        OwnerId = ownerId; Pin = pin; State = state; RecordedAt = recordedAt; UpdatedAt = updatedAt;
    }

    /// <summary>Gets the intent identity.</summary>
    public ArtifactReferenceCommitIntentId Id { get; }

    /// <summary>Gets the tenant partition.</summary>
    public TenantId TenantId { get; }

    /// <summary>Gets the preparation whose finalized reference will be committed.</summary>
    public ArtifactPreparationId PreparationId { get; }

    /// <summary>Gets the reserved logical artifact.</summary>
    public ArtifactId ArtifactId { get; }

    /// <summary>Gets the reserved immutable version.</summary>
    public ArtifactVersion Version { get; }

    /// <summary>Gets the retention owner that will hold the reference.</summary>
    public ArtifactOwnerId OwnerId { get; }

    /// <summary>Gets the retention fence covering the pending commit window.</summary>
    public ArtifactPin Pin { get; }

    /// <summary>Gets the current state.</summary>
    public ArtifactReferenceCommitState State { get; }

    /// <summary>Gets the instant the intent was recorded.</summary>
    public DateTimeOffset RecordedAt { get; }

    /// <summary>Gets the instant of the latest transition.</summary>
    public DateTimeOffset UpdatedAt { get; }
}
