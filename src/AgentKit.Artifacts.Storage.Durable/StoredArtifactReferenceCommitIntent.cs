// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Storage;

/// <summary>Is the persisted form of one <see cref="ArtifactReferenceCommitIntent"/>, carrying only plain values so a durable adapter can encode it under its own contract.</summary>
/// <param name="Id">The intent identity.</param>
/// <param name="Tenant">The tenant partition text.</param>
/// <param name="PreparationId">The preparation identity.</param>
/// <param name="ArtifactId">The reserved logical artifact.</param>
/// <param name="Version">The reserved immutable version text.</param>
/// <param name="OwnerId">The retention owner text.</param>
/// <param name="PinnedAt">The instant the retention fence was established.</param>
/// <param name="HeldUntil">The instant before which collection is refused.</param>
/// <param name="State">The current state.</param>
/// <param name="RecordedAt">The recording instant.</param>
/// <param name="UpdatedAt">The latest transition instant.</param>
internal sealed record StoredArtifactReferenceCommitIntent(
    Guid Id,
    string Tenant,
    Guid PreparationId,
    Guid ArtifactId,
    string Version,
    string OwnerId,
    DateTimeOffset PinnedAt,
    DateTimeOffset HeldUntil,
    ArtifactReferenceCommitState State,
    DateTimeOffset RecordedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>Converts an intent to its persisted form.</summary>
    /// <param name="value">The non-null intent.</param>
    /// <returns>The document.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    internal static StoredArtifactReferenceCommitIntent FromDomain(ArtifactReferenceCommitIntent value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
            value.Id.Value, value.TenantId.Value, value.PreparationId.Value, value.ArtifactId.Value, value.Version.Value,
            value.OwnerId.Value, value.Pin.PinnedAt, value.Pin.HeldUntil, value.State, value.RecordedAt, value.UpdatedAt);
    }

    /// <summary>Restores the intent, re-running every domain validation.</summary>
    /// <returns>The intent.</returns>
    /// <exception cref="InvalidDataException">The persisted values violate an intent invariant.</exception>
    internal ArtifactReferenceCommitIntent ToDomain()
    {
        try
        {
            var id = new ArtifactReferenceCommitIntentId(Id);
            return new ArtifactReferenceCommitIntent(
                id, new TenantId(Tenant), new ArtifactPreparationId(PreparationId), new ArtifactId(ArtifactId), new ArtifactVersion(Version),
                new ArtifactOwnerId(OwnerId), new ArtifactPin(id, PinnedAt, HeldUntil), State, RecordedAt, UpdatedAt);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("A stored artifact reference-commit intent is invalid.", exception);
        }
    }
}
