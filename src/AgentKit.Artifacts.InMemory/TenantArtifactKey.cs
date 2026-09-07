// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory;

/// <summary>Keys one validated portable artifact version within an authenticated tenant partition.</summary>
internal readonly record struct TenantArtifactKey
{
    /// <summary>Initializes a tenant-qualified immutable artifact key.</summary>
    /// <param name="tenantId">The non-empty authenticated tenant that owns the artifact.</param>
    /// <param name="artifactId">The non-empty logical artifact identity within the tenant.</param>
    /// <param name="version">The non-empty immutable artifact version within the tenant.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tenantId"/> or <paramref name="version"/> has a null value.</exception>
    /// <exception cref="ArgumentException"><paramref name="tenantId"/> or <paramref name="version"/> has an empty or whitespace value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="artifactId"/> is empty.</exception>
    internal TenantArtifactKey(TenantId tenantId, ArtifactId artifactId, ArtifactVersion version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentOutOfRangeException.ThrowIfEqual(artifactId, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(version.Value, nameof(version));
        TenantId = tenantId;
        ArtifactId = artifactId;
        Version = version;
    }

    /// <summary>Gets the authenticated tenant that owns the artifact.</summary>
    /// <value>The validated tenant, or the default tenant only on a zero-initialized, invalid default key.</value>
    internal TenantId TenantId { get; }

    /// <summary>Gets the logical artifact identity within the tenant.</summary>
    /// <value>The validated artifact identity, or the empty identity only on a zero-initialized, invalid default key.</value>
    internal ArtifactId ArtifactId { get; }

    /// <summary>Gets the immutable artifact version within the tenant.</summary>
    /// <value>The validated version, or the default version only on a zero-initialized, invalid default key.</value>
    internal ArtifactVersion Version { get; }
}
