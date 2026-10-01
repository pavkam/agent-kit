// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Publishes immutable composition evidence for every coordinator registered through <c>AddAgentArtifacts</c>.</summary>
/// <remarks>
/// Snapshots are built once from the registered coordinators and the retained profile revisions each names. A coordinator whose
/// profile is not registered fails construction with <see cref="InvalidOperationException"/> so composition validation reports it
/// rather than a run discovering it. The catalog resolves no store and performs no I/O.
/// </remarks>
internal sealed class DefaultArtifactCoordinatorCatalog: IArtifactCoordinatorCatalog
{
    private readonly ImmutableDictionary<ComponentKey<IArtifactCoordinator>, ArtifactCoordinatorSnapshot> _snapshots;

    /// <summary>Initializes the catalog from registration evidence.</summary>
    /// <param name="registrations">Every registered coordinator.</param>
    /// <param name="services">The provider the retained profile revisions are resolved from.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="InvalidOperationException">A coordinator names a profile that is not registered.</exception>
    public DefaultArtifactCoordinatorCatalog(IEnumerable<ArtifactCoordinatorRegistration> registrations, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentNullException.ThrowIfNull(services);
        var builder = ImmutableDictionary.CreateBuilder<ComponentKey<IArtifactCoordinator>, ArtifactCoordinatorSnapshot>();
        foreach (var registration in registrations)
        {
            var versions = ArtifactRegistration.ProfileVersions(services, registration.ProfileKey);
            var current = versions.OrderByDescending(static version => version.Version.Value).First();
            builder[registration.Key] = new ArtifactCoordinatorSnapshot(
                registration.Key, registration.ProfileKey, current.Version,
                [.. versions.SelectMany(static version => version.Backends()).Distinct().OrderBy(static backend => backend.Value, StringComparer.Ordinal)]);
        }

        _snapshots = builder.ToImmutable();
    }

    /// <inheritdoc/>
    public bool TryGet(ComponentKey<IArtifactCoordinator> key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ArtifactCoordinatorSnapshot? snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        return _snapshots.TryGetValue(key, out snapshot);
    }
}
