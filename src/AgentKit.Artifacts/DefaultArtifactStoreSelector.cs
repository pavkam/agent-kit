// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Resolves each routed backend from its explicit keyed registration.</summary>
internal sealed class DefaultArtifactStoreSelector: IArtifactStoreSelector
{
    private readonly IServiceProvider _services;

    /// <summary>Initializes the selector over the composition's service provider.</summary>
    /// <param name="services">The provider that holds keyed artifact stores.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public DefaultArtifactStoreSelector(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    /// <inheritdoc/>
    public ValueTask<ArtifactStoreSelectionResult> SelectAsync(
        ArtifactProfileSnapshot profile,
        ArtifactDirectoryId directoryId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        cancellationToken.ThrowIfCancellationRequested();
        return profile.Routes.TryGetValue(directoryId, out var backend)
            ? SelectBackendAsync(backend, cancellationToken)
            : ValueTask.FromResult<ArtifactStoreSelectionResult>(new ArtifactStoreUnavailable(
                new ArtifactFailure(ArtifactFailureKind.Unavailable, "The artifact directory is not routed by the selected profile.")));
    }

    /// <inheritdoc/>
    public ValueTask<ArtifactStoreSelectionResult> SelectBackendAsync(ArtifactBackendKey backend, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backend.Value, nameof(backend));
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<ArtifactStoreSelectionResult>(
            _services.GetKeyedService<IArtifactStore>(backend.Value) is { } store
                ? new ArtifactStoreSelected(backend, store)
                : new ArtifactStoreUnavailable(
                    new ArtifactFailure(ArtifactFailureKind.Unavailable, "No artifact store is registered for the routed backend.")));
    }
}
