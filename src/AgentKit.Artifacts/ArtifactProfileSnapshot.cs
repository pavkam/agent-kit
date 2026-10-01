// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Is the immutable, validated snapshot of one profile revision captured at registration.</summary>
/// <remarks>Equality compares routes and allowed mutability by content, so an identical repeated registration is recognized as idempotent and a conflicting one is rejected.</remarks>
internal sealed record ArtifactProfileSnapshot
{
    private ArtifactProfileSnapshot(
        ArtifactProfileKey key,
        ArtifactProfileVersion version,
        ArtifactDirectoryId defaultDirectory,
        ImmutableDictionary<ArtifactDirectoryId, ArtifactBackendKey> routes,
        ArtifactRetention defaultRetention,
        ImmutableHashSet<ArtifactMutability> allowedMutability,
        bool allowExternalOwnership)
    {
        Key = key;
        Version = version;
        DefaultDirectory = defaultDirectory;
        Routes = routes;
        DefaultRetention = defaultRetention;
        AllowedMutability = allowedMutability;
        AllowExternalOwnership = allowExternalOwnership;
    }

    /// <summary>Gets the profile key.</summary>
    internal ArtifactProfileKey Key { get; }

    /// <summary>Gets the positive profile revision.</summary>
    internal ArtifactProfileVersion Version { get; }

    /// <summary>Gets the directory used when a caller names none.</summary>
    internal ArtifactDirectoryId DefaultDirectory { get; }

    /// <summary>Gets the directory-to-backend routes.</summary>
    internal ImmutableDictionary<ArtifactDirectoryId, ArtifactBackendKey> Routes { get; }

    /// <summary>Gets the default retention.</summary>
    internal ArtifactRetention DefaultRetention { get; }

    /// <summary>Gets the admitted mutability modes.</summary>
    internal ImmutableHashSet<ArtifactMutability> AllowedMutability { get; }

    /// <summary>Gets whether externally owned content is admitted.</summary>
    internal bool AllowExternalOwnership { get; }

    /// <summary>Validates mutable options and captures them.</summary>
    /// <param name="key">The profile key.</param>
    /// <param name="options">The configured options.</param>
    /// <returns>An immutable snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
    /// <exception cref="InvalidOperationException">The version is not positive, no directory is routed, a directory or backend key is blank, the default directory is missing or unrouted, or no mutability mode is allowed.</exception>
    internal static ArtifactProfileSnapshot Create(ArtifactProfileKey key, ArtifactProfileOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(options);
        Require(options.Version.Value > 0, $"Artifact profile '{key}' requires a positive version.");
        Require(options.Routes.Count > 0, $"Artifact profile '{key}' must route at least one directory to a backend.");
        Require(
            options.Routes.All(static route => !string.IsNullOrWhiteSpace(route.Key.Value) && !string.IsNullOrWhiteSpace(route.Value.Value)),
            $"Artifact profile '{key}' has a blank directory or backend key.");
        var defaultDirectory = options.DefaultDirectory.GetValueOrDefault();
        Require(
            !string.IsNullOrWhiteSpace(defaultDirectory.Value) && options.Routes.ContainsKey(defaultDirectory),
            $"Artifact profile '{key}' must name a default directory that is routed.");
        Require(
            options.AllowedMutability.Count > 0 && options.AllowedMutability.All(static mode => Enum.IsDefined(mode)),
            $"Artifact profile '{key}' must allow at least one defined mutability mode.");

        return new ArtifactProfileSnapshot(
            key,
            options.Version,
            defaultDirectory,
            options.Routes.ToImmutableDictionary(),
            options.DefaultRetention ?? new ArtifactRetention(new ArtifactRetentionPolicyKey("default"), null, false),
            [.. options.AllowedMutability],
            options.AllowExternalOwnership);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>Compares two snapshots by content.</summary>
    /// <param name="other">The snapshot to compare.</param>
    /// <returns><see langword="true"/> when every value matches, comparing routes and mutability by content.</returns>
    public bool Equals(ArtifactProfileSnapshot? other) =>
        other is not null
        && Key == other.Key
        && Version == other.Version
        && DefaultDirectory == other.DefaultDirectory
        && DefaultRetention == other.DefaultRetention
        && AllowExternalOwnership == other.AllowExternalOwnership
        && AllowedMutability.SetEquals(other.AllowedMutability)
        && Routes.Count == other.Routes.Count
        && Routes.All(pair => other.Routes.TryGetValue(pair.Key, out var backend) && backend == pair.Value);

    /// <summary>Returns a hash code consistent with <see cref="Equals(ArtifactProfileSnapshot?)"/>.</summary>
    /// <returns>A hash over the identifying values and route and mutability content.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Key);
        hash.Add(Version);
        hash.Add(DefaultDirectory);
        hash.Add(AllowExternalOwnership);
        hash.Add(Routes.Count);
        hash.Add(AllowedMutability.Count);
        return hash.ToHashCode();
    }

    /// <summary>Gets the distinct backends this snapshot routes to, in deterministic order.</summary>
    /// <returns>The backend keys ordered ordinally.</returns>
    internal ImmutableArray<ArtifactBackendKey> Backends() =>
        [.. Routes.Values.Distinct().OrderBy(static backend => backend.Value, StringComparer.Ordinal)];
}
