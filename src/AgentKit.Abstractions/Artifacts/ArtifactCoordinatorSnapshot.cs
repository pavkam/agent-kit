// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Is the immutable composition evidence of one keyed artifact coordinator: its profile and every backend it may route to.</summary>
/// <remarks>The snapshot exists so composition validation can prove that every backend a coordinator's retained profile revisions route to has an explicit store registration, without the facade referencing the artifact runtime. It carries no route from directory to backend; routes stay composition data.</remarks>
public sealed record ArtifactCoordinatorSnapshot
{
    /// <summary>Initializes a validated snapshot.</summary>
    /// <param name="key">The coordinator registration key.</param>
    /// <param name="profileKey">The logical profile the coordinator is bound to.</param>
    /// <param name="profileVersion">The current positive profile revision new writes bind to.</param>
    /// <param name="backends">Every distinct backend the coordinator's retained profile revisions route to; it must not be default or contain a blank or duplicate key.</param>
    /// <exception cref="ArgumentException">A key is blank, or <paramref name="backends"/> is default, empty, blank, or duplicated.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="profileVersion"/> is not positive.</exception>
    public ArtifactCoordinatorSnapshot(
        ComponentKey<IArtifactCoordinator> key,
        ArtifactProfileKey profileKey,
        ArtifactProfileVersion profileVersion,
        ImmutableArray<ArtifactBackendKey> backends)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey.Value, nameof(profileKey));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(profileVersion.Value, nameof(profileVersion));
        ArgumentException.ThrowIfDefaultOrEmpty(backends, nameof(backends));
        if (backends.Any(static backend => string.IsNullOrWhiteSpace(backend.Value)) || backends.Distinct().Count() != backends.Length)
        {
            throw new ArgumentException("Backend keys must be initialized and unique.", nameof(backends));
        }

        Key = key;
        ProfileKey = profileKey;
        ProfileVersion = profileVersion;
        Backends = backends;
    }

    /// <summary>Gets the coordinator registration key.</summary>
    public ComponentKey<IArtifactCoordinator> Key { get; }

    /// <summary>Gets the logical profile the coordinator is bound to.</summary>
    public ArtifactProfileKey ProfileKey { get; }

    /// <summary>Gets the current profile revision new writes bind to.</summary>
    public ArtifactProfileVersion ProfileVersion { get; }

    /// <summary>Gets every distinct backend the coordinator's retained profile revisions route to.</summary>
    public ImmutableArray<ArtifactBackendKey> Backends { get; }

    /// <summary>Determines whether another snapshot is identical, comparing backends by content.</summary>
    /// <param name="other">The snapshot to compare.</param>
    /// <returns><see langword="true"/> when every value matches.</returns>
    public bool Equals(ArtifactCoordinatorSnapshot? other) =>
        other is not null && Key == other.Key && ProfileKey == other.ProfileKey && ProfileVersion == other.ProfileVersion
        && Backends.SequenceEqual(other.Backends);

    /// <summary>Returns a hash code consistent with <see cref="Equals(ArtifactCoordinatorSnapshot?)"/>.</summary>
    /// <returns>A hash over every value.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Key);
        hash.Add(ProfileKey);
        hash.Add(ProfileVersion);
        foreach (var backend in Backends)
        {
            hash.Add(backend);
        }

        return hash.ToHashCode();
    }
}
