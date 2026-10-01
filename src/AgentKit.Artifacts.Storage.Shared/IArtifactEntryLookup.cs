// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Storage;

/// <summary>Reads authoritative artifact entries for one planning step.</summary>
/// <remarks>Every lookup is tenant-qualified, so state in one tenant can never be observed through another tenant's identifiers. A lookup reflects one consistent view for the duration of a plan.</remarks>
internal interface IArtifactEntryLookup
{
    /// <summary>Finds the entry for a tenant preparation.</summary>
    /// <param name="tenantId">The tenant partition.</param>
    /// <param name="preparationId">The preparation identity.</param>
    /// <returns>The entry, or <see langword="null"/> when the tenant has none.</returns>
    public ArtifactEntry? ByPreparation(TenantId tenantId, ArtifactPreparationId preparationId);

    /// <summary>Finds the entry that first used a prepare replay key.</summary>
    /// <param name="tenantId">The tenant partition.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <returns>The entry, or <see langword="null"/> when the key was never used in the tenant.</returns>
    public ArtifactEntry? ByReplay(TenantId tenantId, IdempotencyKey idempotencyKey);

    /// <summary>Finds the finalized entry, live or tombstoned, that claimed an immutable version.</summary>
    /// <param name="tenantId">The tenant partition.</param>
    /// <param name="artifactId">The logical artifact.</param>
    /// <param name="version">The immutable version.</param>
    /// <returns>The finalized entry, or <see langword="null"/> when no preparation ever published the version in the tenant.</returns>
    public ArtifactEntry? ByArtifact(TenantId tenantId, ArtifactId artifactId, ArtifactVersion version);
}
