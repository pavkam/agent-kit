// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Maps a logical directory of one profile revision to the explicitly registered backend that stores it.</summary>
/// <remarks>Backend keys are visible only to the composition root and the artifact runtime. A missing route or store is a typed outcome, never a hidden in-memory backend.</remarks>
internal interface IArtifactStoreSelector
{
    /// <summary>Selects the backend routed for a directory by a profile revision.</summary>
    /// <param name="profile">The immutable profile revision whose routes apply.</param>
    /// <param name="directoryId">The logical directory.</param>
    /// <param name="cancellationToken">Cancels selection.</param>
    /// <returns>The selected store, or an unavailable outcome when the directory is not routed or its store is not registered.</returns>
    public ValueTask<ArtifactStoreSelectionResult> SelectAsync(
        ArtifactProfileSnapshot profile,
        ArtifactDirectoryId directoryId,
        CancellationToken cancellationToken);

    /// <summary>Selects one backend by key, for operations that locate a preparation rather than a directory.</summary>
    /// <param name="backend">The configured backend key.</param>
    /// <param name="cancellationToken">Cancels selection.</param>
    /// <returns>The selected store, or an unavailable outcome when it is not registered.</returns>
    public ValueTask<ArtifactStoreSelectionResult> SelectBackendAsync(ArtifactBackendKey backend, CancellationToken cancellationToken);
}
